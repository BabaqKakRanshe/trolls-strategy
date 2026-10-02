using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The root element of each colony HUD part. In the scene each is a nested UIDocument under the
    /// ColonyHud GameObject; tests clone the same documents from the UI prefab.
    /// </summary>
    public sealed class ColonyHudRoots
    {
        /// <summary>The ColonyHud document itself: every part lies inside it, so hiding it hides the HUD.</summary>
        public VisualElement Screen { get; set; }
        public VisualElement TopBar { get; set; }
        public VisualElement Quest { get; set; }
        public VisualElement Inspect { get; set; }
        public VisualElement Showcase { get; set; }
        public VisualElement Catalog { get; set; }
        public VisualElement Status { get; set; }
        public VisualElement Context { get; set; }
        public VisualElement Fan { get; set; }
        public VisualElement HaulCargo { get; set; }
        public VisualElement Arena { get; set; }
        public VisualElement Menu { get; set; }
        /// <summary>The book; optional until the UI prefab is rebuilt with it (TrollStrategy/Dev/Setup UI).</summary>
        public VisualElement Wiki { get; set; }
        public VisualElement Intro { get; set; }
        public VisualElement Reward { get; set; }
        public VisualElement BattleReward { get; set; }
        /// <summary>The developer cheat menu; missing from release players.</summary>
        public VisualElement Cheat { get; set; }
        public VisualElement Tooltip { get; set; }

        public VisualElement[] Required => new[]
        {
            Screen, TopBar, Quest, Inspect, Showcase, Catalog, Status, Context, Fan, HaulCargo, Arena, Reward,
            BattleReward, Menu, Intro, Tooltip
        };
    }
}
