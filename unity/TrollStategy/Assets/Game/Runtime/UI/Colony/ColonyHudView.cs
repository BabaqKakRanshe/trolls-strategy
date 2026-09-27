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

        private readonly ColonyHudContext _context;
        private bool _refusalPending;

        public ColonyHudView(VisualElement root, ColonyHudContext context)
        {
            _context = context;
            Root = root.Q("hud-screen") ?? root;
            Showcase = new ShowcasePanel(Root, context.Showcase);
            Catalog = new CatalogPanel(Root, context, Showcase);
            TopBar = new TopBar(Root, context, Catalog.Toggle);
            Catalog.OpenChanged += TopBar.SetCatalogOpen;
            TopBar.SetCatalogOpen(Catalog.IsOpen);
            Inspect = new InspectPanel(Root, context);
            ContextBar = new ContextBar(Root, context);
            Status = new StatusLine(Root);
            Fan = new CommandFan(Root, context);
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
        public InspectPanel Inspect { get; }
        public ContextBar ContextBar { get; }
        public StatusLine Status { get; }
        public CommandFan Fan { get; }
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        public CheatPanel Cheat { get; }
#endif

        /// <summary>A new simulation step or command result: every part that shows colony state.</summary>
        public void Refresh(GameSnapshot snapshot)
        {
            TopBar.Refresh(snapshot);
            Catalog.Refresh(snapshot);
            Inspect.Refresh(snapshot);
            ContextBar.Refresh(snapshot);
            Fan.Refresh();
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
    }
}
