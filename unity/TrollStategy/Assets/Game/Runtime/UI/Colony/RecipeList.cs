using System.Collections.Generic;
using TrollStrategy.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// A building's recipes on its card, in pictures and numbers: what goes in, what comes out, by-products with
    /// their chances, and the level a recipe still waits for, greyed with its number. The open ones come first.
    /// One-for-one changes around a good every recipe takes alike (the enchanter: any piece of gear and a crystal)
    /// show that good once and the changes as a grid of pairs. Spoilage all recipes share is said once under
    /// them; the names are in the hint.
    /// </summary>
    public sealed class RecipeList
    {
        // the pairs go in rows of three: a wrapping row inside the card's scroll measured its height short
        private const int PairsPerRow = 3;

        private readonly VisualElement _root;
        private readonly ColonyHudContext _context;
        private readonly HudTooltip _tooltip;
        private BuildingKind _kind;
        // 0 while nothing is shown; buildings start at level 1
        private int _level;

        public RecipeList(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)
        {
            _root = root;
            _context = context;
            _tooltip = tooltip;
            Hide();
        }

        public void Show(BuildingDefinition definition, int level)
        {
            if (definition == null || definition.Recipes.Count == 0)
            {
                Hide();
                return;
            }
            Ui.Show(_root, true);
            // the card refreshes with every snapshot; the list changes only with the building or its level
            if (definition.Kind == _kind && level == _level) return;
            _kind = definition.Kind;
            _level = level;
            Build(definition.Recipes, level);
        }

        public void Hide()
        {
            _level = 0;
            if (_root.childCount > 0) _root.Clear();
            Ui.Show(_root, false);
        }

        private void Build(IReadOnlyList<ProductionRecipe> recipes, int level)
        {
            _root.Clear();
            // the building's priority order decides what runs; the card reads better with the open recipes first
            var ordered = new List<ProductionRecipe>(recipes);
            for (int i = 1; i < ordered.Count; i++)
                for (int j = i; j > 0 && ordered[j - 1].MinLevel > ordered[j].MinLevel; j--)
                    (ordered[j - 1], ordered[j]) = (ordered[j], ordered[j - 1]);

            bool sharedSpoilage = SharesSpoilage(ordered);
            if (IsConversionList(ordered, out var shared))
            {
                var header = Ui.Box("recipes__shared");
                header.Add(Ui.Text("Каждый предмет +", "recipes__words t-bold"));
                header.Add(Icon(shared.Resource, "recipe__icon"));
                header.Add(Ui.Text(shared.Amount.ToString(), "recipe__count t-black"));
                _root.Add(header);
                var grid = Ui.Box("recipe-pairs");
                VisualElement row = null;
                foreach (var recipe in ordered)
                {
                    if (row == null || row.childCount == PairsPerRow)
                    {
                        row = Ui.Box("recipe-pairs__row");
                        grid.Add(row);
                    }
                    row.Add(Pair(recipe, shared.Resource, level));
                }
                // the last row keeps the columns of the others
                while (row.childCount < PairsPerRow) row.Add(Ui.Box("recipe-pairs__filler"));
                _root.Add(grid);
            }
            else
            {
                foreach (var recipe in ordered) _root.Add(Row(recipe, level, !sharedSpoilage));
            }
            if (sharedSpoilage) _root.Add(Spoilage(ordered[0]));
        }

        private VisualElement Row(ProductionRecipe recipe, int level, bool saySpoilage)
        {
            var row = Ui.Box("recipe");
            var line = Ui.Box("recipe__line");
            Amounts(line, recipe.Inputs);
            if (recipe.Inputs.Length > 0) line.Add(Ui.Text("→", "recipe__arrow t-muted"));
            Amounts(line, recipe.Outputs);
            if (saySpoilage && recipe.FailChancePercent > 0)
                line.Add(Ui.Text($"брак {recipe.FailChancePercent}%", "recipe__note t-muted"));
            line.Add(Ui.Box("spacer"));
            if (recipe.MinLevel > level) line.Add(LevelChip(recipe.MinLevel));
            row.Add(line);

            if (recipe.Extras.Length > 0)
            {
                var extras = Ui.Box("recipe__extras");
                extras.Add(Ui.Text("+", "recipe__arrow t-muted"));
                foreach (var extra in recipe.Extras)
                {
                    var chip = Ui.Box("recipe__extra");
                    chip.Add(Icon(extra.Output.Resource, "recipe__icon recipe__icon--small"));
                    if (extra.Output.Amount > 1) chip.Add(Ui.Text(extra.Output.Amount.ToString(), "recipe__count t-black"));
                    chip.Add(Ui.Text($"{extra.ChancePercent}%", "recipe__chance t-bold"));
                    // a by-product the building's level has not reached, inside a recipe that already runs
                    if (extra.MinLevel > level && recipe.MinLevel <= level)
                    {
                        chip.AddToClassList("is-locked");
                        chip.Add(LevelChip(extra.MinLevel));
                    }
                    extras.Add(chip);
                }
                row.Add(extras);
            }
            Mark(row, recipe, level);
            return row;
        }

        // the piece going in and the piece coming out; the shared good is in the header
        private VisualElement Pair(ProductionRecipe recipe, ResourceKind shared, int level)
        {
            var pair = Ui.Box("recipe-pair");
            foreach (var input in recipe.Inputs)
                if (input.Resource != shared) pair.Add(Icon(input.Resource, "recipe-pair__icon"));
            pair.Add(Ui.Text("→", "recipe__arrow t-muted"));
            pair.Add(Icon(recipe.Outputs[0].Resource, "recipe-pair__icon"));
            Mark(pair, recipe, level);
            return pair;
        }

        private VisualElement Spoilage(ProductionRecipe recipe)
        {
            var line = Ui.Box("recipes__spoilage");
            line.Add(Ui.Text($"Брак {recipe.FailChancePercent}%", "recipes__words t-muted"));
            if (recipe.FailOutputs.Length > 0)
            {
                line.Add(Ui.Text("→", "recipe__arrow t-muted"));
                Amounts(line, recipe.FailOutputs);
            }
            return line;
        }

        // greyed while the building is too small for it; the hint names the goods and the level that opens it
        private void Mark(VisualElement element, ProductionRecipe recipe, int level)
        {
            bool locked = recipe.MinLevel > level;
            element.EnableInClassList("is-locked", locked);
            var names = new List<string>(recipe.Outputs.Length);
            foreach (var output in recipe.Outputs) names.Add(_context.Session.ResourceName(output.Resource));
            string title = string.Join(", ", names);
            string body = _context.Session.DescribeRecipe(recipe);
            if (locked) body += "\n" + $"Откроется, когда здание станет {recipe.MinLevel}-го уровня";
            // the good it makes has its page in the book
            _tooltip?.Attach(element, () => title, () => body,
                more: recipe.Outputs.Length > 0 ? _context.WikiLink(WikiSection.Goods, recipe.Outputs[0].Resource.ToString()) : null);
        }

        private static Label LevelChip(int level) => Ui.Text($"ур. {level}", "chip recipe__level");

        private void Amounts(VisualElement line, ResourceAmount[] amounts)
        {
            foreach (var amount in amounts)
            {
                line.Add(Icon(amount.Resource, "recipe__icon"));
                line.Add(Ui.Text(amount.Amount.ToString(), "recipe__count t-black"));
            }
        }

        // The good's icon, or its first letter while it has no art.
        private VisualElement Icon(ResourceKind resource, string classes)
        {
            var sprite = _context.Catalog.TryGetResource(resource)?.Icon;
            VisualElement icon;
            if (sprite != null)
            {
                icon = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit };
            }
            else
            {
                string name = _context.Session.ResourceName(resource);
                icon = Ui.Text(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1), "recipe__letter t-bold");
            }
            icon.pickingMode = PickingMode.Ignore;
            foreach (var style in classes.Split(' ')) icon.AddToClassList(style);
            return icon;
        }

        // every recipe spoils alike, so the card says it once
        private static bool SharesSpoilage(IReadOnlyList<ProductionRecipe> recipes)
        {
            var first = recipes[0];
            if (first.FailChancePercent == 0) return false;
            foreach (var recipe in recipes)
                if (recipe.FailChancePercent != first.FailChancePercent || !Same(recipe.FailOutputs, first.FailOutputs))
                    return false;
            return true;
        }

        private static bool Same(ResourceAmount[] a, ResourceAmount[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].Resource != b[i].Resource || a[i].Amount != b[i].Amount) return false;
            return true;
        }

        // One-for-one changes around a good every recipe takes alike: "a piece and a crystal → its enchanted twin".
        private static bool IsConversionList(IReadOnlyList<ProductionRecipe> recipes, out ResourceAmount shared)
        {
            shared = default;
            if (recipes.Count < 2) return false;
            foreach (var candidate in recipes[0].Inputs)
            {
                bool all = true;
                foreach (var recipe in recipes)
                {
                    if (IsPieceWith(recipe, candidate)) continue;
                    all = false;
                    break;
                }
                if (!all) continue;
                shared = candidate;
                return true;
            }
            return false;
        }

        // one piece of something and the shared good in, one piece out, nothing on the side
        private static bool IsPieceWith(ProductionRecipe recipe, ResourceAmount shared)
        {
            var inputs = recipe.Inputs;
            if (inputs.Length != 2 || recipe.Outputs.Length != 1 || recipe.Outputs[0].Amount != 1 || recipe.Extras.Length > 0)
                return false;
            int at = inputs[0].Resource == shared.Resource && inputs[0].Amount == shared.Amount ? 0
                : inputs[1].Resource == shared.Resource && inputs[1].Amount == shared.Amount ? 1 : -1;
            return at >= 0 && inputs[1 - at].Amount == 1 && inputs[1 - at].Resource != shared.Resource;
        }
    }
}
