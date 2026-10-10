using System;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Support;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.UI
{
    /// <summary>
    /// What the colony HUD may read and call. Gameplay goes through the session and the interaction
    /// controller; scene-level actions (guides, battle scene, model preview) arrive as delegates from
    /// the bootstrap so the HUD does not reach into the scene. Unset delegates hide their controls.
    /// </summary>
    public sealed class ColonyHudContext
    {
        public ColonyHudContext(GameSession session, InteractionController interaction)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        }

        public GameSession Session { get; }
        public InteractionController Interaction { get; }
        public GameContentCatalog Catalog => Session.Catalog;

        public Action ToggleGuides { get; set; }
        public Func<bool> GuidesVisible { get; set; }
        public Action<BattleMissionDefinition> OpenBattle { get; set; }
        /// <summary>Development builds only: places the squad and starts the replay at once.</summary>
        public Func<BattleMissionDefinition, bool> OpenQuickBattle { get; set; }
        public BuildingShowcase Showcase { get; set; }
        /// <summary>
        /// Where a point of the colony map shows on the screen, in pixels from the top left; null off the camera.
        /// Set by the bootstrap from the colony camera for the tutorial pointer; null without a scene (tests).
        /// </summary>
        public Func<WorldPosition, Vector2?> MapToScreen { get; set; }
        /// <summary>The screen rectangle (pixels from the top left) around a creature's view, or null.</summary>
        public Func<string, Rect?> UnitToScreen { get; set; }
        /// <summary>The screen rectangle (pixels from the top left) around a building's model, or null.</summary>
        public Func<string, Rect?> BuildingToScreen { get; set; }
        /// <summary>Opens the arena ladder; set by the HUD view, called by the battle tool.</summary>
        public Action OpenArena { get; set; }
        /// <summary>Opens the pause menu; set by the HUD view, called by the menu tool.</summary>
        public Action OpenMenu { get; set; }
        /// <summary>Opens the book; set by the HUD view when the UI has it, called by the book tool.</summary>
        public Action OpenWiki { get; set; }
        /// <summary>Opens the book on one entry (a section and its key); set by the HUD view with the book.</summary>
        public Action<WikiSection, string> OpenWikiAt { get; set; }
        /// <summary>Stops or resumes the colony's time while a menu holds the screen (the bootstrap owns time).</summary>
        public Action<bool> SetPaused { get; set; }
        /// <summary>Starts the colony over from the beginning.</summary>
        public Action Restart { get; set; }
        /// <summary>The colony's saves: the menu's «Сохранения» and the launch window's list; null hides them.</summary>
        public SaveGames Saves { get; set; }
        /// <summary>
        /// The launch window offers the saved colonies: the run's first colony scene, not one a load opened or a
        /// new start replaced.
        /// </summary>
        public bool OffersSavesAtLaunch { get; set; }
        /// <summary>The alpha notice was closed: the first-launch camera flight may start.</summary>
        public Action IntroClosed { get; set; }
        /// <summary>The camera flies over the island (a new colony's start); the tutorial pointer waits for it.</summary>
        public Func<bool> CameraBusy { get; set; }
        /// <summary>How far the player has moved the camera, in sides of the land it frames (the controls lesson).</summary>
        public Func<float> CameraPanned { get; set; }
        /// <summary>How much the player has zoomed the camera, as the sum of |ln| of the steps (the controls lesson).</summary>
        public Func<float> CameraZoomed { get; set; }
        /// <summary>Sends a bug report and shows its progress (the menu's "report a bug"); null without support.</summary>
        public Action ReportBug { get; set; }
        /// <summary>Languages the menu offers, as code and name in that language.</summary>
        public System.Collections.Generic.List<(string Code, string Name)> Languages { get; set; }
        public Func<string> CurrentLanguage { get; set; }
        public Action<string> SetLanguage { get; set; }
        /// <summary>Which public build runs (the itch.io alpha or the Steam demo): the intro and the about page word it.</summary>
        public BuildEdition Edition { get; set; } = BuildInfo.Current.Edition;

        /// <summary>A hint's or a card's "more": the book on one entry, or null while the HUD has no book.</summary>
        public Action WikiLink(WikiSection section, string key) =>
            OpenWikiAt == null || string.IsNullOrEmpty(key) ? null : () => OpenWikiAt?.Invoke(section, key);

        /// <summary>The mission the colony's battle button leads to, or null when the catalog has none.</summary>
        public BattleMissionDefinition FirstMission
        {
            get
            {
                var missions = Catalog.Missions;
                if (missions == null) return null;
                foreach (var mission in missions)
                    if (mission != null) return mission;
                return null;
            }
        }
    }
}
