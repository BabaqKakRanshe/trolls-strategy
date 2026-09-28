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
