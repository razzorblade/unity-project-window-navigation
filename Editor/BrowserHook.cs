using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Nyvion.Backtrack
{
    /// <summary>
    /// Ties one Project window to its trail. Draws Back / Forward / Up in its toolbar, right after the Create (+)
    /// dropdown, as toolbar buttons so they read as part of the native toolbar; the group lives in the window's
    /// rootVisualElement, which UI Toolkit draws above the window's IMGUI content. Also listens for the mouse
    /// thumb buttons on the window's panel and keeps a live snapshot (scroll, selection) of the open folder so
    /// stepping back lands exactly where the user left.
    /// </summary>
    internal sealed class BrowserHook
    {
        const string ElementName = "nyvion-backtrack";
        // Right edge of the Create (+) dropdown as drawn in Unity 6.3; the Back button's left border overlaps
        // the dropdown's right border so the group reads as one.
        const float LeftOffset = 26f;
        const float ButtonWidth = 24f;
        const float ButtonHeight = 20f;
        const int ButtonCount = 3;
        // Below this the search field shrinks far enough to slide under the buttons.
        const float MinWindowWidth = 370f;
        // U+2215 DIVISION SLASH: looks like '/' in a menu path without opening a submenu.
        const char DivisionSlash = (char)0x2215;
        // UI Toolkit numbers the thumb buttons 3 (back) and 4 (forward), like Event.button.
        const int MouseBackButton = 3;
        const int MouseForwardButton = 4;
        const int MaxRememberedSelection = 64;

        static readonly List<string> SelectionBuffer = new List<string>();

        static GUIContent backContent;
        static GUIContent forwardContent;
        static GUIContent upContent;

        public readonly EditorWindow Window;

        readonly FolderTrail trail;
        readonly Func<string> activeFolder;
        readonly Func<bool> isShowingFolder;
        readonly Func<bool> isTwoColumns;
        readonly IMGUIContainer overlay;
        readonly EventCallback<PointerDownEvent> onPointerDown;

        VisualElement pointerRoot;
        string lastSeenPath;
        // True while the trail's current visit is the folder in lastSeenPath, i.e. the live snapshot belongs to it.
        bool tracking;
        Vector2 liveScroll;
        string[] liveSelection = Array.Empty<string>();
        bool visible = true;

        public BrowserHook(EditorWindow window, FolderTrail trail)
        {
            Window = window;
            this.trail = trail;
            activeFolder = ProjectBrowserBridge.BindActiveFolder(window);
            isShowingFolder = ProjectBrowserBridge.BindIsShowingFolder(window);
            isTwoColumns = ProjectBrowserBridge.BindIsTwoColumns(window);
            onPointerDown = OnPointerDown;

            overlay = new IMGUIContainer(OnGUI) { name = ElementName, focusable = false };
            overlay.style.position = Position.Absolute;
            overlay.style.left = LeftOffset;
            overlay.style.top = 0f;
            overlay.style.width = ButtonWidth * ButtonCount;
            overlay.style.height = ButtonHeight;
        }

        /// <summary>Navigation only makes sense in two-column layout, where the window has an open folder.</summary>
        public bool IsUsable => isTwoColumns();

        public void Poll()
        {
            if (overlay.parent == null) Attach();
            SyncPointerRoot();

            bool twoColumns = isTwoColumns();
            SetVisible(twoColumns && Window.position.width >= MinWindowWidth);

            // One-column mode has no open folder, and a search result is not a place to come back to.
            if (!twoColumns || !isShowingFolder()) return;

            string path = activeFolder();
            if (string.Equals(path, lastSeenPath, StringComparison.Ordinal))
            {
                if (tracking) ProjectBrowserBridge.TryGetScroll(Window, out liveScroll);
                return;
            }
            OnFolderChanged(path);
        }

        public void Detach()
        {
            SetPointerRoot(null);
            overlay.RemoveFromHierarchy();
        }

        public void Repaint() => overlay.MarkDirtyRepaint();

        /// <summary>Makes the next poll record the open folder again, so a cleared trail restarts from it.</summary>
        public void ResetLastSeen()
        {
            lastSeenPath = null;
            tracking = false;
        }

        /// <summary>Remembers what is selected in the open folder. Called on every editor selection change.</summary>
        public void OnSelectionChanged()
        {
            if (!isTwoColumns() || !isShowingFolder()) return;
            // The folder may have switched since the last poll; commit that first so the pick lands on the right visit.
            string path = activeFolder();
            if (!string.Equals(path, lastSeenPath, StringComparison.Ordinal)) OnFolderChanged(path);
            if (!tracking) return;

            string[] picked = SelectedAssetsIn(lastSeenPath);
            // Clicking empty space or another window deselects; keep the last real pick for when the user comes back.
            if (picked.Length > 0) liveSelection = picked;
        }

        /// <summary>Steps through the trail: -1 back, +1 forward.</summary>
        public void Step(int direction)
        {
            int target = trail.FindStep(direction);
            if (target >= 0) NavigateTo(target);
        }

        public bool CanGoUp => ParentOf(lastSeenPath) != null;

        /// <summary>Opens the parent folder, like Up in a file explorer, with the folder you came from selected.</summary>
        public void GoUp()
        {
            string child = lastSeenPath;
            string parent = ParentOf(child);
            if (parent == null) return;

            CaptureScroll();
            try
            {
                ProjectBrowserBridge.ShowFolder(Window, parent);
                OnFolderChanged(activeFolder());
                if (BacktrackSettings.RestoreSelection) Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(child);
                // Scroll the folder we came out of into view, so the eye lands where it left.
                ProjectBrowserBridge.FrameInList(Window, child);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            Window.Repaint();
        }

        void OnFolderChanged(string path)
        {
            StoreLiveState();
            lastSeenPath = path;
            liveScroll = default;
            liveSelection = Array.Empty<string>();

            string guid = AssetDatabase.AssetPathToGUID(path);
            tracking = !string.IsNullOrEmpty(guid);
            if (tracking) trail.Record(guid, BacktrackSettings.TrailCapacity);
            // Up depends on the path even when the trail did not change.
            overlay.MarkDirtyRepaint();
        }

        void NavigateTo(int target)
        {
            if (target < 0 || target >= trail.Count) return;
            FolderVisit visit = trail.VisitAt(target);
            string path = FolderTrail.ResolveFolder(visit.folder);
            if (path == null) return;

            CaptureScroll();
            StoreLiveState();
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
            tracking = true;
            liveScroll = visit.scroll;
            liveSelection = visit.selection ?? Array.Empty<string>();

            if (BacktrackSettings.RestoreSelection) RestoreSelection(liveSelection, lastSeenPath);
            // After the selection: nothing in between may reframe the list.
            ProjectBrowserBridge.SetScroll(Window, liveScroll);
            Window.Repaint();
            overlay.MarkDirtyRepaint();
        }

        /// <summary>Writes the live snapshot into the visit it was taken in.</summary>
        void StoreLiveState()
        {
            FolderVisit current = trail.Current;
            if (!tracking || current == null) return;
            current.scroll = liveScroll;
            current.selection = liveSelection;
        }

        void CaptureScroll()
        {
            if (tracking) ProjectBrowserBridge.TryGetScroll(Window, out liveScroll);
        }

        static void RestoreSelection(string[] guids, string folder)
        {
            if (guids.Length == 0) return;
            var objects = new List<Object>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                // Skip assets deleted or moved out of the folder since.
                if (!IsDirectChild(path, folder)) continue;
                Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null) objects.Add(asset);
            }
            if (objects.Count > 0) Selection.objects = objects.ToArray();
        }

        static string[] SelectedAssetsIn(string folder)
        {
            string[] guids = Selection.assetGUIDs;
            if (guids.Length == 0) return Array.Empty<string>();

            SelectionBuffer.Clear();
            for (int i = 0; i < guids.Length && SelectionBuffer.Count < MaxRememberedSelection; i++)
            {
                if (IsDirectChild(AssetDatabase.GUIDToAssetPath(guids[i]), folder)) SelectionBuffer.Add(guids[i]);
            }
            return SelectionBuffer.Count > 0 ? SelectionBuffer.ToArray() : Array.Empty<string>();
        }

        static bool IsDirectChild(string path, string folder)
        {
            return !string.IsNullOrEmpty(path)
                && path.Length > folder.Length + 1
                && path[folder.Length] == '/'
                && path.StartsWith(folder, StringComparison.Ordinal)
                && path.IndexOf('/', folder.Length + 1) < 0;
        }

        /// <summary>Parent folder of <paramref name="path"/>, or null at a root (Assets, a package root).</summary>
        static string ParentOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) return null;
            string parent = path.Substring(0, slash);
            // "Packages" itself is a virtual root, not a folder that can be opened.
            return AssetDatabase.IsValidFolder(parent) ? parent : null;
        }

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

        // The thumb buttons are caught on the panel root so they work anywhere over the window, not just the toolbar.
        // The panel changes when the window is docked elsewhere, so re-check it every poll.
        void SyncPointerRoot()
        {
            IPanel panel = BacktrackSettings.MouseButtons ? Window.rootVisualElement.panel : null;
            SetPointerRoot(panel?.visualTree);
        }

        void SetPointerRoot(VisualElement root)
        {
            if (pointerRoot == root) return;
            pointerRoot?.UnregisterCallback(onPointerDown, TrickleDown.TrickleDown);
            pointerRoot = root;
            pointerRoot?.RegisterCallback(onPointerDown, TrickleDown.TrickleDown);
        }

        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != MouseBackButton && e.button != MouseForwardButton) return;
            // Tabs docked together share a panel; act only while this window is the one showing.
            if (Window.rootVisualElement.panel?.visualTree != pointerRoot || !isTwoColumns()) return;

            Step(e.button == MouseBackButton ? -1 : 1);
            e.StopImmediatePropagation();
        }

        void OnGUI()
        {
            EnsureContents();

            var backRect = new Rect(0f, 0f, ButtonWidth, ButtonHeight);
            var forwardRect = new Rect(ButtonWidth, 0f, ButtonWidth, ButtonHeight);
            var upRect = new Rect(ButtonWidth * 2f, 0f, ButtonWidth, ButtonHeight);

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
            GUI.enabled = CanGoUp;
            if (GUI.Button(upRect, upContent, EditorStyles.toolbarButton)) GoUp();
            GUI.enabled = wasEnabled;
        }

        void ShowTrailMenu(Rect anchor)
        {
            var menu = new GenericMenu();
            // Newest first, like a browser's history dropdown.
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                string path = FolderTrail.ResolveFolder(trail.VisitAt(i).folder);
                if (path == null) continue;
                int target = i;
                menu.AddItem(new GUIContent(path.Replace('/', DivisionSlash)), i == trail.Index, () => NavigateTo(target));
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No folder history"));
            menu.DropDown(anchor);
        }

        /// <summary>Drops cached button contents so tooltips pick up rebound shortcuts.</summary>
        public static void InvalidateContents() => backContent = null;

        static void EnsureContents()
        {
            if (backContent != null) return;
            backContent = new GUIContent(EditorGUIUtility.IconContent("back").image,
                Tooltip("Back", BacktrackShortcuts.BackId, "right click for history"));
            forwardContent = new GUIContent(EditorGUIUtility.IconContent("forward").image,
                Tooltip("Forward", BacktrackShortcuts.ForwardId, "right click for history"));
            upContent = new GUIContent(EditorGUIUtility.IconContent("scrollup").image,
                Tooltip("Up to parent folder", BacktrackShortcuts.UpId, null));
        }

        static string Tooltip(string action, string shortcutId, string hint)
        {
            string binding = ShortcutManager.instance.GetShortcutBinding(shortcutId).ToString();
            bool hasBinding = !string.IsNullOrEmpty(binding);
            if (!hasBinding && hint == null) return action;
            if (!hasBinding) return action + " (" + hint + ")";
            return hint == null ? action + " (" + binding + ")" : action + " (" + binding + ", " + hint + ")";
        }
    }
}
