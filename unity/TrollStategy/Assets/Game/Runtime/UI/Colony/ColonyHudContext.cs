using System;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Buildings;

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
