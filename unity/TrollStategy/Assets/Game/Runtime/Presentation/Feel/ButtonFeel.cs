using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrollStrategy.Presentation.Feel
{
    /// <summary>
    /// Acknowledges every press within a frame: hover lift, press dip and release spring with a click, and a
    /// "no" wobble with the refusal sound when the button is not available. Only scale is animated, so
    /// layout code that moves or resizes buttons keeps working.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private Sfx _clickSound = Sfx.UiClick;
        private Selectable _selectable;
        private bool _hovered;

        /// <summary>Adds the feedback to a button (idempotent) and returns it.</summary>
        public static ButtonFeel Attach(Component button, Sfx click = Sfx.UiClick)
        {
            if (button == null) return null;
            if (!button.TryGetComponent<ButtonFeel>(out var feel)) feel = button.gameObject.AddComponent<ButtonFeel>();
            feel._clickSound = click;
            return feel;
        }

        /// <summary>Adds the feedback to every Selectable under <paramref name="root"/>, inactive ones included.</summary>
        public static void AttachAll(Transform root)
        {
            if (root == null) return;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                string name = button.name;
                bool back = name.Contains("Cancel") || name.Contains("Close") || name.Contains("Back");
                Attach(button, back ? Sfx.UiBack : Sfx.UiClick);
            }
        }

        public Sfx ClickSound
        {
            get => _clickSound;
            set => _clickSound = value;
        }

        /// <summary>The click's own handler plays a more specific sound (equip, select, pause…).</summary>
        public bool SilentClick { get; set; }

        private bool Available => _selectable == null || _selectable.IsInteractable();

        private void Awake() => _selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (!Available) return;
            Juice.Hover(transform, 1.04f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Juice.Hover(transform, 1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !Available) return;
            Juice.Hover(transform, .94f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Juice.Hover(transform, _hovered && Available ? 1.04f : 1f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (Available)
            {
                Juice.Punch(transform, .1f, .25f);
                if (!SilentClick) GameAudio.Play(_clickSound);
            }
            else
            {
                // horizontal squash wobble, not a move: some buttons are positioned by layout code every refresh
                Juice.Squash(transform, -.08f, .3f);
                GameAudio.Play(Sfx.UiDenied);
            }
        }

        private void OnDisable()
        {
            _hovered = false;
        }
    }
}
