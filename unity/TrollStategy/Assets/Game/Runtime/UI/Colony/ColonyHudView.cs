using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Visuals;
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
        // the tutorial pointer: its layer, the pointer on the screen, how long ago the step was worked out, and the
        // step the camera already went to
        private const float GuideRefreshSeconds = .15f;
        private readonly VisualElement _guideLayer;
        private Vector2? _pointerScreen;
        private float _guideAge;
        private string _focusedKey;
        // the goods the haul's arrow carries in turn, worked out with each refresh
        private IReadOnlyList<Sprite> _routeCargo = Array.Empty<Sprite>();
        private readonly SaveGames _saves;

        public ColonyHudView(ColonyHudRoots roots, ColonyHudContext context)
        {
            _context = context;
            Root = roots.Screen;
            // the hint card's document sorts above every other part of the HUD; the parts that name things
            // by picture only (counters, tools, catalog tokens) put their words in it
            Tooltip = new HudTooltip(roots.Tooltip) { MoreKey = Hotkeys.Wiki.Label };
            // the book before the parts: their hints and the inspect card link into it
            if (roots.Wiki != null)
            {
                Wiki = new WikiPanel(roots.Wiki, context);
                context.OpenWiki = OpenWiki;
                context.OpenWikiAt = OpenWikiAt;
            }
            else
            {
                context.OpenWiki = null;
                context.OpenWikiAt = null;
            }
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
            Intro = new IntroPanel(roots.Intro, context.Edition, () => context.IntroClosed?.Invoke(),
                context.OffersSavesAtLaunch ? context.Saves : null, context.Catalog, Tooltip);
            // the notice that closes a short public build lives in the intro's document
            DemoEnd = new DemoEndPanel(roots.Intro, context, Tooltip);
            context.OpenArena = Arena.Open;
            context.OpenMenu = Menu.Open;
            Status = new StatusLine(roots.Status);
            // a save answers in the status line; the open list of slots follows the autosave
            _saves = context.Saves;
            if (_saves != null) _saves.Saved += OnSaved;
            Fan = new CommandFan(roots.Fan, context, Tooltip);
            Reward = new RewardOverlay(roots.Reward, context, Showcase);
            // the battle's gold flies into the treasury's counter
            BattleReward = new BattleRewardOverlay(roots.BattleReward, context, TopBar);
            Quest.ClaimRequested += OpenReward;
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            if (roots.Cheat != null) Cheat = new CheatPanel(roots.Cheat, context);
#endif
            // the controls lesson comes before the tutorial's first press (a touch screen skips it)
            Lesson = new ControlsLesson(context.CameraPanned, context.CameraZoomed,
                TrollStrategy.Presentation.GameSettings.ControlsLearned || UnityEngine.Application.isMobilePlatform,
                () => MapPointer.UsesTouch);
            // the tutorial pointer over every part but the rewards, the book, the menu and the hints
            _guideLayer = roots.Guide;
            if (_guideLayer != null)
            {
                Guide = new GuideOverlay(_guideLayer);
                Guide.FocusRequested += target => FocusOn(target);
            }
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
        /// <summary>The notice that closes the itch.io alpha or the Steam demo once its last quest is taken.</summary>
        public DemoEndPanel DemoEnd { get; }
        /// <summary>A dialog holds the screen: the map's clicks and keys wait.</summary>
        public bool BlocksMap => Intro.IsOpen || Intro.AsksToRotate || DemoEnd.IsOpen || Menu.IsOpen || Arena.IsOpen || Wiki?.IsOpen == true;
        public StatusLine Status { get; }
        public CommandFan Fan { get; }
        public RewardOverlay Reward { get; }
        public BattleRewardOverlay BattleReward { get; }
        public HudTooltip Tooltip { get; }
        public QuestFocus Focus { get; private set; } = QuestFocus.None;
        /// <summary>The tutorial pointer; null while the UI prefab has no layer for it.</summary>
        public GuideOverlay Guide { get; }
        /// <summary>The tutorial's controls lesson: the camera, the zoom and the keys before the first press.</summary>
        public ControlsLesson Lesson { get; }
        /// <summary>The tutorial pointer's step as of the last refresh (none after the tutorial or with hints off).</summary>
        public GuideStep CurrentStep { get; private set; } = GuideStep.None;
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
            _routeCargo = RouteCargo(snapshot);
            Fan.Refresh();
            // a building's or creature's card needs the column: the quest folds to its header meanwhile
            Quest.SetMakingRoom(Inspect.IsShown);
            OfferReward();
            UpdateGuide();
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
            TickGuide(unscaledDeltaTime);
            _pulseTime += unscaledDeltaTime;
            // while the tutorial pointer shows, it is the one mark of the next press: the suggested buttons stop breathing
            bool guided = Guide != null && Guide.IsShowing;
            Root.EnableInClassList(PulseClass, !guided && (int)(_pulseTime / PulseSeconds) % 2 == 1);
        }

        /// <summary>
        /// The pointer as the scene reads it each frame: its place on the screen (pixels from the top left) and
        /// whether it was pressed. A press outside the pointer's window lifts that step's veil.
        /// </summary>
        public void TrackPointer(Vector2? screenPosition, bool pressed)
        {
            _pointerScreen = screenPosition;
            if (!pressed || screenPosition == null || Guide == null) return;
            var point = ScreenToLayer(screenPosition.Value);
            if (point != null) Guide.PointerPressed(point.Value);
        }

        private void UpdateGuide()
        {
            _guideAge = 0f;
            CurrentStep = Guide != null && _hudVisible ? ColonyGuide.Resolve(this, _context, _snapshot) : GuideStep.None;
            Guide?.Show(CurrentStep);
        }

        // the step follows the HUD's own changes (a tab, the arena's dialog) a few times a second; the window follows
        // its target every frame
        private void TickGuide(float deltaTime)
        {
            if (Guide == null || _snapshot == null) return;
            _guideAge += deltaTime;
            if (_guideAge >= GuideRefreshSeconds) UpdateGuide();
            Lesson.Tick(deltaTime, CurrentStep.Key != null && CurrentStep.Key.StartsWith(ColonyGuide.LessonKey));
            Guide.Tick(deltaTime, LocateInWorld, PlaceOnGround);
            var from = RouteStart();
            Guide.SetRoute(from, from != null && _pointerScreen != null ? ScreenToLayer(_pointerScreen.Value) : null,
                _routeCargo);
            FocusOnceOffScreen();
        }

        // while the player picks where to carry, the arrow starts at the source building
        private Vector2? RouteStart()
        {
            var mode = _context.Interaction.Mode;
            if (mode.Type != InteractionModeType.ChoosingHaulDestination || _context.BuildingToScreen == null) return null;
            var rect = _context.BuildingToScreen(mode.SourceId);
            return rect != null ? ScreenToLayer(rect.Value.center) : null;
        }

        // the goods the arrow's tokens carry in turn: the ones chosen, or what the cargo dialog's "Всё" card shows
        private IReadOnlyList<Sprite> RouteCargo(GameSnapshot snapshot)
        {
            var interaction = _context.Interaction;
            var mode = interaction.Mode;
            if (mode.Type != InteractionModeType.ChoosingHaulDestination) return Array.Empty<Sprite>();
            var goods = interaction.HaulCargo.Count > 0
                ? interaction.HaulCargo
                : HaulCargoDialog.Everything(snapshot, mode.SourceId, interaction.HaulCargoChoices);
            var pictures = new List<Sprite>(goods.Count);
            foreach (var resource in goods)
            {
                var icon = _context.Catalog.TryGetResource(resource)?.Icon;
                if (icon != null) pictures.Add(icon);
            }
            return pictures;
        }

        private Rect? LocateInWorld(GuideTarget target)
        {
            switch (target.Kind)
            {
                case GuideTargetKind.Building:
                    return ScreenRectToLayer(_context.BuildingToScreen?.Invoke(target.Id));
                case GuideTargetKind.Unit:
                    return ScreenRectToLayer(_context.UnitToScreen?.Invoke(target.Id));
                case GuideTargetKind.Cell:
                case GuideTargetKind.Footprint:
                    return CellsToLayer(target.Cell, Math.Max(1, target.Width), Math.Max(1, target.Height));
                default:
                    return null;
            }
        }

        // a place on the map: its corners, as the colony camera sees them
        private Vector2[] PlaceOnGround(GuideTarget target)
        {
            if (_context.MapToScreen == null) return null;
            return GuidePlace.Corners(target.Cell, Math.Max(1, target.Width), Math.Max(1, target.Height),
                _context.Catalog.Economy.CellSize, position =>
                {
                    var screen = _context.MapToScreen(position);
                    return screen != null ? ScreenToLayer(screen.Value) : null;
                });
        }

        // the four corners of the cells, as the camera sees them
        private Rect? CellsToLayer(Cell cell, int width, int height)
        {
            if (_context.MapToScreen == null) return null;
            float size = _context.Catalog.Economy.CellSize;
            Rect? rect = null;
            for (int i = 0; i < 4; i++)
            {
                float x = (cell.X + ((i & 1) == 0 ? 0 : width)) * size;
                float y = (cell.Y + ((i & 2) == 0 ? 0 : height)) * size;
                var screen = _context.MapToScreen(new WorldPosition(x, y));
                var point = screen != null ? ScreenToLayer(screen.Value) : null;
                if (point == null) return null;
                rect = rect == null
                    ? new Rect(point.Value, Vector2.zero)
                    : Rect.MinMaxRect(Mathf.Min(rect.Value.xMin, point.Value.x), Mathf.Min(rect.Value.yMin, point.Value.y),
                        Mathf.Max(rect.Value.xMax, point.Value.x), Mathf.Max(rect.Value.yMax, point.Value.y));
            }
            return rect;
        }

        private Rect? ScreenRectToLayer(Rect? screen)
        {
            if (screen == null) return null;
            var a = ScreenToLayer(new Vector2(screen.Value.xMin, screen.Value.yMin));
            var b = ScreenToLayer(new Vector2(screen.Value.xMax, screen.Value.yMax));
            if (a == null || b == null) return null;
            return Rect.MinMaxRect(Mathf.Min(a.Value.x, b.Value.x), Mathf.Min(a.Value.y, b.Value.y),
                Mathf.Max(a.Value.x, b.Value.x), Mathf.Max(a.Value.y, b.Value.y));
        }

        // screen pixels (from the top left) into the pointer layer's coordinates
        private Vector2? ScreenToLayer(Vector2 screen)
        {
            var panel = _guideLayer?.panel;
            if (panel == null) return null;
            return _guideLayer.WorldToLocal(RuntimePanelUtils.ScreenToPanel(panel, screen));
        }

        // a target off the screen: the camera goes to it once for each step
        private void FocusOnceOffScreen()
        {
            var step = CurrentStep;
            if (!step.IsShown || !step.Target.InWorld || step.Key == _focusedKey || _guideLayer == null) return;
            var size = new Vector2(_guideLayer.layout.width, _guideLayer.layout.height);
            if (float.IsNaN(size.x) || size.x <= 0f) return;
            _focusedKey = step.Key;
            var rect = LocateInWorld(step.Target);
            if (rect != null && new Rect(Vector2.zero, size).Overlaps(rect.Value)) return;
            FocusOn(step.Target);
        }

        private bool FocusOn(GuideTarget target)
        {
            var snapshot = _snapshot;
            if (snapshot == null) return false;
            var catalog = _context.Catalog;
            switch (target.Kind)
            {
                case GuideTargetKind.Building:
                    foreach (var building in snapshot.Buildings)
                    {
                        if (building.Id != target.Id) continue;
                        _context.Interaction.Focus(TutorialPlaces.CenterOf(building, catalog));
                        return true;
                    }
                    return false;
                case GuideTargetKind.Unit:
                    foreach (var unit in snapshot.Units)
                    {
                        if (unit.Id != target.Id) continue;
                        _context.Interaction.Focus(unit.Position);
                        return true;
                    }
                    return false;
                case GuideTargetKind.Cell:
                case GuideTargetKind.Footprint:
                    float size = catalog.Economy.CellSize;
                    _context.Interaction.Focus(new WorldPosition((target.Cell.X + Math.Max(1, target.Width) * .5f) * size,
                        (target.Cell.Y + Math.Max(1, target.Height) * .5f) * size));
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The HUD was shown or hidden (the battle scene covers the colony); rewards wait for it.</summary>
        public void SetHudVisible(bool visible)
        {
            _hudVisible = visible;
            if (_snapshot != null) UpdateGuide();
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

        /// <summary>Esc: closes the topmost dialog (a notice, the menu's page, the arena); false when none is open.</summary>
        public bool CloseTopOverlay()
        {
            if (Intro.IsOpen)
            {
                Intro.Back();
                return true;
            }
            if (DemoEnd.IsOpen)
            {
                DemoEnd.Close();
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

        // The saves the status line names: the player's own, and the autosave at a quest's or a battle's end. The
        // timed autosave (every minute of play) and the ones on the way out stay quiet.
        private static bool Announced(SaveResult result) =>
            !result.Autosave || result.Reason == "quest" || result.Reason == "battleReward" || result.Reason == "battle";

        private void OnSaved(SaveResult result)
        {
            if (result == null) return;
            if (Announced(result))
                Status.ShowNote(result.Ok ? "Колония сохранена" : "Колонию сохранить не удалось", result.Ok);
            if (result.Autosave) Menu.SavesChanged();
        }

        /// <summary>The view is dropped (the scene goes, or the HUD is rebuilt): it stops listening, its pictures go.</summary>
        public void Detach()
        {
            if (_saves != null) _saves.Saved -= OnSaved;
            Menu.SaveSlots?.Release();
            Intro.ReleasePictures();
        }

        /// <summary>The catalog tool's action, also on its key.</summary>
        public void ToggleCatalogTool() => ToggleCatalog();

        /// <summary>
        /// The book's key: closes the book; otherwise opens it on what the hint card or the inspect card shows,
        /// or where the player left it.
        /// </summary>
        public void ToggleWiki()
        {
            if (Wiki == null) return;
            if (Wiki.IsOpen)
            {
                Wiki.Close();
                return;
            }
            var entry = Tooltip.More ?? (Inspect.IsShown ? Inspect.WikiLink : null);
            if (entry != null) entry();
            else OpenWiki();
        }

        // The book covers the screen; the hint that led to it goes too, since the pointer may stay where it was.
        private void OpenWiki()
        {
            Tooltip.Dismiss();
            Wiki.Open();
        }

        private void OpenWikiAt(WikiSection section, string key)
        {
            Tooltip.Dismiss();
            Wiki.OpenAt(section, key);
        }

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
        // placement or order is half done; one at a time. After a short build's last reward its end notice opens.
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
            else if (quest == null) DemoEnd.Offer(_snapshot);
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
