using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nyvion.Backtrack
{
    /// <summary>One remembered folder, plus where the user was inside it when they left.</summary>
    [Serializable]
    internal sealed class FolderVisit
    {
        public string folder;
        public Vector2 scroll;
        public string[] selection = Array.Empty<string>();

        public FolderVisit(string folder) => this.folder = folder;
    }

    /// <summary>
    /// Browser-style history of one Project window. Folders are stored by GUID so renames and moves keep working;
    /// folders deleted since they were visited are skipped when stepping.
    /// </summary>
    [Serializable]
    internal sealed class FolderTrail : ISerializationCallbackReceiver
    {
        [SerializeField] EditorWindow owner;
        [SerializeField] List<FolderVisit> visits = new List<FolderVisit>();
        [SerializeField] int index = -1;

        public FolderTrail(EditorWindow owner) => this.owner = owner;

        public EditorWindow Owner => owner;
        public int Count => visits.Count;
        public int Index => index;
        public FolderVisit Current => index >= 0 && index < visits.Count ? visits[index] : null;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        // Data restored after a domain reload can come from an older layout of this class (e.g. after a package
        // update), so never trust it: drop broken visits and pull the index back into range.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (visits == null) visits = new List<FolderVisit>();
            visits.RemoveAll(visit => visit == null || string.IsNullOrEmpty(visit.folder));
            for (int i = 0; i < visits.Count; i++)
            {
                if (visits[i].selection == null) visits[i].selection = Array.Empty<string>();
            }
            index = Math.Min(Math.Max(index, visits.Count > 0 ? 0 : -1), visits.Count - 1);
        }
        public FolderVisit VisitAt(int i) => visits[i];

        /// <summary>Appends a visit, dropping the forward branch. Returns false if it is already the current entry.</summary>
        public bool Record(string folderGuid, int capacity)
        {
            if (Current?.folder == folderGuid) return false;

            int forward = visits.Count - index - 1;
            if (forward > 0) visits.RemoveRange(index + 1, forward);
            visits.Add(new FolderVisit(folderGuid));
            index = visits.Count - 1;
            Trim(capacity);
            return true;
        }

        /// <summary>Index of the nearest still existing folder in <paramref name="direction"/> (-1 back, +1 forward), or -1.</summary>
        public int FindStep(int direction)
        {
            for (int i = index + direction; i >= 0 && i < visits.Count; i += direction)
            {
                if (ResolveFolder(visits[i].folder) != null) return i;
            }
            return -1;
        }

        public void MoveTo(int target) => index = target;

        /// <summary>Drops the oldest entries first, then the far forward ones, so the current folder always stays.</summary>
        public void Trim(int capacity)
        {
            int overflow = Math.Min(visits.Count - capacity, index);
            if (overflow > 0)
            {
                visits.RemoveRange(0, overflow);
                index -= overflow;
            }
            if (visits.Count > capacity) visits.RemoveRange(capacity, visits.Count - capacity);
        }

        public void Clear()
        {
            visits.Clear();
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
