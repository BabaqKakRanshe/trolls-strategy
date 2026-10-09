using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TrollStrategy.Application;
using TrollStrategy.Bootstrap;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Turns each of the bot's commands into what a player does at the colony HUD and on the map: the catalog's tab,
    /// page and token, then the cell; the creatures picked one by one, then «Работа» and the building; the card and
    /// its «Улучшить»; the arena, the squad on the board and «В бой». Whatever the HUD then sends to the session is
    /// the bot's move. Each step waits for what a player waits for (a panel to slide in, a reward to be ready) and
    /// fails with <see cref="GestureFailed"/> when the HUD does not offer what it needs.
    /// </summary>
    internal sealed class BotUiHands
    {
        private readonly GameBootstrap _game;
        private readonly BotHand _hand;
        private readonly TilemapWorldView _world;
        private readonly UnitVisualsManager _units;
        private readonly BuildingVisualsManager _buildings;
        private readonly ColonyHud _hud;

        public BotUiHands(GameBootstrap game, BotHand hand)
        {
            _game = game;
            _hand = hand;
            _world = UnityEngine.Object.FindAnyObjectByType<TilemapWorldView>();
            _units = UnityEngine.Object.FindAnyObjectByType<UnitVisualsManager>();
            _buildings = UnityEngine.Object.FindAnyObjectByType<BuildingVisualsManager>();
            _hud = UnityEngine.Object.FindAnyObjectByType<ColonyHud>();
            _hand.Rig = () => _game != null && _game.enabled && _game.ColonyCamera != null
                ? _game.ColonyCamera.GetComponent<IslandCameraRig>()
                : null;
        }

        /// <summary>Skips what a player may skip with a click: reward reveals, the reel of a battle prize, the replay.</summary>
        public bool SkipAnimations { get; set; }

        /// <summary>A move the hands made on their own to clear the screen (a reward that opened by itself).</summary>
        public event Action<string> SideMove;

        private GameSession Session => _game.Session;
        private InteractionController Interaction => _game.Interaction;
        private ColonyHudView View => _hud != null ? _hud.View : null;
        private Camera ColonyCamera => _game.ColonyCamera;
        private GameSnapshot Snapshot => Session.CurrentSnapshot;

        /// <summary>The gesture for one command; null when the command has none (it then goes to the session).</summary>
        public IEnumerator Perform(IGameCommand command) => command switch
        {
            ClaimQuestRewardCommand => ClaimQuest(),
            BuildBuildingCommand build => Build(build.Kind, build.Cell),
            BuyUnitsCommand buy => Hire(buy.UnitKind, buy.Amount, buy.Cell),
            HireWorkerCommand hire => HireWorker(hire),
            AssignWorkCommand work => Work(work.UnitIds, work.BuildingId),
            AssignHaulCommand haul => Haul(haul),
            UpgradeBuildingCommand upgrade => UpgradeBuilding(upgrade.BuildingId),
            BuyUpgradeCommand colony => BuyUpgrade(colony.UpgradeId),
            BuyLandCommand land => Land(land.BlockX, land.BlockY, true),
            ClearLandCommand clear => Land(clear.BlockX, clear.BlockY, false),
            StartBattleCommand battle => Battle(battle),
            AcknowledgeBattleCommand => LeaveBattle(),
            ClaimBattleRewardCommand => ClaimBattleReward(),
            ReleaseUnitsCommand release => Release(release.UnitIds),
            MoveBuildingCommand move => MoveBuilding(move.BuildingId, move.Cell),
            SellUnitsCommand sell => Sell(sell.UnitIds),
            BotPeek => ReadBook(),
            _ => null
        };

        // ---------- the screen before a move

        /// <summary>
        /// Clears what a player clears before the next move: the menu or book, a reward that opened by itself, a dialog
        /// of an order left behind. Leaves alone what <paramref name="next"/> needs.
        /// </summary>
        public IEnumerator Settle(IGameCommand next)
        {
            yield return _hand.Until(() => View != null || BattleView != null, 5f, "интерфейс колонии");
            var view = View;
            if (view == null || (BattleHudOpen && next is StartBattleCommand or AcknowledgeBattleCommand)) yield break;
            if (view.Menu.IsOpen) yield return _hand.Press(view.Menu.ContinueButton, "Продолжить");
            if (view.Wiki?.IsOpen == true) yield return _hand.Key(Key.K, "K");
            if (view.Reward.IsOpen && next is not ClaimQuestRewardCommand)
            {
                SideMove?.Invoke("Окно награды открылось само: забираю награду");
                yield return _hand.Exclusive(TakeQuestReward());
            }
            // a prize waits while its battle is still open (a battle started past the HUD): it cannot be taken yet
            if (view.BattleReward.IsOpen && Session.ActiveBattle == null && next is not ClaimBattleRewardCommand)
            {
                SideMove?.Invoke("Окно приза открылось само: забираю приз");
                yield return _hand.Exclusive(TakeBattleReward());
            }
            if (view.HaulCargo.IsOpen) yield return _hand.Press(view.HaulCargo.CancelButton, "Отмена");
            if (view.Arena.IsOpen && next is not StartBattleCommand)
                yield return _hand.Press(view.Root.Q<Button>("arena-close"), "закрыть арену");
        }

        /// <summary>No order half given and nobody selected: Esc as many times as it takes.</summary>
        private IEnumerator Neutral()
        {
            for (int i = 0; i < 4 && (Interaction.Mode.Type != InteractionModeType.Neutral || Interaction.SelectedIds.Count > 0); i++)
                yield return _hand.Key(Key.Escape, "Esc");
            if (Interaction.Mode.Type != InteractionModeType.Neutral || Interaction.SelectedIds.Count > 0)
                throw new GestureFailed("Esc не снял выбор и режим");
        }

        private IEnumerator CloseCard()
        {
            if (View != null && View.Inspect.IsShown)
            {
                yield return _hand.Press(View.Root.Q<Button>("inspect-close"), "закрыть карточку");
                yield return _hand.Until(() => !View.Inspect.IsShown, 1.5f, "карточка закрылась");
            }
        }

        // ---------- catalog

        private IEnumerator Catalog(bool units)
        {
            var catalog = View.Catalog;
            if (!catalog.IsOpen || catalog.IsCovered)
            {
                yield return _hand.Press(View.TopBar.CatalogButton, "Каталог");
                yield return _hand.Until(() => View.Catalog.IsOpen && !View.Catalog.IsCovered, 2f, "каталог открылся");
            }
            if (View.Catalog.ShowsUnits != units)
            {
                yield return _hand.Press(View.Root.Q<Button>(units ? "tab-units" : "tab-buildings"),
                    units ? "вкладка «Существа»" : "вкладка «Постройки»");
                yield return _hand.Until(() => View.Catalog.ShowsUnits == units, 1.5f, "вкладка открылась");
            }
            // the band slides in
            yield return _hand.Wait(0.3f);
        }

        /// <summary>
        /// Turns the wheel over a list until the item shows. Which way the wheel moves a list is the list's own matter:
        /// when the list does not move, the hand turns the other way.
        /// </summary>
        private IEnumerator ScrollTo(ScrollView list, Func<Button> item, float way, string what)
        {
            for (int turn = 0; turn < 16 && list != null && BotHand.Of(item()) == null; turn++)
            {
                var before = list.scrollOffset;
                yield return _hand.Scroll(() => BotHand.Of(list), way, what);
                yield return _hand.Wait(0.1f);
                if ((list.scrollOffset - before).sqrMagnitude < 0.25f) way = -way;
            }
        }

        /// <summary>Turns the catalog's pages until the token shows.</summary>
        private IEnumerator Page(Func<Button> token, CatalogPager pager, string what)
        {
            bool forward = true;
            for (int turns = 0; turns < 16; turns++)
            {
                yield return _hand.Checkpoint();
                if (BotHand.Of(token()) != null) yield break;
                if (!pager.HasPages) break;
                var arrow = forward ? pager.NextButton : pager.PrevButton;
                if (!UiFeel.IsAvailable(arrow))
                {
                    if (!forward) break;
                    forward = false;
                    continue;
                }
                yield return _hand.Press(arrow, forward ? "следующая страница" : "предыдущая страница");
                yield return _hand.Wait(0.25f);
            }
            if (BotHand.Of(token()) == null) throw new GestureFailed($"в каталоге не видно: {what}");
        }

        private IEnumerator Build(BuildingKind kind, Cell cell)
        {
            string name = Session.Catalog.GetBuilding(kind).DisplayName;
            yield return Neutral();
            yield return CloseCard();
            yield return Catalog(false);
            Button Token() => View.Catalog.BuyButton(kind);
            if (Token() == null) throw new GestureFailed($"в каталоге нет постройки «{name}»");
            yield return Page(Token, View.Catalog.BuildingPages, name);
            yield return _hand.Press(Token(), name);
            yield return _hand.Until(() => Interaction.Mode.Type == InteractionModeType.PlacingBuilding &&
                                           Interaction.Mode.BuildingKind == kind, 1.5f, "режим постройки");
            yield return _hand.Wait(0.2f);
            yield return _hand.ClickWorld(() => CellPoint(cell), $"место для «{name}»");
        }

        private IEnumerator Hire(UnitKind kind, int amount, Cell cell)
        {
            string name = Session.Catalog.GetUnit(kind).DisplayName;
            yield return Neutral();
            yield return CloseCard();
            yield return Catalog(true);
            for (int i = 0; i < 25 && View.Catalog.HireAmount != amount; i++)
            {
                bool more = View.Catalog.HireAmount < amount;
                yield return _hand.Press(View.Root.Q<Button>(more ? "hire-more" : "hire-less"), more ? "больше" : "меньше");
            }
            if (View.Catalog.HireAmount != amount) throw new GestureFailed($"счётчик найма не встал на {amount}");
            Button Token() => View.Catalog.HireButton(kind);
            if (Token() == null) throw new GestureFailed($"в каталоге нет существа «{name}»");
            yield return Page(Token, View.Catalog.UnitPages, name);
            yield return _hand.Press(Token(), name);
            yield return _hand.Until(() => Interaction.Mode.Type == InteractionModeType.PlacingUnits &&
                                           Interaction.Mode.UnitKind == kind, 1.5f, "режим найма");
            yield return _hand.Wait(0.2f);
            yield return _hand.ClickWorld(() => CellPoint(cell), "клетка для найма");
        }

        // the card's "hire here" is gone: a player hires in the catalog and then sends the new creature to work
        private IEnumerator HireWorker(HireWorkerCommand command)
        {
            var before = new HashSet<string>(Snapshot.Units.Select(u => u.Id));
            yield return Hire(command.UnitKind, 1, command.Cell);
            yield return _hand.Until(() => Snapshot.Units.Any(u => !before.Contains(u.Id)), 2f, "нанятое существо");
            var hired = Snapshot.Units.First(u => !before.Contains(u.Id)).Id;
            yield return _hand.Wait(0.3f);
            yield return Work(new[] { hired }, command.BuildingId);
        }

        // ---------- creatures and orders

        /// <summary>
        /// Selects the creatures by clicking each (Ctrl for the second on). The HUD does not tell creatures of a kind
        /// apart, nor does the order: in a crowd the hand takes as many of each kind as meant, the ones meant first and
        /// those standing apart from the others before those in a heap (a click in a heap may take or drop a neighbour).
        /// </summary>
        private IEnumerator Select(IReadOnlyList<string> ids)
        {
            yield return Neutral();
            yield return CloseCard();
            var kinds = Snapshot.Units.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id, u => u.UnitKind);
            if (kinds.Count != ids.Count) throw new GestureFailed("существа уже нет в колонии");
            var need = kinds.Values.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
            for (int click = 0; click < ids.Count * 4 + 4; click++)
            {
                var have = Selected();
                // a click took a whole heap (the game read it as a double click): start the choice over
                if (have.Any(h => !need.TryGetValue(h.Key, out int wanted) || h.Value > wanted))
                {
                    yield return _hand.Key(Key.Escape, "Esc");
                    yield return _hand.Wait(0.45f);
                    continue;
                }
                var missing = need.Where(n => (have.TryGetValue(n.Key, out int got) ? got : 0) < n.Value).ToList();
                if (missing.Count == 0) break;
                string pick = Candidates(missing[0].Key, ids).FirstOrDefault();
                if (pick == null) break;
                var target = UnitPoint(pick);
                // two clicks on creatures at one spot within 0.35 s are a double click (the whole heap there), even
                // across moves: the second waits
                if (_lastCreatureClick != null && target != null && Vector2.Distance(_lastCreatureClick.Value, target.Value) < 24f)
                {
                    float gap = 0.45f - (Time.realtimeSinceStartup - _lastCreatureClickAt);
                    if (gap > 0f) yield return _hand.Wait(gap);
                }
                yield return _hand.ClickWorld(() => UnitPoint(pick), "существо", ctrl: Interaction.SelectedIds.Count > 0);
                _lastCreatureClick = _hand.Position;
                _lastCreatureClickAt = Time.realtimeSinceStartup;
                yield return null;
            }
            var chosen = Selected();
            if (chosen.Count != need.Count || need.Any(n => !chosen.TryGetValue(n.Key, out int got) || got != n.Value))
                throw new GestureFailed($"выбрано {Interaction.SelectedIds.Count} существ вместо {ids.Count}");
        }

        private Vector2? _lastCreatureClick;
        private float _lastCreatureClickAt;

        // the selection by kind
        private Dictionary<UnitKind, int> Selected() => Snapshot.Units.Where(u => Interaction.SelectedIds.Contains(u.Id))
            .GroupBy(u => u.UnitKind).ToDictionary(g => g.Key, g => g.Count());

        // creatures of the kind not yet selected that the map lets a player pick: the meant ones first, then free ones,
        // each group the most apart from the others first
        private IEnumerable<string> Candidates(UnitKind kind, IReadOnlyList<string> meant)
        {
            var selectable = Interaction.SelectableUnitIds();
            var feet = Snapshot.Units.ToDictionary(u => u.Id, u => new Vector2(u.Position.X, u.Position.Y));
            float Room(string id) => feet.Where(f => f.Key != id).Select(f => Vector2.Distance(f.Value, feet[id]))
                .DefaultIfEmpty(99f).Min();
            return Snapshot.Units
                .Where(u => u.UnitKind == kind && selectable.Contains(u.Id) && !Interaction.SelectedIds.Contains(u.Id) &&
                            UnitPoint(u.Id) != null)
                .OrderBy(u => meant.Contains(u.Id) ? 0 : u.Assignment.Kind == AssignmentKind.Idle ? 1 : 2)
                .ThenByDescending(u => Room(u.Id))
                .Select(u => u.Id)
                .ToList();
        }

        /// <summary>A reward window that popped up over the move, unless the move is about it.</summary>
        public IEnumerator Interruption(IGameCommand meant)
        {
            var view = View;
            if (view == null) return null;
            if (view.Reward.IsOpen && meant is not ClaimQuestRewardCommand)
            {
                SideMove?.Invoke("Окно награды открылось само: забираю награду");
                return TakeQuestReward();
            }
            if (view.BattleReward.IsOpen && Session.ActiveBattle == null &&
                meant is not (ClaimBattleRewardCommand or AcknowledgeBattleCommand))
            {
                SideMove?.Invoke("Окно приза открылось само: забираю приз");
                return TakeBattleReward();
            }
            return null;
        }

        private IEnumerator Work(IReadOnlyList<string> ids, string buildingId)
        {
            yield return Select(ids);
            yield return Order(View.ContextBar.WorkButton, Key.E, "Работа", InteractionModeType.ChoosingWorkTarget);
            yield return PickTarget(buildingId);
        }

        private IEnumerator Haul(AssignHaulCommand command)
        {
            yield return Select(command.UnitIds);
            yield return Order(View.ContextBar.HaulButton, Key.H, "Перенос", InteractionModeType.ChoosingHaulSource);
            yield return PickTarget(command.SourceId);
            yield return _hand.Until(() => View.HaulCargo.IsOpen, 2f, "окно груза");
            yield return _hand.Wait(0.3f);
            var dialog = View.HaulCargo;
            if (command.Cargo == null || command.Cargo.Count == 0)
                yield return _hand.Press(dialog.CarryAllButton, "Всё");
            else
                foreach (var resource in command.Cargo)
                {
                    string name = Session.ResourceName(resource);
                    if (!dialog.CargoKinds.Contains(resource) && dialog.OffersFilter)
                        yield return _hand.Press(dialog.AllGoodsButton, "Все товары");
                    int index = dialog.CargoKinds.ToList().IndexOf(resource);
                    if (index < 0) throw new GestureFailed($"в окне груза нет товара «{name}»");
                    Button Card() => dialog.CargoButtons[index];
                    yield return ScrollTo(View.Root.Q<ScrollView>("haul-cargo-scroll"), Card, -1f, "список товаров");
                    if (!Card().ClassListContains("is-on")) yield return _hand.Press(Card(), name);
                }
            yield return _hand.Press(dialog.ConfirmButton, "Куда носить");
            yield return _hand.Until(() => Interaction.Mode.Type == InteractionModeType.ChoosingHaulDestination, 1.5f, "выбор, куда носить");
            yield return PickTarget(command.DestinationId);
        }

        /// <summary>
        /// Picks the building an order waits for: on the map where it is in plain view, else with its token on the
        /// bar, which lists the buildings the order can take (with many buildings the tokens cover the island's middle).
        /// </summary>
        private IEnumerator PickTarget(string buildingId)
        {
            Button Token()
            {
                int index = Interaction.GetTargetBuildingIds().ToList().IndexOf(buildingId);
                var tokens = View.ContextBar.TargetButtons;
                return index >= 0 && index < tokens.Count ? tokens[index] : null;
            }
            var point = BuildingPoint(buildingId, false);
            if ((point == null || !BotHand.Clear(point.Value)) && BotHand.Of(Token()) != null)
            {
                yield return _hand.Press(Token(), BuildingName(buildingId));
                yield break;
            }
            yield return _hand.ClickWorld(() => BuildingPoint(buildingId, false), BuildingName(buildingId));
        }

        // Once the release of an order button has fallen through to the map, the hand gives orders by key, as a player
        // who noticed it would.
        private bool _ordersByKey;

        /// <summary>
        /// Starts an order for the selected creatures with its button on the bar, and checks that the order now waits
        /// for its building. The button's release must not pick a building itself: the bar changes in that very frame,
        /// and a map that takes a release begun over the HUD picks whatever building stood behind the button.
        /// </summary>
        private IEnumerator Order(Button button, Key key, string name, InteractionModeType waits)
        {
            var before = new HashSet<string>(Interaction.SelectedIds);
            if (_ordersByKey) yield return _hand.Key(key, key.ToString());
            else yield return _hand.Press(button, name);
            yield return _hand.Wait(0.15f);
            if (Interaction.Mode.Type == waits) yield break;
            bool fellThrough = Interaction.Mode.Type == InteractionModeType.ChoosingHaulCargo ||
                               (waits == InteractionModeType.ChoosingWorkTarget && Interaction.Mode.Type == InteractionModeType.Neutral &&
                                !before.SetEquals(Interaction.SelectedIds));
            if (fellThrough && !_ordersByKey)
            {
                _ordersByKey = true;
                throw new GestureFailed($"отпускание кнопки «{name}» провалилось на карту и выбрало здание под кнопкой " +
                                        "(карта принимает отпускание, нажатое над HUD); дальше бот отдаёт этот приказ клавишей");
            }
            yield return _hand.Until(() => Interaction.Mode.Type == waits, 1.5f, $"приказ «{name}» ждёт здание");
        }

        private IEnumerator Release(IReadOnlyList<string> ids)
        {
            yield return Select(ids);
            yield return _hand.Key(Key.R, "R");
        }

        // ---------- buildings

        private IEnumerator OpenCard(string buildingId)
        {
            if (View.Inspect.IsShown && Interaction.InspectedBuildingId == buildingId) yield break;
            yield return Neutral();
            yield return _hand.ClickWorld(() => BuildingPoint(buildingId, true), BuildingName(buildingId));
            yield return _hand.Until(() => View.Inspect.IsShown && Interaction.InspectedBuildingId == buildingId, 1.5f,
                $"карточка «{BuildingName(buildingId)}»");
            yield return _hand.Wait(0.35f);
        }

        private IEnumerator UpgradeBuilding(string buildingId)
        {
            yield return OpenCard(buildingId);
            var button = View.Inspect.Actions.FirstOrDefault(b => b.ClassListContains("btn--primary"));
            if (button == null) throw new GestureFailed("в карточке нет «Улучшить»");
            yield return _hand.Press(button, "Улучшить");
            yield return _hand.Wait(0.4f);
            yield return CloseCard();
        }

        private IEnumerator BuyUpgrade(string upgradeId)
        {
            var upgrade = Snapshot.Upgrades.FirstOrDefault(u => u.Id == upgradeId);
            if (upgrade == null) throw new GestureFailed("нет такого улучшения");
            var host = Snapshot.Buildings.Where(b => b.Kind == upgrade.Host).OrderByDescending(b => b.Level).FirstOrDefault();
            if (host == null) throw new GestureFailed($"нет здания для улучшения «{upgrade.Name}»");
            yield return OpenCard(host.Id);
            int index = Snapshot.Upgrades.Where(u => u.Host == upgrade.Host).Select(u => u.Id).ToList().IndexOf(upgradeId);
            Button Buy() => index >= 0 && index < View.Inspect.UpgradeButtons.Count ? View.Inspect.UpgradeButtons[index] : null;
            if (Buy() == null) throw new GestureFailed($"в карточке нет улучшения «{upgrade.Name}»");
            var card = View.Root.Q("inspect-panel") ?? View.Inspect.UpgradeButtons[0].parent;
            for (int i = 0; i < 8 && BotHand.Of(Buy()) == null; i++)
                yield return _hand.Scroll(() => BotHand.Of(card) ?? BotHand.Of(Buy().parent), -1f, "карточка");
            yield return _hand.Press(Buy(), upgrade.Name);
            yield return _hand.Wait(0.4f);
            yield return CloseCard();
        }

        // ---------- a newcomer's moves

        private IEnumerator MoveBuilding(string buildingId, Cell cell)
        {
            string name = BuildingName(buildingId);
            yield return OpenCard(buildingId);
            var actions = View.Inspect.Actions;
            if (actions.Count == 0) throw new GestureFailed("в карточке нет «Перенести»");
            yield return _hand.Press(actions[0], "Перенести");
            yield return _hand.Until(() => Interaction.Mode.Type == InteractionModeType.MovingBuilding, 1.5f, "режим переноса");
            yield return _hand.Wait(0.2f);
            yield return _hand.ClickWorld(() => CellPoint(cell), $"новое место для «{name}»");
        }

        private IEnumerator Sell(IReadOnlyList<string> ids)
        {
            yield return Select(ids);
            yield return _hand.Press(View.ContextBar.SellButton, "Продать");
        }

        // the book: opened from its tool, a moment to read, closed with its cross
        private IEnumerator ReadBook()
        {
            yield return Neutral();
            yield return CloseCard();
            if (View.Wiki == null) throw new GestureFailed("в этой сборке нет справочника");
            if (!View.Wiki.IsOpen)
            {
                yield return _hand.Press(View.TopBar.WikiButton, "Справочник");
                yield return _hand.Until(() => View.Wiki.IsOpen, 2f, "справочник открылся");
            }
            yield return _hand.Wait(SkipAnimations ? 0.8f : 2.5f);
            yield return _hand.Press(View.Root.Q<Button>("wiki-close"), "закрыть справочник");
            yield return _hand.Until(() => !View.Wiki.IsOpen, 2f, "справочник закрылся");
        }

        // ---------- land

        private IEnumerator Land(int x, int y, bool buy)
        {
            yield return CloseCard();
            if (Interaction.Mode.Type != InteractionModeType.ManagingLand)
            {
                yield return Neutral();
                yield return _hand.Press(View.TopBar.LandButton, "Земля");
                yield return _hand.Until(() => Interaction.Mode.Type == InteractionModeType.ManagingLand, 1.5f, "режим земли");
                yield return _hand.Wait(0.3f);
            }
            var offer = buy ? LandOffer.Buy : LandOffer.Clear;
            yield return _hand.ClickWorld(() => BlockPoint(x, y), "участок земли");
            yield return _hand.Until(() => Interaction.Mode.LandOffer == offer && Interaction.Mode.LandX == x &&
                                           Interaction.Mode.LandY == y, 1.5f, buy ? "предложение купить" : "предложение расчистить");
            yield return _hand.Press(View.ContextBar.LandButton, buy ? "Купить" : "Расчистить");
            yield return _hand.Wait(0.5f);
            if (Interaction.Mode.Type == InteractionModeType.ManagingLand)
                yield return _hand.Press(View.TopBar.LandButton, "Земля");
        }

        // ---------- quests and rewards

        private IEnumerator ClaimQuest()
        {
            if (!View.Reward.IsOpen)
            {
                yield return Neutral();
                if (View.Quest.IsCollapsed) yield return _hand.Key(Key.Q, "Q");
                yield return _hand.Press(View.Quest.ClaimButton, "Забрать награду");
                yield return _hand.Until(() => View.Reward.IsOpen, 2f, "окно награды");
            }
            yield return TakeQuestReward();
        }

        private IEnumerator TakeQuestReward()
        {
            // two quests done at once: the window shows the next reward as soon as the first is taken
            for (int reward = 0; reward < 6 && View.Reward.IsOpen; reward++)
            {
                string shown = View.Reward.QuestId;
                // a press on the reward's stage shows it at once
                if (SkipAnimations && !View.Reward.IsReady)
                {
                    var stage = View.Root.Q("reward-stage");
                    if (BotHand.Of(stage) != null) yield return _hand.Click(() => BotHand.Of(stage), "пропустить показ");
                }
                yield return _hand.Until(() => View.Reward.IsReady, 6f, "награда готова");
                yield return _hand.Press(View.Reward.ClaimButton, "Забрать");
                yield return _hand.Until(() => !View.Reward.IsOpen || View.Reward.QuestId != shown, 3f, "окно награды закрылось");
                yield return _hand.Wait(0.3f);
            }
        }

        private IEnumerator ClaimBattleReward()
        {
            yield return _hand.Until(() => View != null && View.BattleReward.IsOpen, 5f, "окно приза");
            yield return TakeBattleReward();
        }

        private IEnumerator TakeBattleReward()
        {
            // a press on the reels stops them at once
            if (SkipAnimations && !View.BattleReward.IsReady)
            {
                var machine = View.Root.Q("battle-reward-machine");
                yield return _hand.Wait(0.3f);
                if (BotHand.Of(machine) != null) yield return _hand.Click(() => BotHand.Of(machine), "остановить барабаны");
            }
            yield return _hand.Until(() => View.BattleReward.IsReady, 8f, "приз готов");
            yield return _hand.Press(View.BattleReward.ClaimButton, "Забрать золото");
            yield return _hand.Until(() => !View.BattleReward.IsOpen, 4f, "окно приза закрылось");
        }

        // ---------- the arena and the battle

        private BattleHud BattleHud => UnityEngine.Object.FindAnyObjectByType<BattleHud>();
        private BattleHudView BattleView => BattleHud != null && BattleHud.IsOpen ? BattleHud.View : null;
        private bool BattleHudOpen => BattleView != null;

        private static readonly FieldInfo DeploymentField =
            typeof(BattleHud).GetField("_deployment", BindingFlags.Instance | BindingFlags.NonPublic);

        private BattleDeployment Deployment => BattleHud != null ? DeploymentField?.GetValue(BattleHud) as BattleDeployment : null;

        private IEnumerator Battle(StartBattleCommand command)
        {
            var colony = Snapshot;
            if (!BattleHudOpen)
            {
                yield return Neutral();
                yield return CloseCard();
                if (!View.Arena.IsOpen)
                {
                    if (UiFeel.IsAvailable(View.TopBar.BattleButton))
                        yield return _hand.Press(View.TopBar.BattleButton, "В бой");
                    else
                        yield return _hand.Key(Key.V, "V");
                    yield return _hand.Until(() => View.Arena.IsOpen, 2f, "окно арены");
                    yield return _hand.Wait(0.4f);
                }
                var ladder = Session.ArenaLadder().ToList();
                int index = ladder.FindIndex(m => m.MissionId == command.MissionId);
                if (index < 0) throw new GestureFailed("нет такого уровня арены");
                if (View.Arena.Chosen?.MissionId != command.MissionId)
                {
                    Button Disc() => index < View.Arena.LevelButtons.Count ? View.Arena.LevelButtons[index] : null;
                    var rail = View.Root.Q<ScrollView>("arena-levels");
                    int chosen = ladder.FindIndex(m => m.MissionId == View.Arena.Chosen?.MissionId);
                    yield return ScrollTo(rail, Disc, index > chosen ? -1f : 1f, "уровни арены");
                    yield return _hand.Press(Disc(), $"уровень {ladder[index].Level}");
                    yield return _hand.Until(() => View.Arena.Chosen?.MissionId == command.MissionId, 1.5f, "уровень выбран");
                    yield return _hand.Wait(0.4f);
                }
                yield return _hand.Press(View.Arena.FightButton, "В бой");
                yield return _hand.Until(() => BattleView != null && BattleView.IsDeploying, 6f, "расстановка");
                yield return _hand.Wait(0.8f);
            }

            var deployment = Deployment ?? throw new GestureFailed("расстановка без состояния");
            var board = UnityEngine.Object.FindAnyObjectByType<BattleBoardView>();
            var camera = Camera.main;
            foreach (var placement in command.Placements)
            {
                var unit = colony.Units.FirstOrDefault(u => u.Id == placement.UnitId);
                if (unit == null) throw new GestureFailed("бойца нет в колонии");
                if (deployment.PickedKind != unit.UnitKind)
                {
                    yield return _hand.Press(BattleView.Squad.KindButton(unit.UnitKind), Session.Catalog.GetUnit(unit.UnitKind).DisplayName);
                    yield return _hand.Until(() => deployment.PickedKind == unit.UnitKind, 1f, "вид бойца выбран");
                }
                int before = deployment.Placements.Count;
                var cell = placement.Cell;
                yield return _hand.Click(() => BoardPoint(board, camera, cell), $"клетка ({cell.X}, {cell.Y})");
                yield return _hand.Until(() => deployment.Placements.Count == before + 1, 1.5f, "боец на поле");
                string fighter = deployment.SelectedUnitId;
                foreach (var item in command.Equipment.Where(e => e.OwnerUnitId == placement.UnitId))
                {
                    var definition = colony.Equipment.FirstOrDefault(e => e.Id == item.ItemId)?.DefinitionId;
                    if (definition == null) continue;
                    bool wears = colony.Equipment.Any(e => e.DefinitionId == definition && deployment.OwnerOf(e.Id) == fighter);
                    if (wears) continue;
                    Button Item() => BattleView.Gear.ItemButton(item.ItemId);
                    yield return _hand.Until(() => BattleView.Gear.IsShown, 1f, "снаряжение бойца");
                    yield return Page(Item, BattleView.Gear.Pages, "предмет");
                    yield return _hand.Press(Item(), "предмет");
                }
            }
            yield return _hand.Wait(0.3f);
            yield return _hand.Press(BattleView.Actions.Start, "В бой");
        }

        private IEnumerator LeaveBattle()
        {
            // a battle the session started without its screen (the HUD failed the start) has nothing to watch or close
            if (BattleView == null && Session.ActiveBattle != null)
                throw new GestureFailed("бой начат мимо интерфейса: экрана боя нет");
            yield return _hand.Until(() => BattleView != null, 3f, "экран боя");
            var replay = BattleView.Replay;
            if (SkipAnimations)
            {
                // the outcome is settled at the start; «назад» leaves the replay and closes the battle at once
                yield return _hand.Wait(0.6f);
                yield return _hand.Press(BattleView.Header.Back, "Пропустить бой");
                yield return _hand.Until(() => BattleView == null && View != null, 4f, "колония");
                yield return _hand.Wait(0.3f);
                yield break;
            }
            // the replay at twice the speed: long enough to see the fight, short enough for the video
            if (BotHand.Of(replay.SpeedButton(2f)) != null) yield return _hand.Press(replay.SpeedButton(2f), "×2");
            yield return _hand.Until(() => BotHand.Of(BattleView.Replay.Return) != null, 150f, "конец боя");
            yield return _hand.Wait(1.2f);
            yield return _hand.Press(BattleView.Replay.Return, "В колонию");
            yield return _hand.Until(() => BattleView == null && View != null, 4f, "колония");
            yield return _hand.Wait(0.5f);
        }

        // ---------- targets

        private string BuildingName(string id) => Snapshot.Buildings.FirstOrDefault(b => b.Id == id)?.Name ?? "здание";

        private Vector2? CellPoint(Cell cell)
        {
            float size = Session.Catalog.Economy.CellSize;
            return BotHand.World(ColonyCamera, _world.MapToWorld(new Vector3((cell.X + 0.5f) * size, (cell.Y + 0.5f) * size, 0f)));
        }

        private Vector2? BlockPoint(int x, int y)
        {
            var land = Snapshot.Land;
            if (land == null) return null;
            float size = Session.Catalog.Economy.CellSize;
            return BotHand.World(ColonyCamera, _world.MapToWorld(new Vector3((x + 0.5f) * land.BlockSize * size,
                (y + 0.5f) * land.BlockSize * size, 0f)));
        }

        private Vector2? UnitPoint(string id)
        {
            if (_units == null || !_units.Views.TryGetValue(id, out var view) || view == null || !view.gameObject.activeInHierarchy)
                return null;
            return BotHand.World(ColonyCamera, view.transform.position);
        }

        /// <summary>
        /// A point on the building's footprint: its middle, or with <paramref name="awayFromCreatures"/> the cell of
        /// it farthest from any creature (a click on a creature would pick the creature, not open the card).
        /// </summary>
        private Vector2? BuildingPoint(string id, bool awayFromCreatures)
        {
            var building = Snapshot.Buildings.FirstOrDefault(b => b.Id == id);
            if (building == null) return null;
            float size = Session.Catalog.Economy.CellSize;
            var best = new Vector2(building.Cell.X + building.Width * 0.5f, building.Cell.Y + building.Height * 0.5f);
            if (awayFromCreatures && _units != null)
            {
                var feet = _units.Views.Values.Where(v => v != null && v.gameObject.activeInHierarchy)
                    .Select(v => (Vector2)_world.WorldToMap(v.transform.position)).ToList();
                float Room(Vector2 p) => feet.Count == 0 ? float.MaxValue : feet.Min(f => Vector2.Distance(f, p * size));
                float room = Room(best);
                for (int cx = 0; cx < building.Width; cx++)
                for (int cy = 0; cy < building.Height; cy++)
                {
                    var point = new Vector2(building.Cell.X + cx + 0.5f, building.Cell.Y + cy + 0.5f);
                    if (room >= 0.9f) break;
                    float r = Room(point);
                    if (r > room)
                    {
                        room = r;
                        best = point;
                    }
                }
            }
            return BotHand.World(ColonyCamera, _world.MapToWorld(new Vector3(best.x * size, best.y * size, 0f)));
        }

        private static Vector2? BoardPoint(BattleBoardView board, Camera camera, Cell cell)
        {
            if (board == null || camera == null) return null;
            var point = BotHand.World(camera, board.CellWorldPosition(cell));
            if (point == null || UIInputUtils.IsOverDocument(point.Value)) return null;
            return board.TryCellAt(point.Value, out var hit) && hit.Equals(cell) ? point : null;
        }
    }
}
