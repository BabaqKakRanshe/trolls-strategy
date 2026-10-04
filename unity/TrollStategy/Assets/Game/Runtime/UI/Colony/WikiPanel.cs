using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    public enum WikiSection
    {
        Creatures,
        Enemies,
        Buildings,
        Goods,
        Upgrades,
        Arena
    }

    /// <summary>
    /// The book (the book tool, K): everything the game knows, read from the content catalog. A white sheet over
    /// the island like the menu, the colony standing still while it is open. Sections on the left, a table of
    /// pictures and numbers in the middle, the chosen entry on the right; a building's picture anywhere opens
    /// that building. The search finds names across all sections.
    /// </summary>
    public sealed class WikiPanel
    {
        private static readonly (WikiSection Section, string Title)[] Sections =
        {
            (WikiSection.Creatures, "Существа"), (WikiSection.Enemies, "Враги"), (WikiSection.Buildings, "Здания"),
            (WikiSection.Goods, "Товары"), (WikiSection.Upgrades, "Улучшения"), (WikiSection.Arena, "Арена")
        };

        private sealed class Entry
        {
            public WikiSection Section;
            public string Key;
            public string Name;
            public Sprite Picture;
            public bool Pixel;
            public Action<VisualElement> Cells;
            public Action<VisualElement> Detail;
        }

        private readonly ColonyHudContext _context;
        private readonly Sprite _coin;
        private readonly VisualElement _overlay;
        private readonly VisualElement _head;
        private readonly ScrollView _rows;
        private readonly ScrollView _detail;
        private readonly TextField _search;
        private readonly Dictionary<WikiSection, Button> _nav = new();
        private readonly Dictionary<WikiSection, List<Entry>> _entries = new();
        private readonly List<(Entry Entry, Button Row)> _shown = new();
        private readonly Dictionary<UnitKind, List<int>> _arenaLevels = new();
        private readonly Dictionary<UnitKind, int> _opensAt = new();
        private Entry _selected;
        private string _filter = string.Empty;

        public WikiPanel(VisualElement root, ColonyHudContext context)
        {
            _context = context;
            _coin = RewardArt.Coin(context.Catalog);
            _overlay = Ui.Require<VisualElement>(root, "wiki-overlay");
            _head = Ui.Require<VisualElement>(root, "wiki-head");
            _rows = Ui.Require<ScrollView>(root, "wiki-rows");
            _detail = Ui.Require<ScrollView>(root, "wiki-detail");
            _search = Ui.Require<TextField>(root, "wiki-search");
            _search.RegisterValueChangedCallback(evt => Search(evt.newValue));
            UiFeel.Bind(Ui.Require<Button>(root, "wiki-close"), Close, Sfx.UiBack);
            var nav = Ui.Require<VisualElement>(root, "wiki-nav");
            foreach (var (section, title) in Sections)
            {
                var button = Ui.TextButton(string.Empty, "btn btn-flat wiki-nav__item");
                button.Add(Ui.Text(title, "wiki-nav__title"));
                button.Add(Ui.Text(string.Empty, "wiki-nav__count"));
                var target = section;
                UiFeel.Bind(button, () => Show(target));
                nav.Add(button);
                _nav[section] = button;
            }
            foreach (var mission in context.Catalog.Missions)
            {
                if (mission == null) continue;
                foreach (var enemy in mission.Enemies)
                {
                    if (!_arenaLevels.TryGetValue(enemy.Kind, out var levels)) _arenaLevels[enemy.Kind] = levels = new List<int>();
                    if (!levels.Contains(mission.Level)) levels.Add(mission.Level);
                }
                if (mission.UnlockUnit is UnitKind opens && !_opensAt.ContainsKey(opens)) _opensAt[opens] = mission.Level;
            }
            foreach (var levels in _arenaLevels.Values) levels.Sort();
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => Ui.IsShown(_overlay);
        public WikiSection Section { get; private set; } = WikiSection.Creatures;
        /// <summary>The names of the rows shown, in order (the search's hits across sections when searching).</summary>
        public IReadOnlyList<string> RowNames
        {
            get
            {
                var names = new List<string>(_shown.Count);
                foreach (var (entry, _) in _shown) names.Add(entry.Name);
                return names;
            }
        }
        public string SelectedName => _selected?.Name;
        public IReadOnlyList<Button> Rows
        {
            get
            {
                var rows = new List<Button>(_shown.Count);
                foreach (var (_, row) in _shown) rows.Add(row);
                return rows;
            }
        }
        public Button NavButton(WikiSection section) => _nav[section];
        /// <summary>The words of the chosen entry's page, for tests.</summary>
        public IReadOnlyList<string> DetailTexts
        {
            get
            {
                var texts = new List<string>();
                _detail.Query<Label>().ForEach(label => { if (!string.IsNullOrEmpty(label.text)) texts.Add(label.text); });
                return texts;
            }
        }
        /// <summary>Typing in the search: the book's own key must not close it.</summary>
        public bool IsTyping => IsOpen && _search.focusController?.focusedElement == _search;

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            Build();
            Ui.Show(_overlay, true);
            UiMotion.PopIn(_overlay, .18f);
            _context.SetPaused?.Invoke(true);
            Show(Section);
        }

        public void Close()
        {
            if (!IsOpen) return;
            Ui.Show(_overlay, false);
            _context.SetPaused?.Invoke(false);
        }

        public void Show(WikiSection section)
        {
            Section = section;
            if (!string.IsNullOrEmpty(_filter)) _search.SetValueWithoutNotify(_filter = string.Empty);
            foreach (var (key, button) in _nav) button.EnableInClassList("is-on", key == section);
            Fill(_entries.TryGetValue(section, out var list) ? list : new List<Entry>(), section);
        }

        /// <summary>Finds names across all sections; an empty search shows the section again.</summary>
        public void Search(string text)
        {
            _filter = (text ?? string.Empty).Trim();
            if (_search.value != text) _search.SetValueWithoutNotify(text ?? string.Empty);
            if (_filter.Length == 0)
            {
                Show(Section);
                return;
            }
            var hits = new List<Entry>();
            foreach (var (section, _) in Sections)
                if (_entries.TryGetValue(section, out var list))
                    foreach (var entry in list)
                        if (Localization.T(entry.Name).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            entry.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            hits.Add(entry);
            Fill(hits, null);
        }

        /// <summary>Opens an entry by its key (a kind's name, an upgrade's id, a level number).</summary>
        public void Go(WikiSection section, string key)
        {
            Show(section);
            foreach (var (entry, _) in _shown)
                if (entry.Key == key)
                {
                    Select(entry);
                    return;
                }
        }

        // ---- the table ----

        private void Fill(List<Entry> entries, WikiSection? section)
        {
            _head.Clear();
            _rows.Clear();
            _shown.Clear();
            if (section is WikiSection columns) Head(columns);
            else Head(null);
            foreach (var entry in entries)
            {
                var row = Ui.TextButton(string.Empty, "btn btn-flat wiki-row");
                row.Add(Picture(entry.Picture, entry.Pixel, "wiki-pic"));
                if (section != null) entry.Cells(row);
                else
                {
                    row.Add(Ui.Text(entry.Name, "wiki-cell wiki-cell--name t-black"));
                    row.Add(Ui.Text(Title(entry.Section), "wiki-cell wiki-cell--text t-muted"));
                }
                var picked = entry;
                UiFeel.Bind(row, () => Select(picked));
                _rows.Add(row);
                _shown.Add((entry, row));
            }
            if (entries.Count == 0) _rows.Add(Ui.Text("Ничего не нашлось", "wiki-empty t-muted"));
            var keep = _selected != null && entries.Contains(_selected) ? _selected : entries.Count > 0 ? entries[0] : null;
            Select(keep);
        }

        private void Head(WikiSection? section)
        {
            _head.Add(Ui.Box("wiki-pic"));
            string[] titles = section switch
            {
                WikiSection.Creatures => new[] { "Существо", "Работа", "Шаг", "Груз", "Бой", "Любимые здания", "Найм" },
                WikiSection.Enemies => new[] { "Враг", "Здоровье", "Урон", "Броня", "Уровни арены" },
                WikiSection.Buildings => new[] { "Здание", "Размер", "Рабочих", "Главный рецепт", "Цена" },
                WikiSection.Goods => new[] { "Товар", "На рынке", "Делают", "Берут" },
                WikiSection.Upgrades => new[] { "Улучшение", "Где", "Уровень", "Цены уровней" },
                WikiSection.Arena => new[] { "Уровень", "Враги", "Награда", "Открывает" },
                _ => new[] { "Найдено", "Раздел" }
            };
            string[] classes = Columns(section);
            for (int i = 0; i < titles.Length; i++)
                _head.Add(Ui.Text(titles[i], "wiki-cell wiki-head__cell " + classes[i]));
        }

        private static string[] Columns(WikiSection? section) => section switch
        {
            WikiSection.Creatures => new[] { "wiki-cell--name", "wiki-cell--num", "wiki-cell--num", "wiki-cell--num", "wiki-cell--fight", "wiki-cell--wide", "wiki-cell--price" },
            WikiSection.Enemies => new[] { "wiki-cell--name", "wiki-cell--num", "wiki-cell--num", "wiki-cell--num", "wiki-cell--wide" },
            WikiSection.Buildings => new[] { "wiki-cell--name", "wiki-cell--num", "wiki-cell--num", "wiki-cell--wide", "wiki-cell--price" },
            WikiSection.Goods => new[] { "wiki-cell--name", "wiki-cell--price", "wiki-cell--wide", "wiki-cell--wide" },
            WikiSection.Upgrades => new[] { "wiki-cell--name", "wiki-cell--host", "wiki-cell--level", "wiki-cell--wide" },
            WikiSection.Arena => new[] { "wiki-cell--name", "wiki-cell--wide", "wiki-cell--reward", "wiki-cell--host" },
            _ => new[] { "wiki-cell--name", "wiki-cell--text" }
        };

        private void Select(Entry entry)
        {
            _selected = entry;
            foreach (var (shown, row) in _shown) row.EnableInClassList("is-on", shown == entry);
            _detail.Clear();
            if (entry == null) return;
            var big = Picture(entry.Picture, entry.Pixel, "wiki-big");
            _detail.Add(big);
            _detail.Add(Ui.Text(entry.Name, "wiki-detail__title t-black"));
            entry.Detail(_detail);
        }

        // ---- the entries, from the content ----

        private void Build()
        {
            var catalog = _context.Catalog;
            var session = _context.Session;
            var snapshot = session.CurrentSnapshot;
            _entries.Clear();
            var creatures = new List<Entry>();
            var enemies = new List<Entry>();
            foreach (var unit in catalog.Units)
            {
                if (unit == null) continue;
                if (unit.Hireable) creatures.Add(Creature(unit, session));
                else enemies.Add(Enemy(unit));
            }
            var buildings = new List<Entry>();
            foreach (var building in catalog.Buildings)
                if (building != null && building.Constructible) buildings.Add(Building(building, session));
            var goods = new List<Entry>();
            foreach (var resource in catalog.Resources)
                if (resource != null) goods.Add(Good(resource));
            var upgrades = new List<Entry>();
            foreach (var upgrade in catalog.Upgrades)
                if (upgrade != null) upgrades.Add(Upgrade(upgrade, snapshot));
            var arena = new List<Entry>();
            var missions = new List<BattleMissionDefinition>();
            foreach (var mission in catalog.Missions)
                if (mission != null) missions.Add(mission);
            missions.Sort((a, b) => a.Level.CompareTo(b.Level));
            foreach (var mission in missions) arena.Add(Level(mission));

            _entries[WikiSection.Creatures] = creatures;
            _entries[WikiSection.Enemies] = enemies;
            _entries[WikiSection.Buildings] = buildings;
            _entries[WikiSection.Goods] = goods;
            _entries[WikiSection.Upgrades] = upgrades;
            _entries[WikiSection.Arena] = arena;
            foreach (var (section, button) in _nav)
                Ui.SetText(button.Q<Label>(className: "wiki-nav__count"), _entries[section].Count.ToString());
            _selected = null;
        }

        private Entry Creature(UnitDefinition unit, GameSession session)
        {
            int price = session.HirePrice(unit.Kind);
            bool opens = _opensAt.TryGetValue(unit.Kind, out int level);
            string when = opens ? $"после {level} уровня арены" : "с начала игры";
            var cols = Columns(WikiSection.Creatures);
            return new Entry
            {
                Section = WikiSection.Creatures, Key = unit.Kind.ToString(), Name = unit.DisplayName,
                Picture = RewardArt.Tight(unit.PortraitSprite), Pixel = true,
                Cells = row =>
                {
                    row.Add(NameCell(unit.DisplayName, when, cols[0]));
                    row.Add(Num(unit.Strength.ToString(), cols[1]));
                    row.Add(Num(unit.Speed.ToString("0.#"), cols[2]));
                    row.Add(Num(unit.Stamina.ToString(), cols[3]));
                    row.Add(Num($"{unit.CombatHealth} / {unit.CombatDamage} / {unit.CombatArmor}", cols[4]));
                    row.Add(Favorites(unit, cols[5]));
                    row.Add(Price(price, cols[6]));
                },
                Detail = page =>
                {
                    page.Add(Para(unit.Description));
                    page.Add(Caption("В колонии"));
                    page.Add(Chips(("work", unit.Strength.ToString(), "работа"), (null, unit.Speed.ToString("0.#"), "шаг"),
                        ("haul", unit.Stamina.ToString(), "груз")));
                    page.Add(Caption("В бою"));
                    page.Add(Chips(("health", unit.CombatHealth.ToString(), null), ("damage", unit.CombatDamage.ToString(), null),
                        ("armor", unit.CombatArmor.ToString(), null)));
                    if (unit.FavoredBuildings.Count > 0)
                    {
                        page.Add(Caption($"Работает быстрее на {unit.FavoredWorkPercent}%"));
                        page.Add(BuildingChips(unit.FavoredBuildings));
                    }
                    page.Add(Caption("Как открыть"));
                    page.Add(Para(opens ? $"Победи на {level} уровне арены, потом нанимай в каталоге." : "Есть в каталоге с начала игры."));
                    page.Add(Price(price, "wiki-detail__price"));
                }
            };
        }

        private Entry Enemy(UnitDefinition unit)
        {
            _arenaLevels.TryGetValue(unit.Kind, out var levels);
            string where = levels == null || levels.Count == 0 ? "—" : string.Join(", ", levels);
            var cols = Columns(WikiSection.Enemies);
            return new Entry
            {
                Section = WikiSection.Enemies, Key = unit.Kind.ToString(), Name = unit.DisplayName,
                Picture = RewardArt.Tight(unit.PortraitSprite), Pixel = true,
                Cells = row =>
                {
                    row.Add(NameCell(unit.DisplayName, null, cols[0]));
                    row.Add(Num(unit.CombatHealth.ToString(), cols[1]));
                    row.Add(Num(unit.CombatDamage.ToString(), cols[2]));
                    row.Add(Num(unit.CombatArmor.ToString(), cols[3]));
                    row.Add(Ui.Text(where, "wiki-cell " + cols[4]));
                },
                Detail = page =>
                {
                    if (!string.IsNullOrWhiteSpace(unit.Description)) page.Add(Para(unit.Description));
                    page.Add(Caption("В бою"));
                    page.Add(Chips(("health", unit.CombatHealth.ToString(), null), ("damage", unit.CombatDamage.ToString(), null),
                        ("armor", unit.CombatArmor.ToString(), null)));
                    page.Add(Caption("Уровни арены"));
                    page.Add(Para(where));
                }
            };
        }

        private Entry Building(BuildingDefinition building, GameSession session)
        {
            int price = session.BuildingPrice(building.Kind);
            var main = GameSession.MainRecipe(building);
            var cols = Columns(WikiSection.Buildings);
            var workers = new List<UnitDefinition>();
            foreach (var unit in _context.Catalog.Units)
                if (unit != null && unit.Hireable && unit.Favors(building.Kind)) workers.Add(unit);
            return new Entry
            {
                Section = WikiSection.Buildings, Key = building.Kind.ToString(), Name = building.DisplayName,
                Picture = RewardArt.BuildingIcon(building),
                Cells = row =>
                {
                    row.Add(NameCell(building.DisplayName, null, cols[0]));
                    row.Add(Num($"{building.Width}×{building.Height}", cols[1]));
                    row.Add(Num(building.MaxWorkers > 0 ? building.MaxWorkers.ToString() : "—", cols[2]));
                    row.Add(main != null ? Recipe(main, cols[3]) : Ui.Text(session.DescribeBuilding(building, recipes: false), "wiki-cell t-muted " + cols[3]));
                    row.Add(Price(price, cols[4]));
                },
                Detail = page =>
                {
                    page.Add(Para(session.DescribeBuilding(building)));
                    if (workers.Count > 0)
                    {
                        page.Add(Caption("Быстрее работают"));
                        var chips = Ui.Box("wiki-chips");
                        foreach (var unit in workers)
                            chips.Add(UnitChip(unit, $"+{unit.FavoredWorkPercent}%"));
                        page.Add(chips);
                    }
                    if (building.MaxLevel > 1)
                    {
                        var costs = new List<string>();
                        for (int level = 1; level < building.MaxLevel; level++) costs.Add(building.UpgradeCost(level).ToString());
                        page.Add(Caption("Цены уровней"));
                        page.Add(Para(string.Join(" → ", costs)));
                    }
                    page.Add(Price(price, "wiki-detail__price"));
                }
            };
        }

        private Entry Good(ResourceDefinition resource)
        {
            int price = ColonySimulation.SalePrice(_context.Catalog, resource.Kind, 1);
            var makers = ChainLinks.Makers(_context.Catalog, new[] { resource.Kind });
            var takers = ChainLinks.Takers(_context.Catalog, new[] { resource.Kind });
            var cols = Columns(WikiSection.Goods);
            return new Entry
            {
                Section = WikiSection.Goods, Key = resource.Kind.ToString(), Name = resource.DisplayName,
                Picture = resource.Icon,
                Cells = row =>
                {
                    row.Add(NameCell(resource.DisplayName, null, cols[0]));
                    row.Add(Price(price, cols[1]));
                    row.Add(BuildingIcons(makers, cols[2]));
                    row.Add(BuildingIcons(takers, cols[3]));
                },
                Detail = page =>
                {
                    page.Add(Price(price, "wiki-detail__price"));
                    if (makers.Count > 0)
                    {
                        page.Add(Caption("Делают"));
                        page.Add(BuildingChips(Kinds(makers)));
                    }
                    if (takers.Count > 0)
                    {
                        page.Add(Caption("Берут"));
                        page.Add(BuildingChips(Kinds(takers)));
                    }
                }
            };
        }

        private Entry Upgrade(UpgradeDefinition upgrade, GameSnapshot snapshot)
        {
            int level = 0;
            foreach (var bought in snapshot.Upgrades)
                if (bought.Id == upgrade.Id) level = bought.Level;
            var costs = new List<string>();
            for (int i = 0; i < upgrade.MaxLevel; i++) costs.Add(upgrade.CostFrom(i).ToString());
            var host = Find(upgrade.Host);
            var cols = Columns(WikiSection.Upgrades);
            return new Entry
            {
                Section = WikiSection.Upgrades, Key = upgrade.Id, Name = upgrade.DisplayName,
                Picture = host != null ? RewardArt.BuildingIcon(host) : null,
                Cells = row =>
                {
                    row.Add(NameCell(upgrade.DisplayName, null, cols[0]));
                    row.Add(BuildingIcons(host != null ? new List<BuildingDefinition> { host } : new List<BuildingDefinition>(), cols[1]));
                    row.Add(Num($"{level} / {upgrade.MaxLevel}", cols[2]));
                    row.Add(Ui.Text(string.Join(" → ", costs), "wiki-cell " + cols[3]));
                },
                Detail = page =>
                {
                    page.Add(Para(upgrade.Description));
                    page.Add(Caption($"Куплено уровней: {level} из {upgrade.MaxLevel}"));
                    page.Add(Para(string.Join(" → ", costs)));
                    if (host != null)
                    {
                        page.Add(Caption("Покупается в"));
                        page.Add(BuildingChips(new[] { host.Kind }));
                    }
                    // the deeper levels wait for a bigger building: how many each of its levels opens
                    if (host != null && upgrade.OpenLevels(1) < upgrade.MaxLevel)
                    {
                        page.Add(Caption("Ступени по уровню здания"));
                        var steps = new List<string>();
                        for (int hostLevel = 1; hostLevel <= host.MaxLevel; hostLevel++)
                            steps.Add($"Уровень {hostLevel}: {upgrade.OpenLevels(hostLevel)} из {upgrade.MaxLevel}");
                        page.Add(Para(string.Join("\n", steps)));
                    }
                }
            };
        }

        private Entry Level(BattleMissionDefinition mission)
        {
            var counts = new List<(UnitKind Kind, int Count)>();
            foreach (var enemy in mission.Enemies)
            {
                int at = counts.FindIndex(c => c.Kind == enemy.Kind);
                if (at < 0) counts.Add((enemy.Kind, 1));
                else counts[at] = (enemy.Kind, counts[at].Count + 1);
            }
            var opens = mission.UnlockUnit is UnitKind kind ? _context.Catalog.GetUnit(kind) : null;
            string reward = $"{mission.FirstWinGold}–{mission.FirstWinGoldMax}";
            var cols = Columns(WikiSection.Arena);
            return new Entry
            {
                Section = WikiSection.Arena, Key = mission.Level.ToString(), Name = mission.DisplayName,
                Picture = counts.Count > 0 ? RewardArt.Tight(_context.Catalog.GetUnit(counts[0].Kind).PortraitSprite) : null,
                Pixel = true,
                Cells = row =>
                {
                    row.Add(NameCell(mission.DisplayName, $"уровень {mission.Level}", cols[0]));
                    var foes = Ui.Box("wiki-cell wiki-icons " + cols[1]);
                    foreach (var (foe, count) in counts)
                    {
                        foes.Add(Picture(RewardArt.Tight(_context.Catalog.GetUnit(foe).PortraitSprite), true, "wiki-icon"));
                        foes.Add(Ui.Text($"×{count}", "wiki-count t-black"));
                    }
                    row.Add(foes);
                    row.Add(Price(reward, cols[2]));
                    var unlock = Ui.Box("wiki-cell wiki-icons " + cols[3]);
                    if (opens != null) unlock.Add(Picture(RewardArt.Tight(opens.PortraitSprite), true, "wiki-icon"));
                    row.Add(unlock);
                },
                Detail = page =>
                {
                    page.Add(Caption($"Уровень {mission.Level}"));
                    var foes = Ui.Box("wiki-chips");
                    foreach (var (foe, count) in counts) foes.Add(UnitChip(_context.Catalog.GetUnit(foe), $"×{count}"));
                    page.Add(foes);
                    page.Add(Caption("Награда"));
                    page.Add(Para($"Первая победа: {mission.FirstWinGold}–{mission.FirstWinGoldMax} золота. Повтор: {mission.RepeatWinGold}–{mission.RepeatWinGoldMax}."));
                    page.Add(Para($"Отряд до {mission.MaxPlayerUnits} бойцов, отдых после боя {Mathf.RoundToInt(mission.CooldownActiveSeconds)} с."));
                    if (opens != null)
                    {
                        page.Add(Caption("Победа открывает"));
                        var chips = Ui.Box("wiki-chips");
                        chips.Add(UnitChip(opens, null));
                        page.Add(chips);
                    }
                }
            };
        }

        // ---- pieces ----

        private static string Title(WikiSection section)
        {
            foreach (var (key, title) in Sections)
                if (key == section) return title;
            return string.Empty;
        }

        private BuildingDefinition Find(BuildingKind kind)
        {
            foreach (var building in _context.Catalog.Buildings)
                if (building != null && building.Kind == kind) return building;
            return null;
        }

        private static IEnumerable<BuildingKind> Kinds(List<BuildingDefinition> buildings)
        {
            foreach (var building in buildings) yield return building.Kind;
        }

        private static VisualElement Picture(Sprite sprite, bool pixel, string classes)
        {
            var disc = Ui.Box(classes);
            disc.pickingMode = PickingMode.Ignore;
            if (sprite == null) return disc;
            var art = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            art.AddToClassList(pixel ? "wiki-art wiki-art--pixel" : "wiki-art");
            disc.Add(art);
            return disc;
        }

        private static VisualElement NameCell(string name, string note, string column)
        {
            var cell = Ui.Box("wiki-cell " + column);
            cell.Add(Ui.Text(name, "wiki-name t-black"));
            if (!string.IsNullOrEmpty(note)) cell.Add(Ui.Text(note, "wiki-note t-muted"));
            return cell;
        }

        private static Label Num(string text, string column) => Ui.Text(text, "wiki-cell wiki-num t-black " + column);

        private VisualElement Price(int price, string column) => Price(price.ToString(), column);

        private VisualElement Price(string price, string column)
        {
            var cell = Ui.Box("wiki-cell wiki-price " + column);
            if (_coin != null) cell.Add(new Image { sprite = _coin, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit });
            cell.Add(Ui.Text(price, "wiki-price__value t-black"));
            return cell;
        }

        private VisualElement Favorites(UnitDefinition unit, string column)
        {
            var cell = Ui.Box("wiki-cell wiki-icons " + column);
            if (unit.FavoredBuildings.Count == 0)
            {
                cell.Add(Ui.Text("везде одинаково", "wiki-note t-muted"));
                return cell;
            }
            foreach (var kind in unit.FavoredBuildings)
            {
                var building = Find(kind);
                if (building != null) cell.Add(Picture(RewardArt.BuildingIcon(building), false, "wiki-icon"));
            }
            cell.Add(Ui.Text($"+{unit.FavoredWorkPercent}%", "wiki-bonus t-black"));
            return cell;
        }

        private static VisualElement BuildingIcons(List<BuildingDefinition> buildings, string column)
        {
            var cell = Ui.Box("wiki-cell wiki-icons " + column);
            foreach (var building in buildings) cell.Add(Picture(RewardArt.BuildingIcon(building), false, "wiki-icon"));
            return cell;
        }

        private VisualElement Recipe(ProductionRecipe recipe, string column)
        {
            var cell = Ui.Box("wiki-cell wiki-icons " + column);
            void Amounts(ResourceAmount[] amounts)
            {
                foreach (var amount in amounts)
                {
                    cell.Add(Picture(_context.Catalog.TryGetResource(amount.Resource)?.Icon, false, "wiki-icon"));
                    cell.Add(Ui.Text(amount.Amount.ToString(), "wiki-count t-black"));
                }
            }
            Amounts(recipe.Inputs);
            if (recipe.Inputs.Length > 0) cell.Add(Ui.Text("→", "wiki-arrow t-muted"));
            Amounts(recipe.Outputs);
            return cell;
        }

        private static Label Caption(string text) => Ui.Text(text, "wiki-caption t-bold");

        private static Label Para(string text)
        {
            var label = Ui.Text(text, "wiki-para");
            label.AddToClassList("t-wrap");
            return label;
        }

        private static VisualElement Chips(params (string Glyph, string Value, string Word)[] chips)
        {
            var row = Ui.Box("wiki-chips");
            foreach (var (glyph, value, word) in chips)
            {
                var chip = Ui.Box("wiki-chip");
                if (glyph != null) chip.Add(Ui.Box("glyph glyph--" + glyph));
                chip.Add(Ui.Text(value, "wiki-chip__value t-black"));
                if (word != null) chip.Add(Ui.Text(word, "wiki-chip__word t-muted"));
                row.Add(chip);
            }
            return row;
        }

        // a building's chip opens that building's page
        private VisualElement BuildingChips(IEnumerable<BuildingKind> kinds)
        {
            var row = Ui.Box("wiki-chips");
            foreach (var kind in kinds)
            {
                var building = Find(kind);
                if (building == null) continue;
                var chip = Ui.TextButton(string.Empty, "btn wiki-chip wiki-chip--link");
                chip.Add(Picture(RewardArt.BuildingIcon(building), false, "wiki-icon"));
                chip.Add(Ui.Text(building.DisplayName, "wiki-chip__word"));
                var target = kind;
                if (building.Constructible) UiFeel.Bind(chip, () => Go(WikiSection.Buildings, target.ToString()));
                else UiFeel.Bind(chip, () => { });
                row.Add(chip);
            }
            return row;
        }

        private static VisualElement UnitChip(UnitDefinition unit, string note)
        {
            var chip = Ui.Box("wiki-chip");
            chip.Add(Picture(RewardArt.Tight(unit.PortraitSprite), true, "wiki-icon"));
            chip.Add(Ui.Text(unit.DisplayName, "wiki-chip__word"));
            if (!string.IsNullOrEmpty(note)) chip.Add(Ui.Text(note, "wiki-bonus t-black"));
            return chip;
        }
    }
}
