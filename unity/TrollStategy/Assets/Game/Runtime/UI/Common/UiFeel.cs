using System;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Wires a HUD button to its action and answers every press: a hover tick, a click sound and, for a
    /// button that is not available right now, a "no" wobble with the refusal sound instead of silence.
    /// The hover lift and press dip are USS transitions on <c>.btn</c>.
    /// </summary>
    public static class UiFeel
    {
        public const string UnavailableClass = "is-unavailable";

        private sealed class Binding
        {
            public Action Action;
            public Sfx Click;
            public bool Silent;
            public bool Available = true;
        }

        /// <summary>
        /// Binds <paramref name="action"/> to the button; binding again replaces the action. A silent
        /// click leaves the sound to the command's own outcome (hire, equip, select…).
        /// </summary>
        public static Button Bind(Button button, Action action, Sfx click = Sfx.UiClick, bool silentClick = false)
        {
            if (button == null) return null;
            if (button.userData is Binding existing)
            {
                existing.Action = action;
                existing.Click = click;
                existing.Silent = silentClick;
                return button;
            }

            var binding = new Binding { Action = action, Click = click, Silent = silentClick };
            button.userData = binding;
            // HUD buttons never take keyboard focus: Space or Enter must not repeat the last purchase
            button.focusable = false;
            // the default theme's button look would fight .btn
            button.RemoveFromClassList(Button.ussClassName);
            button.clicked += () => Press(button);
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (binding.Available) GameAudio.Play(Sfx.UiHover, 1f, 1.1f);
            });
            return button;
        }

        public static void SetAvailable(Button button, bool available)
        {
            if (button == null) return;
            if (button.userData is Binding binding) binding.Available = available;
            button.EnableInClassList(UnavailableClass, !available);
        }

        public static bool IsAvailable(Button button) => !(button?.userData is Binding binding) || binding.Available;

        /// <summary>What a left click does. Tests call it directly, since they run without a panel.</summary>
        public static void Press(Button button)
        {
            if (button == null || !(button.userData is Binding binding)) return;
            if (!binding.Available)
            {
                UiMotion.Nudge(button, 6f);
                GameAudio.Play(Sfx.UiDenied);
                return;
            }
            if (!binding.Silent) GameAudio.Play(binding.Click);
            binding.Action?.Invoke();
        }
    }
}
