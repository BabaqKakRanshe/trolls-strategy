using System;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The battle HUD over the root elements of its part documents: the header, the squad with the selected
    /// fighter's gear, the hint and the actions while deploying, then the replay bar and the verdict. Follows
    /// a <see cref="BattleDeployment"/> and asks the battle for the start, pause, speed and the way home; it
    /// needs no scene, so EditMode tests build it the same way.
    /// </summary>
    public sealed class BattleHudView
    {
        private readonly VisualElement _deploymentBand;
        private BattleDeployment _deployment;
        // the tutorial pointer: its layer, where the board's cells show, how long ago the step was worked out
        private readonly VisualElement _guideLayer;
        private Func<Cell, Vector2?> _cellToScreen;
        private float _guideAge;

        public BattleHudView(BattleHudRoots roots)
        {
            Root = roots.Screen;
            _deploymentBand = roots.Deployment;
            Tooltip = new HudTooltip(roots.Tooltip);
            Header = new BattleHeader(roots.Header, () => CloseRequested?.Invoke(),
                () => IsDeploying ? "Бой не начнётся, бойцы вернутся к работе." : null, Tooltip);
            Squad = new SquadPanel(roots.Squad, Tooltip);
            Squad.KindDropped += kind => KindDropped?.Invoke(kind);
            Gear = new GearPanel(roots.Squad, Tooltip, () => GearRoom(roots));
            roots.Deployment.RegisterCallback<GeometryChangedEvent>(_ => Gear.Measure());
            Actions = new DeploymentActions(roots.Actions, roots.Hint, () => StartRequested?.Invoke(), Tooltip);
            Replay = new ReplayBar(roots.Replay, () => PauseToggled?.Invoke(), speed => SpeedChosen?.Invoke(speed),
                () => CloseRequested?.Invoke(), Tooltip);
            Banner = new BattleBanner(roots.Banner);
            _guideLayer = roots.Guide;
            if (_guideLayer != null) Guide = new GuideOverlay(_guideLayer);
            TrollStrategy.Presentation.Localization.TranslateTree(roots.Screen);
        }

        public event Action StartRequested;
        public event Action PauseToggled;
        public event Action<float> SpeedChosen;
        public event Action CloseRequested;
        /// <summary>A kind was dragged out of the reserve and let go; the battle finds the cell under the pointer.</summary>
        public event Action<UnitKind> KindDropped;

        public VisualElement Root { get; }
        public HudTooltip Tooltip { get; }
        public BattleHeader Header { get; }
        public SquadPanel Squad { get; }
        public GearPanel Gear { get; }
        public DeploymentActions Actions { get; }
        public ReplayBar Replay { get; }
        public BattleBanner Banner { get; }
        public bool IsDeploying => Ui.IsShown(_deploymentBand);
        /// <summary>The tutorial pointer; null while the UI prefab has no layer for it.</summary>
        public GuideOverlay Guide { get; }
        /// <summary>The tutorial pointer's step on the deployment (none outside the tutorial's battle steps).</summary>
        public GuideStep CurrentStep { get; private set; } = GuideStep.None;

        public void Open(BattleDeployment deployment)
        {
            Detach();
            _deployment = deployment ?? throw new ArgumentNullException(nameof(deployment));
            _deployment.Changed += Refresh;
            Header.Show(deployment);
            Squad.Build(deployment);
            Gear.Build(deployment);
            Actions.Attach(deployment);
            Ui.Show(_deploymentBand, true);
            Replay.Hide();
            Banner.Hide();
            UpdateGuide();
        }

        /// <summary>The deployment changed: every panel that shows it.</summary>
        public void Refresh()
        {
            Squad.Refresh();
            Gear.Refresh();
            Actions.Refresh();
            UpdateGuide();
        }

        public void Refuse() => Actions.Refuse();

        public void BeginReplay()
        {
            Detach();
            Ui.Show(_deploymentBand, false);
            UpdateGuide();
            Replay.Begin(OurPortrait(), EnemyPortrait(), _deployment != null ? RewardArt.Coin(_deployment.Session.Catalog) : null);
            Banner.Show("В бой!", null, 1.1f);
        }

        public void ShowReplay(bool paused, float speed, int alivePlayers, int aliveEnemies) =>
            Replay.Show(paused, speed, alivePlayers, aliveEnemies);

        public void ShowResult(BattleOutcome outcome, int survived, int fallen, int lostItems, BattleCost cost = null)
        {
            bool victory = outcome == BattleOutcome.PlayerVictory;
            bool defeat = outcome == BattleOutcome.EnemyVictory;
            string verdict = victory ? "Победа" : defeat ? "Поражение" : "Ничья";
            // the amount is a surprise revealed back in the colony
            Replay.ShowResult(verdict, survived, fallen, lostItems, cost?.RewardWaits ?? victory, cost);
            Banner.Show(verdict, victory ? "is-victory" : defeat ? "is-defeat" : "is-draw", 2f);
        }

        public void Tick()
        {
            Banner.Tick();
            if (Guide == null) return;
            float deltaTime = Time.unscaledDeltaTime;
            _guideAge += deltaTime;
            if (_guideAge >= .15f) UpdateGuide();
            Guide.Tick(deltaTime, LocateOnBoard);
        }

        /// <summary>Where a cell of the board shows on the screen (pixels from the top left), from the battle scene.</summary>
        public void LocateCells(Func<Cell, Vector2?> cellToScreen) => _cellToScreen = cellToScreen;

        /// <summary>The pointer as the scene reads it each frame; a press outside the pointer's window lifts its veil.</summary>
        public void TrackPointer(Vector2? screenPosition, bool pressed)
        {
            if (!pressed || screenPosition == null || Guide == null) return;
            var point = ScreenToLayer(screenPosition.Value);
            if (point != null) Guide.PointerPressed(point.Value);
        }

        private void UpdateGuide()
        {
            _guideAge = 0f;
            CurrentStep = BattleGuide.Resolve(this, _deployment);
            Guide?.Show(CurrentStep);
        }

        // a fighter on the board: a box over its cell, as tall as a fighter stands
        private Rect? LocateOnBoard(GuideTarget target)
        {
            if (target.Kind != GuideTargetKind.BattleCell || _cellToScreen == null) return null;
            var screen = _cellToScreen(target.Cell);
            var point = screen != null ? ScreenToLayer(screen.Value) : null;
            if (point == null) return null;
            return new Rect(point.Value.x - 45f, point.Value.y - 115f, 90f, 130f);
        }

        private Vector2? ScreenToLayer(Vector2 screen)
        {
            var panel = _guideLayer?.panel;
            if (panel == null) return null;
            return _guideLayer.WorldToLocal(RuntimePanelUtils.ScreenToPanel(panel, screen));
        }

        public void Close()
        {
            Detach();
            _deployment = null;
            Banner.Hide();
            UpdateGuide();
        }

        // the fighter's row may take the band less the start's column and the least the hint keeps
        private static float GearRoom(BattleHudRoots roots) =>
            roots.Deployment.contentRect.width - roots.Actions.layout.width - roots.Hint.resolvedStyle.minWidth.value;

        private void Detach()
        {
            if (_deployment != null) _deployment.Changed -= Refresh;
        }

        // the squad's face: the first fighter on the board, or the colony's first while none stands there
        private Sprite OurPortrait()
        {
            if (_deployment == null) return null;
            string unitId = _deployment.Placements.Count > 0 ? _deployment.Placements[0].UnitId
                : _deployment.Roster.Count > 0 ? _deployment.Roster[0].Id : null;
            return unitId != null ? RewardArt.Tight(_deployment.DefinitionOf(unitId).PortraitSprite) : null;
        }

        private Sprite EnemyPortrait()
        {
            if (_deployment == null || _deployment.Mission.Enemies.Count == 0) return null;
            var unit = _deployment.Session.Catalog.GetUnit(_deployment.Mission.Enemies[0].Kind);
            return unit != null ? RewardArt.Tight(unit.PortraitSprite) : null;
        }
    }
}
