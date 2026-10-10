using System;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Visuals;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The tutorial's first lesson, before the first worker: move the camera (W A S D, the arrows, a drag with the
    /// right button), zoom it (the wheel), then the tool column's keys. A part is done when the player has done it
    /// (the camera rig counts only the player's own moves); the keys' card also goes by itself after a while. Learned
    /// once per player (<see cref="GameSettings.ControlsLearned"/>); a touch screen has none of these keys and skips
    /// it. Presentation only: the colony knows nothing of it.
    /// </summary>
    public sealed class ControlsLesson
    {
        public enum Stage
        {
            Move,
            Zoom,
            Keys,
            Done
        }

        /// <summary>The camera moved this far, in sides of the land it frames.</summary>
        public const float MoveGoal = .6f;
        /// <summary>The camera zoomed this much in all, as the sum of |ln| of its steps (about four wheel notches).</summary>
        public const float ZoomGoal = .4f;
        /// <summary>The keys' card goes by itself after this long on the screen.</summary>
        public const float KeysSeconds = 12f;

        /// <summary>The tool column's keys, as the keys' card lists them.</summary>
        public static readonly (Hotkey Key, string Name)[] ToolKeys =
        {
            (Hotkeys.Catalog, "Каталог"),
            (Hotkeys.Arena, "Арена"),
            (Hotkeys.Land, "Земля"),
            (Hotkeys.Grid, "Сетка"),
            (Hotkeys.Wiki, "Справочник"),
            (Hotkeys.Quest, "Задание"),
            (Hotkeys.Idle, "Свободный"),
            (Hotkeys.Menu, "Меню")
        };

        private readonly Func<float> _panned;
        private readonly Func<float> _zoomed;
        private readonly Func<bool> _touch;
        private float _from = float.NaN;
        private float _keysShown;

        /// <param name="panned">How far the player has moved the camera (the rig's count); null: no camera, no lesson.</param>
        /// <param name="zoomed">How much the player has zoomed (the rig's count).</param>
        /// <param name="learned">The player has had the lesson already.</param>
        /// <param name="touch">Whether the game is played by touch now; the lesson then ends unlearned.</param>
        public ControlsLesson(Func<float> panned, Func<float> zoomed, bool learned, Func<bool> touch = null)
        {
            _panned = panned;
            _zoomed = zoomed;
            _touch = touch;
            Current = learned || panned == null || zoomed == null ? Stage.Done : Stage.Move;
        }

        public Stage Current { get; private set; }
        public bool IsDone => Current == Stage.Done;

        /// <summary>Every frame; <paramref name="shown"/> is whether the lesson's card is up (a dialog may hold the screen).</summary>
        public void Tick(float deltaTime, bool shown)
        {
            if (IsDone) return;
            if (_touch?.Invoke() == true)
            {
                Current = Stage.Done;
                return;
            }
            if (!shown) return;
            switch (Current)
            {
                case Stage.Move:
                    if (Progress(_panned()) >= MoveGoal) Next();
                    break;
                case Stage.Zoom:
                    if (Progress(_zoomed()) >= ZoomGoal) Next();
                    break;
                case Stage.Keys:
                    _keysShown += deltaTime;
                    bool pressed = false;
                    foreach (var (key, _) in ToolKeys) pressed |= key.WasPressed;
                    if (pressed || _keysShown >= KeysSeconds) Next();
                    break;
            }
        }

        /// <summary>A tool key was pressed while the keys' card is up.</summary>
        public void NoteKey()
        {
            if (Current == Stage.Keys) Next();
        }

        // how far the current part has gone since its card came up
        private float Progress(float total)
        {
            if (float.IsNaN(_from)) _from = total;
            return total - _from;
        }

        private void Next()
        {
            Current++;
            _from = float.NaN;
            if (Current == Stage.Done) GameSettings.SetControlsLearned(true);
        }
    }
}
