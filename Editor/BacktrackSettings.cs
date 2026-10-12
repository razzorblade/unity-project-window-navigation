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

        static int historySize = -1;

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

        [SettingsProvider]
        static SettingsProvider CreateProvider() => new SettingsProvider("Preferences/Nyvion/Backtrack", SettingsScope.User)
        {
            label = "Backtrack",
            keywords = new[] { "back", "forward", "history", "folder", "project" },
            guiHandler = _ =>
            {
                EditorGUIUtility.labelWidth = 200f;
                HistorySize = EditorGUILayout.IntSlider(
                    new GUIContent("Folders Remembered", "How many folders the Project window Back button can return to."),
                    HistorySize, MinHistorySize, MaxHistorySize);

                EditorGUILayout.Space();
                if (GUILayout.Button("Clear History", GUILayout.Width(120f))) HistoryCleared?.Invoke();
            },
        };
    }
}
