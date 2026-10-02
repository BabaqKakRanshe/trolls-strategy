using System;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    /// <summary>
    /// Colony improvements bought level by level in a host building (the haulers' guild, the barracks). An
    /// upgrade is colony-wide: every level adds its effect to every creature or battle it concerns.
    /// </summary>
    public static class UpgradeRules
    {
        /// <summary>The summed effect of every upgrade of this kind the colony has bought.</summary>
        public static int Total(GameState state, GameContentCatalog catalog, UpgradeEffect effect)
        {
            if (state == null || catalog == null) return 0;
            int total = 0;
            foreach (var upgrade in catalog.Upgrades)
                if (upgrade != null && upgrade.Effect == effect)
                    total += state.UpgradeLevel(upgrade.Id) * upgrade.AmountPerLevel;
            return total;
        }

        /// <summary>A value scaled up by a percent effect: 100 with +20% gives 120.</summary>
        public static float Raise(float value, int percent) => value * (1f + Math.Max(0, percent) / 100f);

        /// <summary>A time scaled down by a percent effect, never below a tenth of it.</summary>
        public static float Shorten(float value, int percent) => value * Math.Max(0.1f, 1f - Math.Max(0, percent) / 100f);

        public static CommandResult Validate(GameState state, string upgradeId, GameContentCatalog catalog)
        {
            var upgrade = catalog.TryGetUpgrade(upgradeId);
            if (upgrade == null) return CommandResult.Fail("Такого улучшения нет");
            if (!state.Buildings.Exists(b => b.Kind == upgrade.Host))
            {
                string host = TryHostName(catalog, upgrade.Host);
                return CommandResult.Fail($"Сначала постройте: {host}");
            }
            int cost = upgrade.CostFrom(state.UpgradeLevel(upgrade.Id));
            if (cost < 0) return CommandResult.Fail("Достигнут максимальный уровень");
            if (state.Gold < cost) return CommandResult.Fail("Недостаточно золота");
            return CommandResult.Success();
        }

        public static CommandResult Buy(GameState state, string upgradeId, GameContentCatalog catalog)
        {
            var valid = Validate(state, upgradeId, catalog);
            if (!valid.Ok) return valid;
            var upgrade = catalog.TryGetUpgrade(upgradeId);
            int level = state.UpgradeLevel(upgrade.Id);
            state.Gold -= upgrade.CostFrom(level);
            state.Upgrades[upgrade.Id] = level + 1;
            return CommandResult.Success();
        }

        private static string TryHostName(GameContentCatalog catalog, BuildingKind kind)
        {
            foreach (var building in catalog.Buildings)
                if (building != null && building.Kind == kind) return building.DisplayName.ToLowerInvariant();
            return kind.ToString();
        }
    }
}
