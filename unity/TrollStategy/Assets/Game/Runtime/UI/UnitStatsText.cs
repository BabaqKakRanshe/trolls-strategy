using System.Globalization;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.UI
{
    public static class UnitStatsText
    {
        public static string Compact(UnitDefinition definition)
        {
            if (definition == null) return "";
            return $"Сила {definition.Strength} · скорость {Number(definition.Speed)}\n" +
                   $"Выносливость {definition.Stamina}% · груз {Number(ColonySimulation.CarryCapacity(definition.Stamina))}";
        }

        public static string Detailed(UnitDefinition definition)
        {
            if (definition == null) return "";
            return $"Сила: {definition.Strength} | Скорость: {Number(definition.Speed)}\n" +
                   $"Выносливость: {definition.Stamina}% (груз {Number(ColonySimulation.CarryCapacity(definition.Stamina))} за ходку)";
        }

        public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
