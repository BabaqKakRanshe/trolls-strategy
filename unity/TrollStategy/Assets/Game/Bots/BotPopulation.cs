using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TrollStrategy.Content;

namespace TrollStrategy.Bots
{
    public enum BotTraitKind
    {
        /// <summary>A number from a range, rounded to the trait's step.</summary>
        Number,
        /// <summary>Yes (1) or no (0), yes with the trait's chance.</summary>
        Flag,
        /// <summary>One of the trait's choices (its index), all equally likely.</summary>
        Choice
    }

    /// <summary>
    /// One way players differ: how often they look, what a click costs them, whom they hire. A persona draws one
    /// value per trait; the report groups the runs by it to show what the trait does to the campaign.
    /// </summary>
    public sealed class BotTrait
    {
        private readonly Func<double, string> _format;

        internal BotTrait(string key, string title, string shortTitle, BotTraitKind kind, Func<double, string> format,
            double min = 0, double max = 1, double step = 0, bool logScale = false, double chance = 0,
            string[] choices = null, string requires = null)
        {
            Key = key;
            Title = title;
            Short = shortTitle;
            Kind = kind;
            _format = format;
            Min = min;
            Max = max;
            Step = step;
            LogScale = logScale;
            Chance = chance;
            Choices = choices ?? Array.Empty<string>();
            Requires = requires;
        }

        public string Key { get; }
        /// <summary>What the trait is, in the report's words.</summary>
        public string Title { get; }
        /// <summary>A word or two for a table's column.</summary>
        public string Short { get; }
        public BotTraitKind Kind { get; }
        public double Min { get; }
        public double Max { get; }
        /// <summary>Numbers are rounded to this; 1 on whole bounds draws whole numbers, every one equally likely.</summary>
        public double Step { get; }
        /// <summary>Numbers spread evenly over ratios, not differences: 5–10 s is as likely as 30–60 s.</summary>
        public bool LogScale { get; }
        /// <summary>A flag's chance of yes.</summary>
        public double Chance { get; }
        public IReadOnlyList<string> Choices { get; }
        /// <summary>The flag that must be on for this trait to change anything (null when it always does).</summary>
        public string Requires { get; }

        public string Format(double value) => _format(value);

        /// <summary>The range or the chances, in words.</summary>
        public string Range => Kind switch
        {
            BotTraitKind.Flag => $"да в {Percent(Chance)}",
            BotTraitKind.Choice => string.Join(", ", Choices),
            _ => $"{Format(Min)} — {Format(Max)}"
        };

        // one roll per trait, whatever its kind, so a trait added at the end of the list leaves the others as they were
        internal double Draw(double roll)
        {
            switch (Kind)
            {
                case BotTraitKind.Flag:
                    return roll < Chance ? 1 : 0;
                case BotTraitKind.Choice:
                    return Math.Min(Choices.Count - 1, (int)(roll * Choices.Count));
            }
            if (Step == 1 && !LogScale && Min == Math.Floor(Min) && Max == Math.Floor(Max))
                return Math.Min(Max, Min + Math.Floor(roll * (Max - Min + 1)));
            double value = LogScale ? Min * Math.Pow(Max / Min, roll) : Min + (Max - Min) * roll;
            if (Step > 0) value = Math.Round(value / Step) * Step;
            return Math.Max(Min, Math.Min(Max, Math.Round(value, 6)));
        }

        internal string Signature => string.Join(":", Key, Kind, Num(Min), Num(Max), Num(Step), LogScale, Num(Chance),
            string.Join("/", Choices), Requires);

        internal static string Percent(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        private static string Num(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The players the bots stand for. Persona n draws every trait from its range with seed n, so the same
    /// population plays every run: a change between two runs is a change of the game's rules or numbers, and each
    /// persona can be compared with itself. The traits vary only what a player can choose; the game's own dice
    /// (battles, rewards) are the player's too and stay as the game rolls them.
    /// New traits go to the end of <see cref="Traits"/>: each trait takes one roll in turn, so the personas keep
    /// the values they had. A change of a range changes <see cref="Signature"/>, and the report stops pairing runs
    /// across it.
    /// </summary>
    public static class BotPopulation
    {
        /// <summary>Personas in a run from the menu and the batch script.</summary>
        public const int DefaultCount = 200;
        /// <summary>Personas in the large run (a night, a release).</summary>
        public const int LargeCount = 1000;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static string Seconds(double v) => v.ToString("0.#", Invariant) + " с";
        private static string Count(double v) => v.ToString("0", Invariant);
        private static string YesNo(double v) => v > 0 ? "да" : "нет";

        private static readonly string[] Hiring = { "гоблинов", "троллей", "выгодных", "сильных" };

        public static IReadOnlyList<BotTrait> Traits { get; } = new[]
        {
            new BotTrait("think", "Смотрит в колонию раз в", "Взгляд", BotTraitKind.Number, Seconds, 5, 60, 1, logScale: true),
            new BotTrait("click", "Одна команда стоит", "Команда", BotTraitKind.Number, Seconds, 4, 12, 0.5),
            new BotTrait("read", "Чтение задания", "Чтение", BotTraitKind.Number, Seconds, 10, 45, 1),
            new BotTrait("hiring", "Кого нанимает", "Найм", BotTraitKind.Choice, v => Hiring[(int)v], choices: Hiring),
            new BotTrait("haulers", "Носильщиков на маршрут, не больше", "Носильщики", BotTraitKind.Number, Count, 1, 8, 1),
            new BotTrait("grows", "Растёт сверх заданий", "Рост", BotTraitKind.Flag, YesNo, chance: 0.8),
            new BotTrait("raws", "Сырьевых зданий при росте", "Сырьё", BotTraitKind.Number, Count, 2, 5, 1, requires: "grows"),
            new BotTrait("spare", "Вкладывает, когда золота больше цены в", "Запас", BotTraitKind.Number,
                v => "×" + v.ToString("0.0", Invariant), 1.2, 3, 0.1, requires: "grows"),
            new BotTrait("reserve", "Бережёт на шаг задания", "Бережёт", BotTraitKind.Number,
                BotTrait.Percent, 0.5, 1, 0.05, requires: "grows"),
            new BotTrait("guild", "Улучшает гильдию носильщиков", "Гильдия", BotTraitKind.Flag, YesNo, chance: 0.7, requires: "grows"),
            new BotTrait("army", "Улучшает бараки и склад экипировки", "Армия", BotTraitKind.Flag, YesNo, chance: 0.35, requires: "grows"),
            new BotTrait("fights", "Ходит на арену за золотом", "Арена", BotTraitKind.Flag, YesNo, chance: 0.25),
            new BotTrait("squad", "Троллей до первого боя", "Тролли", BotTraitKind.Number, Count, 2, 3, 1),
            // the choices a bot takes while it plays (since 2026-10-06): its own dice decide each time
            new BotTrait("inattention", "Не смотрит за хозяйством в доле взглядов", "Невнимание", BotTraitKind.Number,
                BotTrait.Percent, 0, 0.6, 0.05),
            new BotTrait("placement", "Ставит здание на одно из лучших мест", "Место", BotTraitKind.Number,
                v => v <= 1 ? "лучшее" : "из " + Count(v), 1, 6, 1),
            new BotTrait("risk", "Лезет на арену, когда отряд слабее, в доле взглядов", "Риск", BotTraitKind.Number,
                BotTrait.Percent, 0, 0.3, 0.05)
        };

        /// <summary>The traits and their ranges in one short code: runs with the same code played the same personas.</summary>
        public static string Signature { get; } = Hash("population-1|" + string.Join("|", Traits.Select(t => t.Signature)));

        public static int IndexOf(string key)
        {
            for (int i = 0; i < Traits.Count; i++)
                if (Traits[i].Key == key) return i;
            throw new ArgumentException($"Нет признака {key}");
        }

        /// <summary>Persona <paramref name="seed"/>'s value of every trait, in the order of <see cref="Traits"/>.</summary>
        public static double[] Values(int seed)
        {
            if (seed < 1) throw new ArgumentOutOfRangeException(nameof(seed), "Персоны считаются с 1");
            var dice = new BotDice(seed);
            return Traits.Select(t => t.Draw(dice.Roll())).ToArray();
        }

        /// <summary>Personas 1 to <paramref name="count"/>.</summary>
        public static List<BotProfile> Personas(int count) => Enumerable.Range(1, count).Select(Persona).ToList();

        public static BotProfile Persona(int seed)
        {
            var v = Values(seed);
            double Get(string key) => v[IndexOf(key)];
            int hiring = (int)Get("hiring");
            var hosts = new List<BuildingKind>();
            if (Get("army") > 0) hosts.AddRange(new[] { BuildingKind.Barracks, BuildingKind.Armory });
            if (Get("guild") > 0) hosts.Add(BuildingKind.HaulersGuild);
            return new BotProfile(Id(seed), $"Бот {seed}", Describe(v),
                thinkSeconds: (float)Get("think"), grows: Get("grows") > 0,
                workerKind: hiring == 1 ? UnitKind.Troll : UnitKind.Goblin,
                maxHaulersPerRoute: (int)Get("haulers"), maxRawProducers: (int)Get("raws"),
                growthGoldFactor: (float)Get("spare"), reserveShare: (float)Get("reserve"),
                squadTrolls: (int)Get("squad"), fightsForGold: Get("fights") > 0,
                actionSeconds: (float)Get("click"), questReadSeconds: (float)Get("read"),
                roleHiring: hiring switch { 2 => 0.9f, 3 => 0.65f, _ => 0f },
                upgradeHosts: hosts.ToArray(), seed: seed, inattention: (float)Get("inattention"),
                placementChoice: (int)Get("placement"), arenaRisk: (float)Get("risk"));
        }

        public static string Id(int seed) => "p" + seed.ToString(Invariant);

        /// <summary>A persona by its id ("p17"), or the reference bot of the tests ("typical"); null when there is none.</summary>
        public static BotProfile Find(string id)
        {
            if (id == BotProfile.Typical.Id) return BotProfile.Typical;
            return id != null && id.StartsWith("p", StringComparison.Ordinal) &&
                   int.TryParse(id.Substring(1), NumberStyles.None, Invariant, out int seed) && seed >= 1
                ? Persona(seed)
                : null;
        }

        private static string Describe(double[] v)
        {
            string F(string key) => Traits[IndexOf(key)].Format(v[IndexOf(key)]);
            bool On(string key) => v[IndexOf(key)] > 0;
            var text = new StringBuilder();
            text.Append($"Смотрит раз в {F("think")}, команда стоит {F("click")}, чтение задания {F("read")}. ");
            text.Append($"Нанимает {F("hiring")}, носильщиков на маршрут не больше {F("haulers")}. ");
            if (On("grows"))
            {
                var upgrades = new List<string>();
                if (On("guild")) upgrades.Add("гильдию носильщиков");
                if (On("army")) upgrades.Add("бараки и склад экипировки");
                text.Append($"Растёт: до {F("raws")} сырьевых зданий, вкладывает при золоте {F("spare")} от цены, " +
                            $"бережёт {F("reserve")} на шаг задания, улучшает " +
                            (upgrades.Count > 0 ? string.Join(" и ", upgrades) : "только то, что просят задания") + ". ");
            }
            else text.Append("Делает только то, что просят задания. ");
            text.Append(On("fights") ? "Ходит на арену за золотом" : "Воюет, когда просит задание");
            text.Append($", троллей до первого боя {F("squad")}. ");
            text.Append($"Не смотрит за хозяйством в {F("inattention")} взглядов, ставит здание на " +
                        (v[IndexOf("placement")] <= 1 ? "лучшее место" : $"одно из {Count(v[IndexOf("placement")])} лучших мест") +
                        $", лезет на арену слабее врага в {F("risk")} взглядов.");
            return text.ToString();
        }

        private static string Hash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("x8", Invariant);
        }
    }

    /// <summary>
    /// A bot's own dice (SplitMix32): the same seed rolls the same numbers on every machine. Stream 0 draws a
    /// persona's traits, stream 1 the choices it makes while it plays; the game's own dice are not these.
    /// </summary>
    internal sealed class BotDice
    {
        private uint _state;

        public BotDice(int seed, uint stream = 0) => _state = (uint)seed * 0x9E3779B9u ^ stream * 0x85EBCA6Bu;

        /// <summary>A number in [0, 1).</summary>
        public double Roll()
        {
            uint z = _state += 0x9E3779B9u;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            z ^= z >> 16;
            return z / 4294967296.0;
        }

        /// <summary>True with the chance; a chance of 0 rolls nothing.</summary>
        public bool Chance(double chance) => chance > 0 && Roll() < chance;

        /// <summary>One of <paramref name="count"/> in turn, all equally likely; one of one rolls nothing.</summary>
        public int Pick(int count) => count <= 1 ? 0 : Math.Min(count - 1, (int)(Roll() * count));
    }
}
