using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nyvion.Backtrack
{
    /// <summary>
    /// Browser-style history of one Project window. Folders are stored by GUID so renames and moves keep working;
    /// folders deleted since they were visited are skipped when stepping.
    /// </summary>
    [Serializable]
    internal sealed class FolderTrail
    {
        [SerializeField] EditorWindow owner;
        [SerializeField] List<string> guids = new List<string>();
        [SerializeField] int index = -1;

        public FolderTrail(EditorWindow owner) => this.owner = owner;

        public EditorWindow Owner => owner;
        public int Count => guids.Count;
        public int Index => index;
        public string GuidAt(int i) => guids[i];

        /// <summary>Appends a visit, dropping the forward branch. Returns false if it is already the current entry.</summary>
        public bool Record(string guid, int capacity)
        {
            if (index >= 0 && guids[index] == guid) return false;

            int forward = guids.Count - index - 1;
            if (forward > 0) guids.RemoveRange(index + 1, forward);
            guids.Add(guid);
            index = guids.Count - 1;
            Trim(capacity);
            return true;
        }

        /// <summary>Index of the nearest still existing folder in <paramref name="direction"/> (-1 back, +1 forward), or -1.</summary>
        public int FindStep(int direction)
        {
            for (int i = index + direction; i >= 0 && i < guids.Count; i += direction)
            {
                if (ResolveFolder(guids[i]) != null) return i;
            }
            return -1;
        }

        public void MoveTo(int target) => index = target;

        /// <summary>Drops the oldest entries first, then the far forward ones, so the current folder always stays.</summary>
        public void Trim(int capacity)
        {
            int overflow = Math.Min(guids.Count - capacity, index);
            if (overflow > 0)
            {
                guids.RemoveRange(0, overflow);
                index -= overflow;
            }
            if (guids.Count > capacity) guids.RemoveRange(capacity, guids.Count - capacity);
        }

        public void Clear()
        {
            guids.Clear();
            index = -1;
        }

        /// <summary>Asset path of the folder, or null if the GUID no longer points at a folder.</summary>
        public static string ResolveFolder(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path) ? path : null;
        }
    }
}
