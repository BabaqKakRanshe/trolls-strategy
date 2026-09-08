using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TrollStrategy.Presentation.Visuals
{
    public static class UIInputUtils
    {
        public static bool IsPointerOverInteractiveUI()
        {
            if (EventSystem.current == null) return false;

            Vector2 screenPos = Mouse.current != null 
                ? Mouse.current.position.ReadValue() 
                : Vector2.zero;

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPos
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].module is GraphicRaycaster)
                    return true;
                if (results[i].gameObject != null && results[i].gameObject.GetComponentInParent<Selectable>() != null)
                    return true;
            }

            return false;
        }

        public static bool IsPointerOverUI()
        {
            return IsPointerOverInteractiveUI();
        }
    }
}
