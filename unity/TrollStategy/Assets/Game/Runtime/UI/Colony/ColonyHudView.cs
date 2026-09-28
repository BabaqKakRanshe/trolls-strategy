using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The colony HUD over a cloned ColonyHud.uxml. Holds the screen parts and routes snapshots and
    /// interaction changes to them; it needs no scene, so EditMode tests build it the same way.
    /// </summary>
    public sealed class ColonyHudView
    {
        private static readonly Color RefusalColor = new(1f, .5f, .42f, 1f);
        // Suggested buttons and cards breathe between two border tones at this pace.
        private const float PulseSeconds = .55f;
        private const string PulseClass = "hud-pulse";

        private readonly ColonyHudContext _context;
        private bool _refusalPending;
        private bool _hudVisible = true;
        private GameSnapshot _snapshot;
        private float _pulseTime;

        public ColonyHudView(VisualElement root, ColonyHudContext context)
        {
            _context = context;
            Root = root.Q("hud-screen") ?? root;
            Showcase = new ShowcasePanel(Root, context.Showcase);
            Catalog = new CatalogPanel(Root, context, Showcase);
            TopBar = new TopBar(Root, context, Catalog.Toggle);
            Catalog.OpenChanged += TopBar.SetCatalogOpen;
            TopBar.SetCatalogOpen(Catalog.IsOpen);
            Quest = new QuestTracker(Root, context);
            Inspect = new InspectPanel(Root, context);
            Tooltip = new HudTooltip(Root);
            ContextBar = new ContextBar(Root, context);
            HaulCargo = new HaulCargoDialog(Root, context, Tooltip);
            Status = new StatusLine(Root);
            Fan = new CommandFan(Root, context);
            Reward = new RewardOverlay(Root, context, Showcase);
            BattleReward = new BattleRewardOverlay(Root, context);
            Quest.ClaimRequested += OpenReward;
            // the hint card draws over every other part of the HUD
            Tooltip.BringToFront();
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            Cheat = new CheatPanel(Root, context);
#else
            Root.Q("cheat-overlay")?.RemoveFromHierarchy();
#endif
            var snapshot = context.Session.CurrentSnapshot;
            Refresh(snapshot);
            Status.Show(context.Interaction, first: true);
        }

        public VisualElement Root { get; }
        public TopBar TopBar { get; }
        public CatalogPanel Catalog { get; }
        public ShowcasePanel Showcase { get; }
        public QuestTracker Quest { get; }
        public InspectPanel Inspect { get; }
        public ContextBar ContextBar { get; }
        public HaulCargoDialog HaulCargo { get; }
        public StatusLine Status { get; }
        public CommandFan Fan { get; }
        public RewardOverlay Reward { get; }
        public BattleRewardOverlay BattleReward { get; }
        public HudTooltip Tooltip { get; }
        public QuestFocus Focus { get; private set; } = QuestFocus.None;
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        public CheatPanel Cheat { get; }
#endif

        /// <summary>A new simulation step or command result: every part that shows colony state.</summary>
        public void Refresh(GameSnapshot snapshot)
        {
            _snapshot = snapshot;
            Focus = QuestFocus.From(snapshot);
            TopBar.SetFocus(Focus);
            Catalog.SetFocus(Focus, snapshot.Progress.Quest?.Id);
            ContextBar.SetFocus(Focus);

            TopBar.Refresh(snapshot);
            Catalog.Refresh(snapshot);
            // a battle is decided when it starts; its quest tick waits until the colony is back on screen
            if (_hudVisible) Quest.Refresh(snapshot);
            Inspect.Refresh(snapshot);
            ContextBar.Refresh(snapshot);
            HaulCargo.Refresh(snapshot);
            Fan.Refresh();
            OfferReward();
        }

        /// <summary>The player's intent changed: the same refresh plus the message line.</summary>
        public void OnInteractionChanged(GameSnapshot snapshot)
        {
            Refresh(snapshot);
            Status.Show(_context.Interaction);
            FlushRefusal();
        }

        public void Tick(float unscaledDeltaTime)
        {
            FlushRefusal();
            TopBar.Tick();
            Status.Tick();
            Showcase.Tick(unscaledDeltaTime);
            Reward.Tick(unscaledDeltaTime);
            BattleReward.Tick(unscaledDeltaTime);
            _pulseTime += unscaledDeltaTime;
            Root.EnableInClassList(PulseClass, (int)(_pulseTime / PulseSeconds) % 2 == 1);
        }

        /// <summary>The HUD was shown or hidden (the battle scene covers the colony); rewards wait for it.</summary>
        public void SetHudVisible(bool visible)
        {
            _hudVisible = visible;
            if (!visible || _snapshot == null) return;
            Quest.Refresh(_snapshot);
            OfferReward();
        }

        /// <summary>
        /// A refused intent. The refusal arrives before the controller publishes its reason, so the "no"
        /// plays once the line that carries the reason is up to date.
        /// </summary>
        public void PlayRefusal() => _refusalPending = true;

        private void FlushRefusal()
        {
            if (!_refusalPending) return;
            _refusalPending = false;
            if (HaulCargo.IsOpen)
            {
                HaulCargo.PlayRefusal(RefusalColor);
                return;
            }
            if (ContextBar.IsShown && _context.Interaction.Mode.Type != InteractionModeType.Neutral)
            {
                UiMotion.Nudge(ContextBar.Prompt, 8f);
                UiMotion.Flash(ContextBar.Prompt, RefusalColor, .6f);
                return;
            }
            Status.PlayRefusal();
        }

        public void OpenCommandFan(Vector2 panelPoint) => Fan.OpenAt(panelPoint);

        public void ToggleCheat()
        {
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            Cheat.Toggle();
#endif
        }

        // A won battle's prize, then a finished quest's reward, opens once the colony is on screen and no
        // placement or order is half done; one at a time.
        private void OfferReward()
        {
            if (_snapshot == null || Reward.IsOpen || BattleReward.IsOpen || !_hudVisible) return;
            if (_context.Interaction.Mode.Type != InteractionModeType.Neutral) return;
            if (_snapshot.BattleReward != null)
            {
                BattleReward.Open(_snapshot.BattleReward);
                return;
            }
            var quest = _snapshot.Progress.Quest;
            if (quest != null && quest.IsComplete) Reward.Open(quest);
        }

        // The tracker's button: open the reveal even in the middle of an order.
        private void OpenReward()
        {
            var quest = _snapshot?.Progress.Quest;
            if (quest == null || !quest.IsComplete || BattleReward.IsOpen) return;
            if (Reward.IsOpen) Reward.Skip();
            else Reward.Open(quest);
        }
    }
}
