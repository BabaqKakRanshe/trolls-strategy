using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Keeps the nested UIDocuments under this GameObject in sorting order. Unity files a nested document by its
    /// sorting order but inserts its root by how many siblings are already in the tree, so a document enabled
    /// before its root exists can land after a later sibling: after a scene load the catalog band came above the
    /// quest. Each group is checked every frame and put back in order when it is not; an ordered tree is left alone.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiDocumentOrder : MonoBehaviour
    {
        private readonly List<(UIDocument Parent, UIDocument[] Children)> _groups = new();
        private readonly List<VisualElement> _roots = new();

        private void OnEnable() => Collect();

        private void LateUpdate()
        {
            foreach (var (parent, children) in _groups)
            {
                var parentRoot = parent != null ? parent.rootVisualElement : null;
                if (parentRoot == null) continue;
                _roots.Clear();
                foreach (var child in children)
                    if (child != null) _roots.Add(child.rootVisualElement);
                Restore(parentRoot, _roots);
            }
        }

        /// <summary>
        /// Moves <paramref name="ordered"/> (the parent's nested document roots, in sorting order) after the
        /// parent's own elements in that order. Roots that are missing or not attached to the parent are skipped.
        /// False when they already stood in order and nothing moved.
        /// </summary>
        public static bool Restore(VisualElement parent, IReadOnlyList<VisualElement> ordered)
        {
            int last = -1;
            bool inOrder = true;
            foreach (var root in ordered)
            {
                if (root == null || root.parent != parent) continue;
                int index = parent.IndexOf(root);
                if (index < last)
                {
                    inOrder = false;
                    break;
                }
                last = index;
            }
            if (inOrder) return false;
            foreach (var root in ordered)
                if (root != null && root.parent == parent) root.BringToFront();
            return true;
        }

        // Every document with two or more nested documents, its children by sorting order, then as Unity files
        // equal orders: by their place in the hierarchy.
        private void Collect()
        {
            _groups.Clear();
            foreach (var parent in GetComponentsInChildren<UIDocument>(true))
            {
                var children = new List<UIDocument>();
                foreach (Transform child in parent.transform)
                    if (child.TryGetComponent(out UIDocument document)) children.Add(document);
                if (children.Count < 2) continue;
                children.Sort((a, b) => a.sortingOrder != b.sortingOrder
                    ? a.sortingOrder.CompareTo(b.sortingOrder)
                    : a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
                _groups.Add((parent, children.ToArray()));
            }
        }
    }
}
