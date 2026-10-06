using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The root element of each battle HUD part. In the scene each is a nested UIDocument under the
    /// BattleHud GameObject; tests clone the same documents from the UI prefab.
    /// </summary>
    public sealed class BattleHudRoots
    {
        /// <summary>The BattleHud document itself: hiding it hides the whole battle screen.</summary>
        public VisualElement Screen { get; set; }
        public VisualElement Header { get; set; }
        /// <summary>The band holding the squad, the hint and the actions; shown only while deploying.</summary>
        public VisualElement Deployment { get; set; }
        public VisualElement Squad { get; set; }
        public VisualElement Hint { get; set; }
        public VisualElement Actions { get; set; }
        public VisualElement Replay { get; set; }
        public VisualElement Banner { get; set; }
        public VisualElement Tooltip { get; set; }
        /// <summary>The tutorial pointer's layer; optional until the UI prefab is rebuilt with it (TrollStrategy/Dev/Setup UI).</summary>
        public VisualElement Guide { get; set; }

        public VisualElement[] Required => new[] { Screen, Header, Deployment, Squad, Hint, Actions, Replay, Banner, Tooltip };
    }
}
