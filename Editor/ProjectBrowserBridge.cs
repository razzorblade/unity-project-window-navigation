using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nyvion.Backtrack
{
    /// <summary>
    /// The only place that touches Unity's internal ProjectBrowser. Members are resolved once per domain;
    /// per-window queries are bound as closed delegates so polling costs a plain call, not a reflection invoke.
    /// If a Unity upgrade renames anything, <see cref="IsAvailable"/> turns false and Backtrack stays dormant.
    /// </summary>
    internal static class ProjectBrowserBridge
    {
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly FieldInfo BrowsersField;
        static readonly MethodInfo ActiveFolderMethod;
        static readonly MethodInfo IsShowingFolderMethod;
        static readonly MethodInfo IsTwoColumnsMethod;
        static readonly MethodInfo FolderIdMethod;
        static readonly MethodInfo ShowFolderMethod;

        public static readonly bool IsAvailable;

        static ProjectBrowserBridge()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (type == null)
            {
                Debug.LogWarning("[Backtrack] UnityEditor.ProjectBrowser not found; navigation buttons disabled.");
                return;
            }

            BrowsersField = type.GetField("s_ProjectBrowsers", StaticFlags);
            ActiveFolderMethod = type.GetMethod("GetActiveFolderPath", InstanceFlags, null, Type.EmptyTypes, null);
            IsShowingFolderMethod = type.GetMethod("IsShowingFolderContents", InstanceFlags, null, Type.EmptyTypes, null);
            IsTwoColumnsMethod = type.GetMethod("IsTwoColumns", InstanceFlags, null, Type.EmptyTypes, null);
            FolderIdMethod = type.GetMethod("GetFolderInstanceID", StaticFlags, null, new[] { typeof(string) }, null);
            // The id parameter is int before Unity 6.2 and EntityId after; match by shape and pass the boxed id through.
            foreach (MethodInfo method in type.GetMethods(InstanceFlags))
            {
                if (method.Name != "ShowFolderContents") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool))
                {
                    ShowFolderMethod = method;
                    break;
                }
            }

            IsAvailable = BrowsersField != null && typeof(IList).IsAssignableFrom(BrowsersField.FieldType)
                && ActiveFolderMethod != null && ActiveFolderMethod.ReturnType == typeof(string)
                && IsShowingFolderMethod != null && IsShowingFolderMethod.ReturnType == typeof(bool)
                && IsTwoColumnsMethod != null && IsTwoColumnsMethod.ReturnType == typeof(bool)
                && FolderIdMethod != null && ShowFolderMethod != null
                && ShowFolderMethod.GetParameters()[0].ParameterType == FolderIdMethod.ReturnType;

            if (!IsAvailable)
                Debug.LogWarning("[Backtrack] ProjectBrowser internals changed in this Unity version; navigation buttons disabled.");
        }

        /// <summary>Live list of open Project windows, owned by Unity. Do not modify.</summary>
        public static IList Browsers => (IList)BrowsersField.GetValue(null);

        public static Func<string> BindActiveFolder(EditorWindow browser)
            => (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), browser, ActiveFolderMethod);

        public static Func<bool> BindIsShowingFolder(EditorWindow browser)
            => (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), browser, IsShowingFolderMethod);

        public static Func<bool> BindIsTwoColumns(EditorWindow browser)
            => (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), browser, IsTwoColumnsMethod);

        /// <summary>Opens <paramref name="folderPath"/> in the right pane and reveals it in the folder tree.</summary>
        public static void ShowFolder(EditorWindow browser, string folderPath)
        {
            object folderId = FolderIdMethod.Invoke(null, new object[] { folderPath });
            ShowFolderMethod.Invoke(browser, new[] { folderId, true });
        }
    }
}
