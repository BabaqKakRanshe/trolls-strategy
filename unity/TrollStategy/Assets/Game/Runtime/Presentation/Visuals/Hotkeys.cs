using UnityEngine.InputSystem;

namespace TrollStrategy.Presentation.Visuals
{
    /// <summary>A key and the letter the HUD shows for it.</summary>
    public readonly struct Hotkey
    {
        public Hotkey(Key key, string label)
        {
            Key = key;
            Label = label;
        }

        public Key Key { get; }
        public string Label { get; }

        public bool WasPressed => Keyboard.current != null && Keyboard.current[Key].wasPressedThisFrame;
    }

    /// <summary>
    /// Every colony key in one table: the map input and the HUD read their keys here, and the hints show the same
    /// letters, so a key and its hint cannot drift apart.
    /// </summary>
    public static class Hotkeys
    {
        public static readonly Hotkey Idle = new(Key.Digit1, "1");
        public static readonly Hotkey Land = new(Key.L, "L");
        public static readonly Hotkey Grid = new(Key.G, "G");
        public static readonly Hotkey Catalog = new(Key.C, "C");
        public static readonly Hotkey Arena = new(Key.V, "V");
        public static readonly Hotkey Wiki = new(Key.K, "K");
        public static readonly Hotkey Menu = new(Key.Escape, "Esc");
        public static readonly Hotkey Quest = new(Key.Q, "Q");
        public static readonly Hotkey Work = new(Key.E, "E");
        public static readonly Hotkey Haul = new(Key.H, "H");
        public static readonly Hotkey Release = new(Key.R, "R");
        public static readonly Hotkey Mine = new(Key.B, "B");
        public static readonly Hotkey Confirm = new(Key.Enter, "Enter");
    }
}
