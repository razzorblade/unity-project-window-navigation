using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nyvion.Backtrack
{
    /// <summary>
    /// Ties one Project window to its trail and draws the Back / Forward pair in its toolbar, right after the
    /// Create (+) dropdown, as toolbar buttons so they read as part of the native toolbar. The pair lives in the
    /// window's rootVisualElement, which UI Toolkit draws above the window's IMGUI content.
    /// Left click steps; right click lists the whole trail.
    /// </summary>
    internal sealed class BrowserHook
    {
        const string ElementName = "nyvion-backtrack";
        // Right edge of the Create (+) dropdown as drawn in Unity 6.3; the Back button's left border overlaps
        // the dropdown's right border so the three read as one group.
        const float LeftOffset = 26f;
        const float ButtonWidth = 24f;
        const float ButtonHeight = 20f;
        // Below this the search field shrinks far enough to slide under the buttons.
        const float MinWindowWidth = 340f;
        // U+2215 DIVISION SLASH: looks like '/' in a menu path without opening a submenu.
        const char DivisionSlash = (char)0x2215;

        static GUIContent backContent;
        static GUIContent forwardContent;

        public readonly EditorWindow Window;

        readonly FolderTrail trail;
        readonly Func<string> activeFolder;
        readonly Func<bool> isShowingFolder;
        readonly Func<bool> isTwoColumns;
        readonly IMGUIContainer overlay;

        string lastSeenPath;
        bool visible = true;

        public BrowserHook(EditorWindow window, FolderTrail trail)
        {
            Window = window;
            this.trail = trail;
            activeFolder = ProjectBrowserBridge.BindActiveFolder(window);
            isShowingFolder = ProjectBrowserBridge.BindIsShowingFolder(window);
            isTwoColumns = ProjectBrowserBridge.BindIsTwoColumns(window);

            overlay = new IMGUIContainer(OnGUI) { name = ElementName, focusable = false };
            overlay.style.position = Position.Absolute;
            overlay.style.left = LeftOffset;
            overlay.style.top = 0f;
            overlay.style.width = ButtonWidth * 2f;
            overlay.style.height = ButtonHeight;
        }

        public void Poll()
        {
            if (overlay.parent == null) Attach();

            bool twoColumns = isTwoColumns();
            SetVisible(twoColumns && Window.position.width >= MinWindowWidth);

            // One-column mode has no open folder, and a search result is not a place to come back to.
            if (!twoColumns || !isShowingFolder()) return;

            string path = activeFolder();
            if (string.Equals(path, lastSeenPath, StringComparison.Ordinal)) return;
            lastSeenPath = path;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid) && trail.Record(guid, BacktrackSettings.TrailCapacity)) overlay.MarkDirtyRepaint();
        }

        public void Detach() => overlay.RemoveFromHierarchy();

        public void Repaint() => overlay.MarkDirtyRepaint();

        /// <summary>Makes the next poll record the open folder again, so a cleared trail restarts from it.</summary>
        public void ResetLastSeen() => lastSeenPath = null;

        void Attach()
        {
            VisualElement root = Window.rootVisualElement;
            root.Q(ElementName)?.RemoveFromHierarchy();
            root.Add(overlay);
        }

        void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            overlay.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnGUI()
        {
            EnsureContents();

            var backRect = new Rect(0f, 0f, ButtonWidth, ButtonHeight);
            var forwardRect = new Rect(ButtonWidth, 0f, ButtonWidth, ButtonHeight);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                if (backRect.Contains(e.mousePosition)) ShowTrailMenu(backRect);
                else if (forwardRect.Contains(e.mousePosition)) ShowTrailMenu(forwardRect);
                e.Use();
                return;
            }

            int back = trail.FindStep(-1);
            int forward = trail.FindStep(1);

            bool wasEnabled = GUI.enabled;
            GUI.enabled = back >= 0;
            if (GUI.Button(backRect, backContent, EditorStyles.toolbarButton)) NavigateTo(back);
            GUI.enabled = forward >= 0;
            if (GUI.Button(forwardRect, forwardContent, EditorStyles.toolbarButton)) NavigateTo(forward);
            GUI.enabled = wasEnabled;
        }

        void ShowTrailMenu(Rect anchor)
        {
            var menu = new GenericMenu();
            // Newest first, like a browser's history dropdown.
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                string path = FolderTrail.ResolveFolder(trail.GuidAt(i));
                if (path == null) continue;
                int target = i;
                // A plain '/' would turn into a submenu; DIVISION SLASH looks the same.
                menu.AddItem(new GUIContent(path.Replace('/', DivisionSlash)), i == trail.Index, () => NavigateTo(target));
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No folder history"));
            menu.DropDown(anchor);
        }

        void NavigateTo(int target)
        {
            if (target < 0 || target >= trail.Count) return;
            string path = FolderTrail.ResolveFolder(trail.GuidAt(target));
            if (path == null) return;

            try
            {
                ProjectBrowserBridge.ShowFolder(Window, path);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            trail.MoveTo(target);
            lastSeenPath = activeFolder();
            overlay.MarkDirtyRepaint();
        }

        static void EnsureContents()
        {
            if (backContent != null) return;
            backContent = new GUIContent(EditorGUIUtility.IconContent("back").image, "Back (right-click for history)");
            forwardContent = new GUIContent(EditorGUIUtility.IconContent("forward").image, "Forward (right-click for history)");
        }
    }
}
