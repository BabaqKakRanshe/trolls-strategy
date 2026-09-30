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
        private bool _rightClick;
        private Vector2 _rightPress;
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
                    Cancel();

                // Enter settles the haul cargo; where to carry it is picked on the map next
                if ((Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame) &&
                    _interaction.Mode.Type == InteractionModeType.ChoosingHaulCargo)
                {
                    if (_interaction.HaulCargoHasDestination)
                        TrollStrategy.Presentation.Audio.GameAudio.Play(TrollStrategy.Presentation.Audio.Sfx.UiClick);
                    _interaction.ConfirmHaulCargo();
                }

                if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    _interaction.SelectNextIdle();

                if (Keyboard.current.bKey.wasPressedThisFrame)
                    _interaction.BeginBuildingPlacement(TrollStrategy.Content.BuildingKind.Mine);

                // W, A, S, D move the camera (IslandCameraRig)
                if (Keyboard.current.eKey.wasPressedThisFrame)
                    _interaction.BeginWorkTarget();

                if (Keyboard.current.hKey.wasPressedThisFrame)
                    _interaction.BeginHaulTarget();

                if (Keyboard.current.rKey.wasPressedThisFrame)
                    _interaction.ReleaseSelected();

                if (Keyboard.current.lKey.wasPressedThisFrame)
                    _interaction.ToggleLandMode();

                if (Keyboard.current.gKey.wasPressedThisFrame)
                {
                    var visualizer = UnityEngine.Object.FindAnyObjectByType<HaulRouteVisualizer>();
                    if (visualizer != null) visualizer.ToggleGuides();
                }
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _rightClick = !UIInputUtils.IsPointerOverUI();
                _rightPress = mouse.position.ReadValue();
            }
            // a right press that moved is the camera grabbing the ground (IslandCameraRig), not a click
            if (_rightClick && mouse.rightButton.isPressed &&
                UIInputUtils.IsDrag(_rightPress, mouse.position.ReadValue()))
                _rightClick = false;
            if (mouse.rightButton.wasReleasedThisFrame && _rightClick)
            {
                _rightClick = false;
                var pointer = mouse.position.ReadValue();
                if (_interaction.Mode.Type == InteractionModeType.Neutral &&
                    _interaction.SelectedIds.Count > 0)
                {
                    if (_selectionBox == null || _selectionBox.IsBuildingAt(pointer)) return;
                    _interaction.ToggleCommands(true);
                    CommandFanRequested?.Invoke(pointer);
                    TrollStrategy.Presentation.Audio.GameAudio.Play(TrollStrategy.Presentation.Audio.Sfx.UiClick);
                }
                else
                {
                    Cancel();
                }
            }
        }

        /// <summary>Esc or right click: drops the current mode or selection, audibly when there was one.</summary>
        private void Cancel()
        {
            bool something = _interaction.Mode.Type != InteractionModeType.Neutral || _interaction.SelectedIds.Count > 0;
            _interaction.CancelOrClear();
            if (something) TrollStrategy.Presentation.Audio.GameAudio.Play(TrollStrategy.Presentation.Audio.Sfx.UiBack);
        }
    }
}
