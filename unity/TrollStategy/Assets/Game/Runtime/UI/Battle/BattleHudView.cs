using System;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The battle HUD over the root elements of its part documents: deployment panels, then the replay
    /// bar and the verdict. Follows a <see cref="BattleDeployment"/> and asks the battle for the start,
    /// pause, speed and the way home; it needs no scene, so EditMode tests build it the same way.
    /// </summary>
    public sealed class BattleHudView
    {
        private readonly VisualElement _deploymentBand;
        private BattleDeployment _deployment;

        public BattleHudView(BattleHudRoots roots)
        {
            Root = roots.Screen;
            _deploymentBand = roots.Deployment;
            Header = new BattleHeader(roots.Header, () => CloseRequested?.Invoke());
            Roster = new RosterPanel(roots.Roster);
            Gear = new GearPanel(roots.Selected);
            Actions = new DeploymentActions(roots.Actions, () => StartRequested?.Invoke());
            Replay = new ReplayBar(roots.Replay, () => PauseToggled?.Invoke(), speed => SpeedChosen?.Invoke(speed),
                () => CloseRequested?.Invoke());
            Banner = new BattleBanner(roots.Banner);
        }

        public event Action StartRequested;
        public event Action PauseToggled;
        public event Action<float> SpeedChosen;
        public event Action CloseRequested;

        public VisualElement Root { get; }
        public BattleHeader Header { get; }
        public RosterPanel Roster { get; }
        public GearPanel Gear { get; }
        public DeploymentActions Actions { get; }
        public ReplayBar Replay { get; }
        public BattleBanner Banner { get; }
        public bool IsDeploying => Ui.IsShown(_deploymentBand);

        public void Open(BattleDeployment deployment)
        {
            Detach();
            _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
            _deployment.Changed += Refresh;
            var mission = deployment.Mission;
            Header.Show($"{mission.DisplayName.ToUpperInvariant()} · РАССТАНОВКА",
                $"Цель: победи всех врагов · {EnemySummary(deployment)} · Награда за победу — сюрприз: " +
                $"{GameSession.GoldRange(mission.FirstWinGold, mission.FirstWinGoldMax)} золота за первую, " +
                $"{GameSession.GoldRange(mission.RepeatWinGold, mission.RepeatWinGoldMax)} за повторную",
                "1  ВЫБЕРИ БОЙЦА     →     2  НАЖМИ СИНЮЮ КЛЕТКУ     →     3  ВЫДАЙ СНАРЯЖЕНИЕ И НАЧНИ БОЙ");
            Roster.Build(deployment);
            Gear.Build(deployment);
            Actions.Attach(deployment);
            Ui.Show(_deploymentBand, true);
            Replay.Hide();
            Banner.Hide();
        }

        /// <summary>The deployment changed: every panel that shows it.</summary>
        public void Refresh()
        {
            Roster.Refresh();
            Gear.Refresh();
            Actions.Refresh();
        }

        public void Refuse() => Actions.Refuse();

        public void BeginReplay()
        {
            Detach();
            Ui.Show(_deploymentBand, false);
            Replay.Begin();
            Header.Show($"{_deployment?.Mission.DisplayName.ToUpperInvariant()}  ·  БОЙ",
                "Цель: победи всех врагов · синий круг — твой боец · красный — противник · над бойцами — здоровье",
                "БОЙ ИДЁТ АВТОМАТИЧЕСКИ · ПРОБЕЛ — ПАУЗА / ПРОДОЛЖИТЬ · СКОРОСТЬ ВНИЗУ СПРАВА");
            Banner.Show("В БОЙ!", null, 1.1f);
        }

        public void ShowReplay(bool paused, float speed, float seconds, int alivePlayers, int aliveEnemies) =>
            Replay.Show(paused, speed, seconds, alivePlayers, aliveEnemies);

        public void ShowResult(BattleOutcome outcome, int survived, int fallen, int lostItems)
        {
            bool victory = outcome == BattleOutcome.PlayerVictory;
            bool defeat = outcome == BattleOutcome.EnemyVictory;
            string verdict = victory ? "ПОБЕДА" : defeat ? "ПОРАЖЕНИЕ" : "НИЧЬЯ";
            // the amount is a surprise revealed back in the colony
            string reward = victory ? "награда ждёт в поселении" : "без награды";
            Replay.ShowResult($"{verdict} · {reward}\nВыжило: {survived} · погибло: {fallen} · потеряно вещей: {lostItems}");
            Header.Show($"{_deployment?.Mission.DisplayName.ToUpperInvariant()}  ·  {verdict}",
                victory
                    ? "Бой завершён · потери применены · награда ждёт в поселении · выжившие вернутся в колонию"
                    : "Бой завершён · потери применены · выжившие бойцы вернутся в колонию",
                "Нажми «Вернуться в колонию», чтобы продолжить строительство и добычу.");
            Banner.Show(verdict, victory ? "is-victory" : defeat ? "is-defeat" : "is-draw", 2f);
        }

        public void Tick() => Banner.Tick();

        public void Close()
        {
            Detach();
            _deployment = null;
            Banner.Hide();
        }

        private void Detach()
        {
            if (_deployment != null) _deployment.Changed -= Refresh;
        }

        private static string EnemySummary(BattleDeployment deployment) => "Враги: " + string.Join(", ",
            deployment.Mission.Enemies
                .GroupBy(enemy => enemy.Kind)
                .Select(group => $"{deployment.Session.Catalog.GetUnit(group.Key).DisplayName} ×{group.Count()}"));
    }
}
