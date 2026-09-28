using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation;
using UnityEngine;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Pictures for rewards: a building's catalog icon, a creature's portrait, the market's coin for gold and
    /// the barracks' icon for a battle. Null when the content has no art; callers show a monogram instead.
    /// </summary>
    public static class RewardArt
    {
        public static Sprite For(RewardSnapshot reward, GameContentCatalog catalog)
        {
            if (reward == null || catalog == null) return null;
            switch (reward.Kind)
            {
                case QuestRewardKind.UnlockBuilding:
                    return BuildingIcon(Building(catalog, reward.Reward.Building));
                case QuestRewardKind.UnlockUnit:
                    return Tight(Unit(catalog, reward.Reward.Unit)?.PortraitSprite);
                case QuestRewardKind.UnlockMission:
                    return BuildingIcon(Building(catalog, BuildingKind.Barracks));
                default:
                    return Coin(catalog);
            }
        }

        public static Sprite Coin(GameContentCatalog catalog) =>
            ContentPrefabs.Building(Building(catalog, BuildingKind.Market))?.Model?.SaleIncomeSprite;

        public static Sprite BuildingIcon(BuildingDefinition building) =>
            building == null ? null : building.Icon != null ? building.Icon : building.Sprite;

        private static readonly Dictionary<Sprite, Sprite> s_tight = new();

        /// <summary>
        /// The sprite cut down to its drawn pixels. Creature portraits are animation frames with a wide empty
        /// border, which would leave the creature tiny in a large frame. Uses the tight mesh Unity builds on
        /// import; atlas-packed or full-rect sprites come back unchanged.
        /// </summary>
        public static Sprite Tight(Sprite sprite)
        {
            if (sprite == null || sprite.packed) return sprite;
            if (s_tight.TryGetValue(sprite, out var cached) && cached != null) return cached;
            var vertices = sprite.vertices;
            if (vertices.Length == 0) return sprite;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var vertex in vertices)
            {
                var pixel = vertex * sprite.pixelsPerUnit + sprite.pivot;
                min = Vector2.Min(min, pixel);
                max = Vector2.Max(max, pixel);
            }
            var rect = sprite.rect;
            var cut = Rect.MinMaxRect(
                Mathf.Max(rect.xMin, rect.xMin + Mathf.Floor(min.x)), Mathf.Max(rect.yMin, rect.yMin + Mathf.Floor(min.y)),
                Mathf.Min(rect.xMax, rect.xMin + Mathf.Ceil(max.x)), Mathf.Min(rect.yMax, rect.yMin + Mathf.Ceil(max.y)));
            var tight = cut.width < 1f || cut.height < 1f || cut == rect
                ? sprite
                : Sprite.Create(sprite.texture, cut, new Vector2(.5f, .5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            if (tight != sprite)
            {
                tight.name = sprite.name + " (tight)";
                tight.hideFlags = HideFlags.DontSave;
            }
            s_tight[sprite] = tight;
            return tight;
        }

        private static BuildingDefinition Building(GameContentCatalog catalog, BuildingKind kind)
        {
            foreach (var building in catalog.Buildings)
                if (building != null && building.Kind == kind) return building;
            return null;
        }

        private static UnitDefinition Unit(GameContentCatalog catalog, UnitKind kind)
        {
            foreach (var unit in catalog.Units)
                if (unit != null && unit.Kind == kind) return unit;
            return null;
        }
    }
}
