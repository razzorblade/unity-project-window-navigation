using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Nyvion.Backtrack
{
    /// <summary>
    /// Active while a Project window that can navigate has keyboard focus. ProjectBrowser is internal, so the
    /// usual typeof(EditorWindow) context is not possible; a custom context does the same job. It stays inactive
    /// while a text field is being edited, so Alt+arrows keep working in rename and search fields.
    /// </summary>
    internal sealed class ProjectWindowShortcutContext : IShortcutContext
    {
        public bool active => !EditorGUIUtility.editingTextField && BacktrackNavigator.FocusedHook() != null;
    }

    /// <summary>Keyboard shortcuts, rebindable under Edit > Shortcuts > Backtrack.</summary>
    internal static class BacktrackShortcuts
    {
        public const string BackId = "Backtrack/Back";
        public const string ForwardId = "Backtrack/Forward";
        public const string UpId = "Backtrack/Up";

        [Shortcut(BackId, typeof(ProjectWindowShortcutContext), KeyCode.LeftArrow, ShortcutModifiers.Alt)]
        static void Back() => BacktrackNavigator.FocusedHook()?.Step(-1);

        [Shortcut(ForwardId, typeof(ProjectWindowShortcutContext), KeyCode.RightArrow, ShortcutModifiers.Alt)]
        static void Forward() => BacktrackNavigator.FocusedHook()?.Step(1);

        [Shortcut(UpId, typeof(ProjectWindowShortcutContext), KeyCode.UpArrow, ShortcutModifiers.Alt)]
        static void Up() => BacktrackNavigator.FocusedHook()?.GoUp();
    }
}
