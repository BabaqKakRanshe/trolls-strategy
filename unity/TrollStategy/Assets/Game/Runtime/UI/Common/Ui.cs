using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>Element factories and toggles the HUD screens share. The look lives in USS classes.</summary>
    public static class Ui
    {
        public static VisualElement Box(string classes = null)
        {
            var element = new VisualElement();
            AddClasses(element, classes);
            return element;
        }

        public static Label Text(string text, string classes = null)
        {
            var label = new Label(text);
            AddClasses(label, classes);
            return label;
        }

        public static Button TextButton(string text, string classes = null)
        {
            var button = new Button { text = text };
            AddClasses(button, classes);
            return button;
        }

        /// <summary>
        /// A button whose caption is a child label, with an optional hotkey badge in its corner. A text
        /// element that also holds children stops measuring its own text, so the caption must be a child.
        /// </summary>
        public static Button CaptionButton(string text, string hotkey, string classes)
        {
            var button = TextButton(string.Empty, classes);
            var caption = Text(text, "btn__caption");
            caption.pickingMode = PickingMode.Ignore;
            button.Add(caption);
            if (!string.IsNullOrEmpty(hotkey))
            {
                var badge = Text(hotkey, "hotkey");
                badge.pickingMode = PickingMode.Ignore;
                button.Add(badge);
            }
            return button;
        }

        /// <summary>Sets a caption button's caption, or a plain button's text.</summary>
        public static void SetCaption(Button button, string text)
        {
            var caption = button?.Q<Label>(className: "btn__caption");
            if (caption != null) SetText(caption, text);
            else SetText(button, text);
        }

        /// <summary>A two-line button: what it does, then its price or hint.</summary>
        public static Button StackButton(string classes, out Label title, out Label hint)
        {
            var button = TextButton(string.Empty, "btn btn-stack " + classes);
            title = Text(string.Empty, "btn__title");
            hint = Text(string.Empty, "btn__hint");
            title.pickingMode = PickingMode.Ignore;
            hint.pickingMode = PickingMode.Ignore;
            button.Add(title);
            button.Add(hint);
            return button;
        }

        /// <summary>A picture box: the sprite, or the name's first letter when there is no art yet.</summary>
        public static VisualElement Art(Sprite sprite, string name, string classes)
        {
            var box = Box(classes);
            box.pickingMode = PickingMode.Ignore;
            if (sprite != null)
            {
                var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                image.AddToClassList("card__image");
                box.Add(image);
            }
            else
            {
                var letter = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
                box.Add(Text(letter, "card__monogram"));
            }
            return box;
        }

        public static void Show(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static bool IsShown(VisualElement element) =>
            element != null && element.style.display.value != DisplayStyle.None;

        public static void SetText(TextElement element, string text)
        {
            text ??= string.Empty;
            if (element != null && element.text != text) element.text = text;
        }

        public static string Gold(int amount) => amount + " зол.";

        /// <summary>A named element the layout must contain; a renamed element fails loudly, not silently.</summary>
        public static T Require<T>(VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException($"HUD layout has no {typeof(T).Name} '{name}'");

        private static void AddClasses(VisualElement element, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                element.AddToClassList(name);
        }
    }
}
