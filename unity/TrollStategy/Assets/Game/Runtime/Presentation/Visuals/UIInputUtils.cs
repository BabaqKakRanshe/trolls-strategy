using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TrollStrategy.Presentation.Visuals
{
    /// <summary>
    /// Whether the pointer is over the screen UI rather than the world, so map and board clicks under a
    /// panel are ignored. The HUD documents register themselves and are asked with Pick: document roots
    /// and layout containers are picking-mode Ignore, so only real panels and buttons count.
    /// </summary>
    public static class UIInputUtils
    {
        /// <summary>A press that moves farther than this before release is a drag, not a click, px.</summary>
        public const float DragThresholdPixels = 7f;

        private static readonly List<UIDocument> s_documents = new();

        public static bool IsDrag(Vector2 pressed, Vector2 pointer) =>
            Vector2.Distance(pressed, pointer) >= DragThresholdPixels;

        public static void RegisterDocument(UIDocument document)
        {
            if (document != null && !s_documents.Contains(document)) s_documents.Add(document);
        }

        public static void UnregisterDocument(UIDocument document) => s_documents.Remove(document);

        public static bool IsPointerOverInteractiveUI()
        {
            Vector2 screenPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            return IsOverDocument(screenPosition);
        }

        public static bool IsPointerOverUI() => IsPointerOverInteractiveUI();

        /// <summary>True when a UI Toolkit element that takes the pointer lies under the screen point.</summary>
        public static bool IsOverDocument(Vector2 screenPosition)
        {
            for (int i = s_documents.Count - 1; i >= 0; i--)
            {
                var document = s_documents[i];
                if (document == null)
                {
                    s_documents.RemoveAt(i);
                    continue;
                }
                if (!document.isActiveAndEnabled) continue;
                var root = document.rootVisualElement;
                var panel = root?.panel;
                if (panel == null) continue;
                // Input System screen space starts at the bottom left, the panel's at the top left
                var point = RuntimePanelUtils.ScreenToPanel(panel,
                    new Vector2(screenPosition.x, Screen.height - screenPosition.y));
                var picked = panel.Pick(point);
                if (picked != null && picked != root && picked != panel.visualTree) return true;
            }
            return false;
        }
    }
}
