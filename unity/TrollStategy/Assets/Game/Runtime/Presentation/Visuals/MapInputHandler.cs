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
        /// <summary>Esc with nothing to cancel: the HUD opens its menu.</summary>
        public event Action MenuRequested;
        /// <summary>An open dialog takes Esc; true when it did.</summary>
        public Func<bool> EscapeOverlay { get; set; }
        /// <summary>A dialog holds the screen: the map's keys and clicks wait.</summary>
        public Func<bool> InputBlocked { get; set; }

        public void Init(InteractionController interaction, SelectionBoxRenderer selectionBox)
        {
            _interaction = interaction;
            _selectionBox = selectionBox;
        }

        private void Update()
        {
            if (_interaction == null) return;

            if (Hotkeys.Menu.WasPressed)
            {
                if (EscapeOverlay?.Invoke() == true) return;
                if (!Cancel()) MenuRequested?.Invoke();
                return;
            }
            if (InputBlocked?.Invoke() == true) return;

            if (Keyboard.current != null)
            {

                // Enter settles the haul cargo; where to carry it is picked on the map next
                if ((Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame) &&
                    _interaction.Mode.Type == InteractionModeType.ChoosingHaulCargo)
                {
                    if (_interaction.HaulCargoHasDestination)
                        TrollStrategy.Presentation.Audio.GameAudio.Play(TrollStrategy.Presentation.Audio.Sfx.UiClick);
                    _interaction.ConfirmHaulCargo();
                }

                if (Hotkeys.Idle.WasPressed)
                    _interaction.SelectNextIdle();

                if (Hotkeys.Mine.WasPressed)
                    _interaction.BeginBuildingPlacement(TrollStrategy.Content.BuildingKind.Mine);

                // W, A, S, D move the camera (IslandCameraRig)
                if (Hotkeys.Work.WasPressed)
                    _interaction.BeginWorkTarget();

                if (Hotkeys.Haul.WasPressed)
                    _interaction.BeginHaulTarget();

                if (Hotkeys.Release.WasPressed)
                    _interaction.ReleaseSelected();

                if (Hotkeys.Land.WasPressed)
                    _interaction.ToggleLandMode();

                if (Hotkeys.Grid.WasPressed)
                {
                    var visualizer = UnityEngine.Object.FindAnyObjectByType<HaulRouteVisualizer>();
                    if (visualizer != null) visualizer.ToggleGuides();
                }
            }

            // a finger held still on the map for a moment does what the right button does
            if (MapPointer.UsesTouch && MapPointer.Fingers == 1 && !MapPointer.StartedOverUi &&
                MapPointer.HeldSeconds >= LongPressSeconds)
            {
                MapPointer.ConsumeGesture();
                SecondaryClick(MapPointer.Position);
                return;
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
                SecondaryClick(mouse.position.ReadValue());
            }
        }

        private const float LongPressSeconds = .55f;

        // right click or long press: the order fan over the selected creatures, otherwise a cancel
        private void SecondaryClick(Vector2 pointer)
        {
            if (_interaction.Mode.Type == InteractionModeType.Neutral && _interaction.SelectedIds.Count > 0)
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

        /// <summary>Esc or right click: drops the current mode or selection, audibly when there was one.</summary>
        private bool Cancel()
        {
            bool something = _interaction.Mode.Type != InteractionModeType.Neutral || _interaction.SelectedIds.Count > 0;
            _interaction.CancelOrClear();
            if (something) TrollStrategy.Presentation.Audio.GameAudio.Play(TrollStrategy.Presentation.Audio.Sfx.UiBack);
            return something;
        }
    }
}
