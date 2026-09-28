using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// USS classes for the root element of this GameObject's UIDocument. A nested document's root is the
    /// element its parent lays out, so bands and columns without UXML, and layers that cover the screen,
    /// take their layout from these classes.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiDocumentClasses : MonoBehaviour
    {
        [SerializeField] private string[] _classes = Array.Empty<string>();

        private readonly List<string> _applied = new();
        private VisualElement _appliedTo;

        public IReadOnlyList<string> Classes => _classes;

        public void SetClasses(params string[] classes)
        {
            _classes = classes ?? Array.Empty<string>();
            Apply();
        }

        /// <summary>Adds the classes to an element standing in for the document root, as tests do.</summary>
        public void ApplyTo(VisualElement root)
        {
            foreach (var name in _classes)
                if (!string.IsNullOrWhiteSpace(name)) root.AddToClassList(name.Trim());
        }

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (_appliedTo != null)
                foreach (var name in _applied) _appliedTo.RemoveFromClassList(name);
            _applied.Clear();
            _appliedTo = root;
            if (root == null) return;
            ApplyTo(root);
            foreach (var name in _classes)
                if (!string.IsNullOrWhiteSpace(name)) _applied.Add(name.Trim());
        }
    }
}
