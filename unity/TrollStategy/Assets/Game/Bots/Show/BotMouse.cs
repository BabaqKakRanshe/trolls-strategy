using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// The show bot's own mouse and keyboard: Input System devices fed with states, so the game reads them exactly as
    /// it reads a player's (<c>Mouse.current</c>, the event system, the map's clicks and keys). While the bot plays,
    /// the real mouse and keyboard are switched off for the game (the editor keeps them), and input reaches the game
    /// view even when the editor is not focused. <see cref="Dispose"/> puts everything back; a domain reload in the
    /// middle is caught by <see cref="RestoreAfterReload"/>.
    /// Positions are screen pixels from the bottom left, as Input System counts.
    /// </summary>
    public sealed class BotMouse : System.IDisposable
    {
        private const string Prefix = "TrollStrategy.BotMouse.";

        private readonly Mouse _mouse;
        private readonly Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new();
        private Vector2 _position;
        private bool _left, _right, _ctrl;
        private readonly HashSet<Key> _keys = new();

        public BotMouse(Vector2 start)
        {
            var settings = InputSystem.settings;
            SessionState.SetInt(Prefix + "Background", (int)settings.backgroundBehavior);
            SessionState.SetInt(Prefix + "Editor", (int)settings.editorInputBehaviorInPlayMode);
            SessionState.SetBool(Prefix + "Changed", true);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            foreach (var device in InputSystem.devices)
            {
                if (!(device is Mouse || device is Keyboard || device is Touchscreen || device is Pen) || !device.enabled)
                    continue;
                InputSystem.DisableDevice(device);
                _disabled.Add(device);
            }
            _mouse = InputSystem.AddDevice<Mouse>("BotMouse");
            _keyboard = InputSystem.AddDevice<Keyboard>("BotKeyboard");
            _position = start;
            Send(Vector2.zero);
            _mouse.MakeCurrent();
            _keyboard.MakeCurrent();
        }

        public Vector2 Position => _position;
        public bool LeftDown => _left;

        /// <summary>Puts the pointer at <paramref name="position"/>; the game sees it from the next frame.</summary>
        public void MoveTo(Vector2 position)
        {
            var delta = position - _position;
            _position = position;
            Send(delta);
        }

        public void Left(bool down)
        {
            _left = down;
            Send(Vector2.zero);
        }

        public void Right(bool down)
        {
            _right = down;
            Send(Vector2.zero);
        }

        /// <summary>Turns the wheel by <paramref name="notches"/> (positive scrolls up); one frame of scroll.</summary>
        public void Wheel(float notches)
        {
            var state = new MouseState { position = _position, scroll = new Vector2(0f, notches * 120f) };
            state = state.WithButton(MouseButton.Left, _left).WithButton(MouseButton.Right, _right);
            InputSystem.QueueStateEvent(_mouse, state);
        }

        /// <summary>Holds Ctrl (the map adds creatures to the selection with it).</summary>
        public void Ctrl(bool down)
        {
            _ctrl = down;
            SendKeys();
        }

        public void KeyDown(Key key)
        {
            _keys.Add(key);
            SendKeys();
        }

        public void KeyUp(Key key)
        {
            _keys.Remove(key);
            SendKeys();
        }

        /// <summary>Makes the bot's devices current again, should anything else have taken over.</summary>
        public void KeepCurrent()
        {
            if (Mouse.current != _mouse) _mouse.MakeCurrent();
            if (Keyboard.current != _keyboard) _keyboard.MakeCurrent();
        }

        public void Dispose()
        {
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            foreach (var device in _disabled)
                if (device.added) InputSystem.EnableDevice(device);
            _disabled.Clear();
            RestoreSettings();
        }

        /// <summary>After a domain reload the devices are gone with the old domain; the settings come back here.</summary>
        public static void RestoreAfterReload()
        {
            if (!SessionState.GetBool(Prefix + "Changed", false)) return;
            foreach (var device in InputSystem.devices)
            {
                if (device.name.StartsWith("Bot")) InputSystem.RemoveDevice(device);
                else if (!device.enabled && (device is Mouse || device is Keyboard)) InputSystem.EnableDevice(device);
            }
            RestoreSettings();
        }

        private static void RestoreSettings()
        {
            if (!SessionState.GetBool(Prefix + "Changed", false)) return;
            var settings = InputSystem.settings;
            settings.backgroundBehavior = (InputSettings.BackgroundBehavior)SessionState.GetInt(Prefix + "Background",
                (int)InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices);
            settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(
                Prefix + "Editor", (int)InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus);
            SessionState.EraseBool(Prefix + "Changed");
            SessionState.EraseInt(Prefix + "Background");
            SessionState.EraseInt(Prefix + "Editor");
        }

        private void Send(Vector2 delta)
        {
            var state = new MouseState { position = _position, delta = delta };
            state = state.WithButton(MouseButton.Left, _left).WithButton(MouseButton.Right, _right);
            InputSystem.QueueStateEvent(_mouse, state);
        }

        private void SendKeys()
        {
            var keys = new List<Key>(_keys);
            if (_ctrl) keys.Add(UnityEngine.InputSystem.Key.LeftCtrl);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys.ToArray()));
        }
    }
}
