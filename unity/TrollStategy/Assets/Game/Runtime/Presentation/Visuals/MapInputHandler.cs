using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TrollStrategy.Application;

namespace TrollStrategy.Presentation.Visuals
{
    public class MapInputHandler : MonoBehaviour
    {
        private InteractionController _interaction;
        private SelectionBoxRenderer _selectionBox;
        public event Action<Vector2> CommandFanRequested;

        public void Init(InteractionController interaction, SelectionBoxRenderer selectionBox)
        {
            _interaction = interaction;
            _selectionBox = selectionBox;
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
                    var visualizer = UnityEngine.Object.FindAnyObjectByType<HaulRouteVisualizer>();
                    if (visualizer != null) visualizer.ToggleGuides();
                }
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (UIInputUtils.IsPointerOverUI()) return;

                var pointer = Mouse.current.position.ReadValue();
                if (_interaction.Mode.Type == InteractionModeType.Neutral &&
                    _interaction.SelectedIds.Count > 0)
                {
                    if (_selectionBox == null || _selectionBox.IsBuildingAt(pointer)) return;
                    _interaction.ToggleCommands(true);
                    CommandFanRequested?.Invoke(pointer);
                }
                else
                {
                    _interaction.CancelOrClear();
                }
            }
        }
    }
}
