using System;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Presentation.Audio;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The way back to the colony, the mission's name and what it holds: each kind of enemy and the gold a
    /// victory brings, as a picture and a number. How many exactly and the rest are in the hints.
    /// </summary>
    public sealed class BattleHeader
    {
        private readonly Label _title;
        private readonly VisualElement _enemies;
        private readonly VisualElement _rewardIcon;
        private readonly Label _reward;
        private readonly HudTooltip _tooltip;
        private BattleDeployment _deployment;

        public BattleHeader(VisualElement root, Action back, Func<string> backHint, HudTooltip tooltip = null)
        {
            _title = Ui.Require<Label>(root, "battle-title");
            _enemies = Ui.Require<VisualElement>(root, "battle-enemies");
            _rewardIcon = Ui.Require<VisualElement>(root, "battle-reward-icon");
            _reward = Ui.Require<Label>(root, "battle-reward-value");
            _tooltip = tooltip;
            Back = UiFeel.Bind(Ui.Require<Button>(root, "battle-back"), back, Sfx.UiBack);
            tooltip?.Attach(Back, () => "В колонию", backHint, "Esc");
            tooltip?.Attach(Ui.Require<VisualElement>(root, "battle-reward"), () => "Награда за победу", RewardHint);
        }

        public Button Back { get; }
        public string Title => _title.text;
        public string Reward => _reward.text;
        /// <summary>The number beside each kind of enemy, in the mission's order.</summary>
        public string[] Enemies => _enemies.Query<Label>().ToList().Select(label => label.text).ToArray();

        public void Show(BattleDeployment deployment)
        {
            _deployment = deployment;
            var mission = deployment.Mission;
            var catalog = deployment.Session.Catalog;
            Ui.SetText(_title, mission.DisplayName);

            _enemies.Clear();
            foreach (var group in mission.Enemies.GroupBy(enemy => enemy.Kind))
            {
                var unit = catalog.GetUnit(group.Key);
                string name = unit.DisplayName;
                int count = group.Count();
                var fact = Ui.Box("battle-fact");
                var icon = Ui.Box("battle-fact__icon battle-fact__icon--side side-enemy");
                icon.pickingMode = PickingMode.Ignore;
                Ui.SetPicture(icon, RewardArt.Tight(unit.PortraitSprite));
                var value = Ui.Text(count.ToString(), "battle-fact__value t-black");
                value.pickingMode = PickingMode.Ignore;
                fact.Add(icon);
                fact.Add(value);
                _enemies.Add(fact);
                _tooltip?.Attach(fact, () => "Враги", () => $"{name}: {count}. Победи всех, чтобы выиграть бой.");
            }

            var (min, max) = deployment.WinGold;
            Ui.SetPicture(_rewardIcon, RewardArt.Coin(catalog));
            Ui.SetText(_reward, Range(min, max));
        }

        private string RewardHint()
        {
            if (_deployment == null) return null;
            var mission = _deployment.Mission;
            const string exact = "Сколько именно, узнаешь в колонии.";
            return _deployment.FirstWin
                ? $"Золото за первую победу. {exact} Следующие победы: {Range(mission.RepeatWinGold, mission.RepeatWinGoldMax)}."
                : $"Золото за победу. {exact}";
        }

        private static string Range(int min, int max) => max > min ? $"{min}–{max}" : min.ToString();
    }
}
