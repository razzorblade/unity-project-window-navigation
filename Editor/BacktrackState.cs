using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nyvion.Backtrack
{
    /// <summary>
    /// Holds every window's trail. A ScriptableSingleton without a file path is serialized across domain
    /// reloads but not across editor restarts, which is the lifetime folder history wants.
    /// </summary>
    internal sealed class BacktrackState : ScriptableSingleton<BacktrackState>
    {
        static readonly System.Predicate<FolderTrail> IsOrphaned = trail => trail == null || trail.Owner == null;

        [SerializeField] List<FolderTrail> trails = new List<FolderTrail>();

        public FolderTrail TrailFor(EditorWindow browser)
        {
            for (int i = 0; i < trails.Count; i++)
            {
                if (trails[i].Owner == browser) return trails[i];
            }
            var trail = new FolderTrail(browser);
            trails.Add(trail);
            return trail;
        }

        /// <summary>Forgets trails of closed windows.</summary>
        public void Prune() => trails.RemoveAll(IsOrphaned);

        public void TrimAll(int capacity)
        {
            for (int i = 0; i < trails.Count; i++) trails[i].Trim(capacity);
        }

        public void ClearAll()
        {
            for (int i = 0; i < trails.Count; i++) trails[i].Clear();
        }
    }
}
