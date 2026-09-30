#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
using System;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>Editor and development-build shortcuts (F1). Gameplay changes still use session commands.</summary>
    public sealed class CheatPanel
    {
        private const int GoldGrant = 1000;

        private readonly ColonyHudContext _context;
        private readonly VisualElement _overlay;
        private readonly Label _status;

        public CheatPanel(VisualElement root, ColonyHudContext context)
        {
            _context = context;
            _overlay = Ui.Require<VisualElement>(root, "cheat-overlay");
            _status = Ui.Require<Label>(root, "cheat-status");
            UiFeel.Bind(Ui.Require<Button>(root, "cheat-close"), Hide, Sfx.UiBack);
            UiFeel.Bind(Ui.Require<Button>(root, "cheat-prepare"), OpenPreparation);
            UiFeel.Bind(Ui.Require<Button>(root, "cheat-quick"), StartQuickBattle);
            UiFeel.Bind(Ui.Require<Button>(root, "cheat-quest"), CompleteQuest);
            UiFeel.Bind(Ui.Require<Button>(root, "cheat-land-buy"), () => Report(_context.Session.DebugOwnAllLand()));
            UiFeel.Bind(Ui.Require<Button>(root, "cheat-land-clear"),
                () => Report(_context.Session.DebugClearAllLand()));
            UiFeel.Bind(Ui.Require<Button>(root, "cheat-buildings"),
                () => Report(_context.Session.DebugUnlockAllBuildings()));
            var gold = Ui.Require<Button>(root, "cheat-gold");
            gold.text = $"+{GoldGrant} золота";
            UiFeel.Bind(gold, () => Report(_context.Session.DebugAddGold(GoldGrant)));
            Hide();
        }

        public bool IsShown => Ui.IsShown(_overlay);

        public void Toggle()
        {
            if (IsShown)
            {
                Hide();
                return;
            }
            Ui.Show(_overlay, true);
            _overlay.BringToFront();
            UiMotion.PopIn(_overlay.Q(className: "dialog"), .25f);
        }

        public void Hide() => Ui.Show(_overlay, false);

        private void OpenPreparation()
        {
            var mission = _context.FirstMission;
            if (mission == null)
            {
                _status.text = "Миссия не найдена";
                return;
            }
            var session = _context.Session;
            session.EnableDebugBattleAccess();
            var available = session.CanEnterMission(mission.MissionId);
            if (!available.Ok)
            {
                _status.text = available.Error;
                return;
            }
            Hide();
            _context.OpenBattle?.Invoke(mission);
        }

        private void StartQuickBattle()
        {
            var mission = _context.FirstMission;
            if (mission == null)
            {
                _status.text = "Миссия не найдена";
                return;
            }
            try { mission.CreateBoard(); }
            catch (Exception)
            {
                _status.text = "Данные миссии некорректны";
                return;
            }
            var session = _context.Session;
            session.EnableDebugBattleAccess();
            var available = session.CanEnterMission(mission.MissionId);
            if (!available.Ok)
            {
                _status.text = available.Error;
                return;
            }

            if (session.CurrentSnapshot.Units.Count == 0)
            {
                var catalog = session.Catalog;
                // a campaign that has not opened the troll yet fights with goblins only
                var leader = session.IsUnitUnlocked(UnitKind.Troll) ? UnitKind.Troll : UnitKind.Goblin;
                var squad = new[] { leader, UnitKind.Goblin, UnitKind.Goblin, UnitKind.Goblin };
                int squadCost = 0;
                foreach (var kind in squad) squadCost += catalog.GetUnit(kind).Price;
                if (session.CurrentSnapshot.Gold < squadCost)
                {
                    _status.text = "Не хватает золота для тестового отряда";
                    return;
                }
                foreach (var kind in squad)
                {
                    var buy = session.Dispatch(new BuyUnitsCommand(kind, 1, session.FindSpawnCell()));
                    if (!buy.Ok)
                    {
                        _status.text = buy.Error;
                        return;
                    }
                }
            }

            Hide();
            if (_context.OpenQuickBattle == null || !_context.OpenQuickBattle(mission))
                _status.text = "Не удалось запустить бой: проверь состав и данные миссии";
        }

        private void Report(CommandResult result) =>
            _status.text = result.Ok ? "Готово" : result.Error;

        private void CompleteQuest()
        {
            var result = _context.Session.DebugCompleteQuest();
            if (!result.Ok)
            {
                _status.text = result.Error;
                return;
            }
            // the reward reveal opens over the colony
            Hide();
        }
    }
}
#endif
