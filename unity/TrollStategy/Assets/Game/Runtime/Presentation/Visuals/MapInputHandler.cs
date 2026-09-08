using UnityEngine;
using UnityEngine.InputSystem;
using TrollStrategy.Application;

namespace TrollStrategy.Presentation.Visuals
{
    public class MapInputHandler : MonoBehaviour
    {
        private InteractionController _interaction;

        public void Init(InteractionController interaction)
        {
            _interaction = interaction;
        }

        private void Update()
        {
            if (_interaction == null) return;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    _interaction.CancelOrClear();

                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    _interaction.SelectNextIdle();

                if (Keyboard.current.bKey.wasPressedThisFrame)
                    _interaction.BeginMinePlacement();

                if (Keyboard.current.wKey.wasPressedThisFrame)
                    _interaction.BeginWorkTarget();

                if (Keyboard.current.hKey.wasPressedThisFrame)
                    _interaction.BeginHaulTarget();

                if (Keyboard.current.rKey.wasPressedThisFrame)
                    _interaction.ReleaseSelected();

                if (Keyboard.current.gKey.wasPressedThisFrame)
                {
                    var visualizer = Object.FindAnyObjectByType<HaulRouteVisualizer>();
                    if (visualizer != null) visualizer.ToggleGuides();
                }
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                _interaction.CancelOrClear();
            }
        }
    }
}
