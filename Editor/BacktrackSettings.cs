using System;
using UnityEditor;
using UnityEngine;

namespace Nyvion.Backtrack
{
    /// <summary>Per-user settings (EditorPrefs), shown under Preferences > Nyvion > Backtrack.</summary>
    internal static class BacktrackSettings
    {
        public const int DefaultHistorySize = 10;
        public const int MinHistorySize = 1;
        public const int MaxHistorySize = 100;

        const string HistorySizeKey = "Nyvion.Backtrack.HistorySize";
        const string MouseButtonsKey = "Nyvion.Backtrack.MouseButtons";
        const string RestoreSelectionKey = "Nyvion.Backtrack.RestoreSelection";

        static int historySize = -1;
        static readonly BoolPref mouseButtons = new BoolPref(MouseButtonsKey, true);
        static readonly BoolPref restoreSelection = new BoolPref(RestoreSelectionKey, true);

        public static event Action HistorySizeChanged;
        public static event Action HistoryCleared;

        /// <summary>How many folders Back can return to.</summary>
        public static int HistorySize
        {
            get
            {
                if (historySize < 0)
                    historySize = Mathf.Clamp(EditorPrefs.GetInt(HistorySizeKey, DefaultHistorySize), MinHistorySize, MaxHistorySize);
                return historySize;
            }
            set
            {
                value = Mathf.Clamp(value, MinHistorySize, MaxHistorySize);
                if (value == HistorySize) return;
                historySize = value;
                EditorPrefs.SetInt(HistorySizeKey, value);
                HistorySizeChanged?.Invoke();
            }
        }

        /// <summary>Entries a trail keeps: the current folder plus <see cref="HistorySize"/> behind it.</summary>
        public static int TrailCapacity => HistorySize + 1;

        /// <summary>Mouse thumb buttons (4 and 5) step Back and Forward while the pointer is over a Project window.</summary>
        public static bool MouseButtons
        {
            get => mouseButtons.Value;
            set => mouseButtons.Value = value;
        }

        /// <summary>Back, Forward and Up reselect the assets you had selected in that folder.</summary>
        public static bool RestoreSelection
        {
            get => restoreSelection.Value;
            set => restoreSelection.Value = value;
        }

        [SettingsProvider]
        static SettingsProvider CreateProvider() => new SettingsProvider("Preferences/Nyvion/Backtrack", SettingsScope.User)
        {
            label = "Backtrack",
            keywords = new[] { "back", "forward", "up", "history", "folder", "project", "mouse", "shortcut", "selection" },
            guiHandler = _ =>
            {
                EditorGUIUtility.labelWidth = 200f;
                HistorySize = EditorGUILayout.IntSlider(
                    new GUIContent("Folders Remembered", "How many folders the Project window Back button can return to."),
                    HistorySize, MinHistorySize, MaxHistorySize);
                MouseButtons = EditorGUILayout.Toggle(
                    new GUIContent("Mouse Side Buttons", "Mouse buttons 4 and 5 go Back and Forward while the pointer is over a Project window."),
                    MouseButtons);
                RestoreSelection = EditorGUILayout.Toggle(
                    new GUIContent("Restore Selection", "Back and Forward reselect what you had selected in that folder. Up selects the folder you came from."),
                    RestoreSelection);

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Keyboard Shortcuts...", GUILayout.Width(150f))) OpenShortcutManager();
                    if (GUILayout.Button("Clear History", GUILayout.Width(120f))) HistoryCleared?.Invoke();
                }
            },
        };

        static void OpenShortcutManager()
        {
            // The window type is internal; its menu item lives under Edit on Windows and Linux, under the app menu on macOS.
            if (!EditorApplication.ExecuteMenuItem("Edit/Shortcuts...")) EditorApplication.ExecuteMenuItem("Unity/Shortcuts...");
        }

        sealed class BoolPref
        {
            readonly string key;
            readonly bool fallback;
            bool loaded;
            bool value;

            public BoolPref(string key, bool fallback)
            {
                this.key = key;
                this.fallback = fallback;
            }

            public bool Value
            {
                get
                {
                    if (!loaded)
                    {
                        value = EditorPrefs.GetBool(key, fallback);
                        loaded = true;
                    }
                    return value;
                }
                set
                {
                    if (Value == value) return;
                    this.value = value;
                    EditorPrefs.SetBool(key, value);
                }
            }
        }
    }
}
