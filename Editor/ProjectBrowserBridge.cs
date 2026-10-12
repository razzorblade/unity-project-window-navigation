using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nyvion.Backtrack
{
    /// <summary>
    /// The only place that touches Unity's internal ProjectBrowser. Members are resolved once per domain;
    /// per-window queries are bound as closed delegates and the scroll field as compiled accessors, so polling
    /// costs a plain call, not a reflection invoke.
    /// If a Unity upgrade renames the core members, <see cref="IsAvailable"/> turns false and Backtrack stays
    /// dormant. Optional extras (scroll restore, framing on Up) degrade on their own flags.
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
        static readonly FieldInfo ListAreaField;
        static readonly MethodInfo FrameInListMethod;
        static readonly MethodInfo FrameIdConversion;
        static readonly FieldInfo ListAreaStateField;
        static readonly Func<object, Vector2> GetScrollPosition;
        static readonly Action<object, Vector2> SetScrollPosition;

        public static readonly bool IsAvailable;
        /// <summary>Scroll position of the asset list can be read and written.</summary>
        public static readonly bool CanAccessScroll;
        /// <summary>The asset list can be scrolled to show a given asset.</summary>
        public static readonly bool CanFrame;

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
            // Ids are int before Unity 6.2 and EntityId after; match by shape and pass the boxed id straight through.
            Type idType = FolderIdMethod?.ReturnType;
            ShowFolderMethod = FindMethod(type, "ShowFolderContents", idType);

            IsAvailable = BrowsersField != null && typeof(IList).IsAssignableFrom(BrowsersField.FieldType)
                && ActiveFolderMethod != null && ActiveFolderMethod.ReturnType == typeof(string)
                && IsShowingFolderMethod != null && IsShowingFolderMethod.ReturnType == typeof(bool)
                && IsTwoColumnsMethod != null && IsTwoColumnsMethod.ReturnType == typeof(bool)
                && FolderIdMethod != null && ShowFolderMethod != null;

            if (!IsAvailable)
            {
                Debug.LogWarning("[Backtrack] ProjectBrowser internals changed in this Unity version; navigation buttons disabled.");
                return;
            }

            // ObjectListArea.Frame(id, frame, ping) scrolls the list to an item. Unlike ProjectBrowser.FrameObject it
            // never changes folder, so it also works in a locked Project window. Its id may be an older type than
            // the folder id (int vs EntityId in 6.3), so bridge the two through the id type's implicit operator.
            ListAreaField = type.GetField("m_ListArea", InstanceFlags);
            Type listAreaType = ListAreaField?.FieldType;
            if (listAreaType != null)
            {
                foreach (MethodInfo method in listAreaType.GetMethods(InstanceFlags))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (method.Name != "Frame" || parameters.Length != 3
                        || parameters[1].ParameterType != typeof(bool) || parameters[2].ParameterType != typeof(bool)) continue;
                    Type frameIdType = parameters[0].ParameterType;
                    if (frameIdType == idType)
                    {
                        FrameInListMethod = method;
                        break;
                    }
                    MethodInfo conversion = FindImplicitConversion(idType, frameIdType);
                    if (conversion != null)
                    {
                        FrameInListMethod = method;
                        FrameIdConversion = conversion;
                        break;
                    }
                }
            }
            CanFrame = FrameInListMethod != null;

            ListAreaStateField = type.GetField("m_ListAreaState", InstanceFlags);
            FieldInfo scrollField = ListAreaStateField?.FieldType.GetField("m_ScrollPosition", InstanceFlags);
            if (scrollField != null && scrollField.FieldType == typeof(Vector2) && !ListAreaStateField.FieldType.IsValueType)
            {
                try
                {
                    ParameterExpression state = Expression.Parameter(typeof(object), "state");
                    ParameterExpression value = Expression.Parameter(typeof(Vector2), "value");
                    MemberExpression field = Expression.Field(Expression.Convert(state, scrollField.DeclaringType), scrollField);
                    GetScrollPosition = Expression.Lambda<Func<object, Vector2>>(field, state).Compile();
                    SetScrollPosition = Expression.Lambda<Action<object, Vector2>>(Expression.Assign(field, value), state, value).Compile();
                    CanAccessScroll = true;
                }
                catch (Exception)
                {
                    GetScrollPosition = null;
                    SetScrollPosition = null;
                }
            }
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
            ShowFolderMethod.Invoke(browser, new[] { FolderId(folderPath), true });
        }

        /// <summary>Scrolls the asset list so the folder at <paramref name="folderPath"/> is in view.</summary>
        public static void FrameInList(EditorWindow browser, string folderPath)
        {
            object listArea = CanFrame ? ListAreaField.GetValue(browser) : null;
            if (listArea == null) return;
            object id = FolderId(folderPath);
            if (FrameIdConversion != null) id = FrameIdConversion.Invoke(null, new[] { id });
            FrameInListMethod.Invoke(listArea, new[] { id, true, false });
        }

        public static bool TryGetScroll(EditorWindow browser, out Vector2 scroll)
        {
            object state = CanAccessScroll ? ListAreaStateField.GetValue(browser) : null;
            scroll = state != null ? GetScrollPosition(state) : default;
            return state != null;
        }

        public static void SetScroll(EditorWindow browser, Vector2 scroll)
        {
            object state = CanAccessScroll ? ListAreaStateField.GetValue(browser) : null;
            if (state != null) SetScrollPosition(state, scroll);
        }

        static object FolderId(string folderPath) => FolderIdMethod.Invoke(null, new object[] { folderPath });

        static MethodInfo FindImplicitConversion(Type from, Type to)
        {
            foreach (Type owner in new[] { from, to })
            {
                foreach (MethodInfo method in owner.GetMethods(StaticFlags))
                {
                    if (method.Name != "op_Implicit" || method.ReturnType != to) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == from) return method;
                }
            }
            return null;
        }

        static MethodInfo FindMethod(Type type, string name, Type idType)
        {
            if (idType == null) return null;
            foreach (MethodInfo method in type.GetMethods(InstanceFlags))
            {
                if (method.Name != name) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == idType && parameters[1].ParameterType == typeof(bool))
                    return method;
            }
            return null;
        }
    }
}
