using System;
using System.Collections.Generic;
using TrollStrategy.Domain;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>What the tutorial pointer shows: a button of the HUD or a place in the world.</summary>
    public enum GuideTargetKind
    {
        None,
        /// <summary>A HUD element: a catalog token or tab, a tool, an order, a card's or a dialog's button.</summary>
        Element,
        /// <summary>A building on the colony map, by id.</summary>
        Building,
        /// <summary>A creature on the colony map, by id; it walks, so the pointer follows it.</summary>
        Unit,
        /// <summary>One cell of the colony map: where the next creature should stand.</summary>
        Cell,
        /// <summary>A building's place on the colony map: its lower-left cell and its size.</summary>
        Footprint,
        /// <summary>A cell of the battle board: the fighter standing there.</summary>
        BattleCell,
        /// <summary>Nothing to point at: the card stands alone (the controls lesson), its keys on it.</summary>
        Card
    }

    /// <summary>A key or a mouse gesture a tutorial card shows: a keycap with its letter or a mouse picture, and a name in a list.</summary>
    public readonly struct GuideKey
    {
        private GuideKey(string label, string glyph, string name)
        {
            Label = label;
            Glyph = glyph;
            Name = name;
        }

        /// <summary>The key's letter on its keycap; null for a mouse picture.</summary>
        public string Label { get; }
        /// <summary>The picture's glyph (glyph--&lt;name&gt; in Theme.uss); null for a key.</summary>
        public string Glyph { get; }
        /// <summary>What the key does, when the card lists keys; null beside a sentence.</summary>
        public string Name { get; }

        public static GuideKey Of(string label, string name = null) => new(label, null, name);
        public static GuideKey Mouse(string glyph) => new(null, glyph, null);
        public static readonly GuideKey RightButton = Mouse("mouse-right");
        public static readonly GuideKey Wheel = Mouse("mouse-wheel");

        public override string ToString() => Label ?? Glyph;
    }

    /// <summary>The one thing the next press of a tutorial step should hit.</summary>
    public readonly struct GuideTarget
    {
        private GuideTarget(GuideTargetKind kind, VisualElement element, string id, Cell cell, int width, int height,
            UnityEngine.Sprite art = null)
        {
            Kind = kind;
            Element = element;
            Id = id;
            Cell = cell;
            Width = width;
            Height = height;
            Art = art;
        }

        public GuideTargetKind Kind { get; }
        public VisualElement Element { get; }
        public string Id { get; }
        public Cell Cell { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>On a place on the colony map: the picture of what goes there, on the pin over it.</summary>
        public UnityEngine.Sprite Art { get; }
        /// <summary>A place on a map rather than a button of the HUD.</summary>
        public bool InWorld => Kind == GuideTargetKind.Building || Kind == GuideTargetKind.Unit ||
                               Kind == GuideTargetKind.Cell || Kind == GuideTargetKind.Footprint ||
                               Kind == GuideTargetKind.BattleCell;

        public static GuideTarget Of(VisualElement element) =>
            element != null ? new GuideTarget(GuideTargetKind.Element, element, null, default, 0, 0) : default;
        public static GuideTarget Building(string id) => new(GuideTargetKind.Building, null, id, default, 0, 0);
        public static GuideTarget Unit(string id) => new(GuideTargetKind.Unit, null, id, default, 0, 0);
        public static GuideTarget At(Cell cell, UnityEngine.Sprite art = null) =>
            new(GuideTargetKind.Cell, null, null, cell, 1, 1, art);
        public static GuideTarget Place(Cell cell, int width, int height, UnityEngine.Sprite art = null) =>
            new(GuideTargetKind.Footprint, null, null, cell, width, height, art);
        public static GuideTarget Fighter(Cell cell) => new(GuideTargetKind.BattleCell, null, null, cell, 1, 1);
        public static GuideTarget Card => new(GuideTargetKind.Card, null, null, default, 0, 0);

        public override string ToString() => Kind switch
        {
            GuideTargetKind.Element => "element " + Element?.name,
            GuideTargetKind.Building or GuideTargetKind.Unit => Kind + " " + Id,
            GuideTargetKind.None => "none",
            _ => Kind + " " + Cell
        };
    }

    /// <summary>
    /// One step of the tutorial pointer: the next press, its words for the card and its place among the steps of
    /// the order. Worked out again on every refresh from the quest, the interaction and what the screen shows; it
    /// owns nothing of the colony.
    /// </summary>
    public sealed class GuideStep
    {
        public static readonly GuideStep None = new(default, null, null, 0, 0, false, null);

        public GuideStep(GuideTarget target, string title, string text, int number, int count, bool veil, string key,
            IReadOnlyList<GuideKey> keys = null)
        {
            Target = target;
            Title = title;
            Text = text;
            Number = number;
            Count = count;
            Veil = veil;
            Key = key;
            Keys = keys ?? Array.Empty<GuideKey>();
        }

        public GuideTarget Target { get; }
        /// <summary>The action, bold on the card.</summary>
        public string Title { get; }
        /// <summary>One line: how, or what it is for.</summary>
        public string Text { get; }
        /// <summary>The step's place in its order, from 1; with <see cref="Count"/> of 1 or less the card names none.</summary>
        public int Number { get; }
        public int Count { get; }
        /// <summary>Whether the light veil covers the rest; a dialog with a veil of its own gets no second one.</summary>
        public bool Veil { get; }
        /// <summary>Tells one step from the next: a new key brings the veil back and lets the camera come once more.</summary>
        public string Key { get; }
        /// <summary>The keys and mouse gestures of the step, as keycaps and pictures under the card's line.</summary>
        public IReadOnlyList<GuideKey> Keys { get; }
        public bool IsShown => Target.Kind != GuideTargetKind.None;

        public override string ToString() => IsShown ? $"{Number}/{Count} {Title} → {Target}" : "no step";
    }
}
