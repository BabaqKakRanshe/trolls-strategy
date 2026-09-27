using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace TrollStrategy.Presentation.Visuals
{
    /// <summary>
    /// Whether the pointer is over HUD rather than the world, so map clicks under a panel are ignored.
    /// UI Toolkit documents register themselves and are asked with Pick: layout containers are
    /// picking-mode Ignore, so only real panels and buttons count. uGUI canvases are still checked
    /// through the EventSystem.
    /// </summary>
    public static class UIInputUtils
    {
        private static readonly List<UIDocument> s_documents = new();
        private static readonly List<RaycastResult> s_results = new();

        public static void RegisterDocument(UIDocument document)
        {
            if (document != null && !s_documents.Contains(document)) s_documents.Add(document);
        }

        public static void UnregisterDocument(UIDocument document) => s_documents.Remove(document);

        public static bool IsPointerOverInteractiveUI()
        {
            Vector2 screenPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            return IsOverDocument(screenPosition) || IsOverCanvas(screenPosition);
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

        private static bool IsOverCanvas(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;
            var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            s_results.Clear();
            EventSystem.current.RaycastAll(pointerData, s_results);
            for (int i = 0; i < s_results.Count; i++)
                if (s_results[i].module is GraphicRaycaster)
                    return true;
            return false;
        }
    }
}
