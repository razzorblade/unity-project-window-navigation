using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;

namespace Nyvion.Backtrack
{
    /// <summary>
    /// Keeps one <see cref="BrowserHook"/> per open Project window and polls them on a short interval.
    /// Polling a cached delegate is far cheaper than hooking per-item GUI callbacks, and catches every way the
    /// folder can change (tree click, double click, breadcrumb, ping, script).
    /// </summary>
    [InitializeOnLoad]
    internal static class BacktrackNavigator
    {
        const double PollInterval = 0.1;

        static readonly List<BrowserHook> Hooks = new List<BrowserHook>();
        static readonly ProjectWindowShortcutContext ShortcutContext = new ProjectWindowShortcutContext();
        static double nextPoll;
        static bool pruned;

        static BacktrackNavigator()
        {
            if (!ProjectBrowserBridge.IsAvailable) return;

            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            Selection.selectionChanged += OnSelectionChanged;
            BacktrackSettings.HistorySizeChanged += OnHistorySizeChanged;
            BacktrackSettings.HistoryCleared += OnHistoryCleared;
            ShortcutManager.RegisterContext(ShortcutContext);
            ShortcutManager.instance.shortcutBindingChanged += OnShortcutsChanged;
            ShortcutManager.instance.activeProfileChanged += OnShortcutsChanged;
        }

        /// <summary>Hook of the focused Project window, if it can navigate. Target of the keyboard shortcuts.</summary>
        public static BrowserHook FocusedHook()
        {
            EditorWindow focused = EditorWindow.focusedWindow;
            if (focused == null) return null;
            for (int i = 0; i < Hooks.Count; i++)
            {
                if (Hooks[i].Window == focused) return Hooks[i].IsUsable ? Hooks[i] : null;
            }
            return null;
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPoll) return;
            nextPoll = now + PollInterval;

            SyncHooks();
            for (int i = 0; i < Hooks.Count; i++) Hooks[i].Poll();
        }

        static void SyncHooks()
        {
            IList browsers = ProjectBrowserBridge.Browsers;
            bool removed = false;

            for (int i = Hooks.Count - 1; i >= 0; i--)
            {
                EditorWindow window = Hooks[i].Window;
                if (window != null && browsers.Contains(window)) continue;
                Hooks[i].Detach();
                Hooks.RemoveAt(i);
                removed = true;
            }

            for (int i = 0; i < browsers.Count; i++)
            {
                if (browsers[i] is EditorWindow browser && !IsHooked(browser))
                    Hooks.Add(new BrowserHook(browser, BacktrackState.instance.TrailFor(browser)));
            }

            // Trails restored after a domain reload may belong to windows closed in the meantime.
            if (removed || !pruned)
            {
                BacktrackState.instance.Prune();
                pruned = true;
            }
        }

        static bool IsHooked(EditorWindow browser)
        {
            for (int i = 0; i < Hooks.Count; i++)
            {
                if (Hooks[i].Window == browser) return true;
            }
            return false;
        }

        static void OnSelectionChanged()
        {
            for (int i = 0; i < Hooks.Count; i++) Hooks[i].OnSelectionChanged();
        }

        static void OnBeforeAssemblyReload()
        {
            ShortcutManager.UnregisterContext(ShortcutContext);
            for (int i = 0; i < Hooks.Count; i++) Hooks[i].Detach();
            Hooks.Clear();
        }

        static void OnHistorySizeChanged()
        {
            BacktrackState.instance.TrimAll(BacktrackSettings.TrailCapacity);
            RepaintAll();
        }

        static void OnHistoryCleared()
        {
            BacktrackState.instance.ClearAll();
            for (int i = 0; i < Hooks.Count; i++) Hooks[i].ResetLastSeen();
            RepaintAll();
        }

        static void OnShortcutsChanged<T>(T _)
        {
            BrowserHook.InvalidateContents();
            RepaintAll();
        }

        static void RepaintAll()
        {
            for (int i = 0; i < Hooks.Count; i++) Hooks[i].Repaint();
        }
    }
}
