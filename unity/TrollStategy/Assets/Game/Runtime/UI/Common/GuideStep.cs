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
        BattleCell
    }

    /// <summary>The one thing the next press of a tutorial step should hit.</summary>
    public readonly struct GuideTarget
    {
        private GuideTarget(GuideTargetKind kind, VisualElement element, string id, Cell cell, int width, int height)
        {
            Kind = kind;
            Element = element;
            Id = id;
            Cell = cell;
            Width = width;
            Height = height;
        }

        public GuideTargetKind Kind { get; }
        public VisualElement Element { get; }
        public string Id { get; }
        public Cell Cell { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>A place on a map rather than a button of the HUD.</summary>
        public bool InWorld => Kind == GuideTargetKind.Building || Kind == GuideTargetKind.Unit ||
                               Kind == GuideTargetKind.Cell || Kind == GuideTargetKind.Footprint ||
                               Kind == GuideTargetKind.BattleCell;

        public static GuideTarget Of(VisualElement element) =>
            element != null ? new GuideTarget(GuideTargetKind.Element, element, null, default, 0, 0) : default;
        public static GuideTarget Building(string id) => new(GuideTargetKind.Building, null, id, default, 0, 0);
        public static GuideTarget Unit(string id) => new(GuideTargetKind.Unit, null, id, default, 0, 0);
        public static GuideTarget At(Cell cell) => new(GuideTargetKind.Cell, null, null, cell, 1, 1);
        public static GuideTarget Place(Cell cell, int width, int height) =>
            new(GuideTargetKind.Footprint, null, null, cell, width, height);
        public static GuideTarget Fighter(Cell cell) => new(GuideTargetKind.BattleCell, null, null, cell, 1, 1);

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

        public GuideStep(GuideTarget target, string title, string text, int number, int count, bool veil, string key)
        {
            Target = target;
            Title = title;
            Text = text;
            Number = number;
            Count = count;
            Veil = veil;
            Key = key;
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
        public bool IsShown => Target.Kind != GuideTargetKind.None;

        public override string ToString() => IsShown ? $"{Number}/{Count} {Title} → {Target}" : "no step";
    }
}
