using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The colony HUD over the root elements of its part documents. Holds the screen parts and routes
    /// snapshots and interaction changes to them; it needs no scene, so EditMode tests build it the same way.
    /// </summary>
    public sealed class ColonyHudView
    {
        private static readonly Color RefusalColor = new(.72f, .21f, .14f, 1f);
        // Suggested buttons and cards breathe between two border tones at this pace.
        private const float PulseSeconds = .55f;
        private const string PulseClass = "hud-pulse";

        private readonly ColonyHudContext _context;
        private bool _refusalPending;
        private bool _hudVisible = true;
        private GameSnapshot _snapshot;
        private float _pulseTime;

        public ColonyHudView(ColonyHudRoots roots, ColonyHudContext context)
        {
            _context = context;
            Root = roots.Screen;
            // the hint card's document sorts above every other part of the HUD; the parts that name things
            // by picture only (counters, tools, catalog tokens) put their words in it
            Tooltip = new HudTooltip(roots.Tooltip);
            Showcase = new ShowcasePanel(roots.Showcase, context.Showcase);
            Catalog = new CatalogPanel(roots.Catalog, context, Showcase, Tooltip);
            TopBar = new TopBar(roots.TopBar, context, ToggleCatalog, Tooltip);
            Catalog.OpenChanged += TopBar.SetCatalogOpen;
            TopBar.SetCatalogOpen(Catalog.IsOpen);
            Quest = new QuestTracker(roots.Quest, context, Tooltip);
            Inspect = new InspectPanel(roots.Inspect, context, Tooltip);
            ContextBar = new ContextBar(roots.Context, context, Tooltip);
            HaulCargo = new HaulCargoDialog(roots.HaulCargo, context, Tooltip);
            Arena = new ArenaPanel(roots.Arena, context, Tooltip);
            Menu = new MenuPanel(roots.Menu, context, Tooltip);
            if (roots.Wiki != null)
            {
                Wiki = new WikiPanel(roots.Wiki, context);
                context.OpenWiki = Wiki.Open;
            }
            Intro = new IntroPanel(roots.Intro, context.Edition, () => context.IntroClosed?.Invoke());
            context.OpenArena = Arena.Open;
            context.OpenMenu = Menu.Open;
            Status = new StatusLine(roots.Status);
            Fan = new CommandFan(roots.Fan, context, Tooltip);
            Reward = new RewardOverlay(roots.Reward, context, Showcase);
            // the battle's gold flies into the treasury's counter
            BattleReward = new BattleRewardOverlay(roots.BattleReward, context, TopBar);
            Quest.ClaimRequested += OpenReward;
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            if (roots.Cheat != null) Cheat = new CheatPanel(roots.Cheat, context);
#endif
            // the static texts of every part's layout in the player's language
            TrollStrategy.Presentation.Localization.TranslateTree(roots.Screen);
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
        public ArenaPanel Arena { get; }
        public MenuPanel Menu { get; }
        /// <summary>The book; null while the UI prefab has no document for it.</summary>
        public WikiPanel Wiki { get; }
        public IntroPanel Intro { get; }
        /// <summary>A dialog holds the screen: the map's clicks and keys wait.</summary>
        public bool BlocksMap => Intro.IsOpen || Intro.AsksToRotate || Menu.IsOpen || Arena.IsOpen || Wiki?.IsOpen == true;
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
            // the orders take the tray's place while creatures are selected or the map waits for a pick
            var interaction = _context.Interaction;
            Catalog.SetCovered(interaction.SelectedIds.Count > 0 || interaction.Mode.Type != InteractionModeType.Neutral);
            HaulCargo.Refresh(snapshot);
            Fan.Refresh();
            // a building's or creature's card needs the column: the quest folds to its header meanwhile
            Quest.SetMakingRoom(Inspect.IsShown);
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
            Arena.Tick(unscaledDeltaTime);
            Intro.SetPortrait(Screen.height > Screen.width * 1.1f);
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

        /// <summary>Esc: closes the topmost dialog (the notice, the menu's page, the arena); false when none is open.</summary>
        public bool CloseTopOverlay()
        {
            if (Intro.IsOpen)
            {
                Intro.Close();
                return true;
            }
            if (Menu.IsOpen)
            {
                Menu.Back();
                return true;
            }
            if (Wiki?.IsOpen == true)
            {
                Wiki.Close();
                return true;
            }
            if (Arena.IsOpen)
            {
                Arena.Close();
                return true;
            }
            return false;
        }

        /// <summary>The catalog tool's action, also on its key.</summary>
        public void ToggleCatalogTool() => ToggleCatalog();

        // The catalog tool: while the orders hold the tray it drops the selection or the pick and brings the
        // catalog back; otherwise it opens and closes the catalog.
        private void ToggleCatalog()
        {
            if (!Catalog.IsCovered)
            {
                Catalog.Toggle();
                return;
            }
            // a pick is dropped first, then the selection under it
            var interaction = _context.Interaction;
            for (int i = 0; i < 3 && (interaction.SelectedIds.Count > 0 || interaction.Mode.Type != InteractionModeType.Neutral); i++)
                interaction.CancelOrClear();
            Catalog.SetCovered(false);
            Catalog.SetOpen(true);
        }

        public void ToggleCheat()
        {
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            Cheat?.Toggle();
#endif
        }

        // A won battle's prize, then a finished quest's reward, opens once the colony is on screen and no
        // placement or order is half done; one at a time.
        private void OfferReward()
        {
            if (_snapshot == null || Reward.IsOpen || BattleReward.IsOpen || !_hudVisible || BlocksMap) return;
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
