using System;
using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The bottom bar of the fight: how many still stand on each side, a portrait and a number, with pause
    /// and the speeds. After the verdict: the verdict, who came back, who fell, what gear was lost, the
    /// reward that waits at home and the way back to the colony.
    /// </summary>
    public sealed class ReplayBar
    {
        private static readonly float[] SpeedValues = { 1f, 2f, 4f };

        private readonly VisualElement _panel;
        private readonly VisualElement _score;
        private readonly VisualElement _outcome;
        private readonly Label _ours;
        private readonly Label _theirs;
        private readonly VisualElement _pauseGlyph;
        private readonly Label _verdict;
        private readonly Label _survived;
        private readonly Label _fallen;
        private readonly Label _lost;
        private readonly VisualElement _lostFact;
        private readonly VisualElement _prize;
        private readonly VisualElement[] _ourIcons;
        private readonly VisualElement _theirIcon;
        private readonly VisualElement _prizeIcon;
        private readonly VisualElement _stakeFact;
        private readonly VisualElement _stakeIcon;
        private readonly Label _stake;
        private readonly VisualElement _restFact;
        private readonly Label _rest;
        private readonly VisualElement _closedFact;
        private readonly Label _closed;
        private readonly Button[] _speeds = new Button[SpeedValues.Length];
        // what the bar shows now; the texts are rebuilt only when one of these changes
        private (bool Paused, float Speed, int Players, int Enemies) _shown = (false, -1f, -1, -1);

        public ReplayBar(VisualElement root, Action pause, Action<float> speed, Action close, HudTooltip tooltip = null)
        {
            _panel = Ui.Require<VisualElement>(root, "battle-replay");
            _score = Ui.Require<VisualElement>(root, "battle-score");
            _outcome = Ui.Require<VisualElement>(root, "battle-outcome");
            _ours = Ui.Require<Label>(root, "battle-ours-value");
            _theirs = Ui.Require<Label>(root, "battle-theirs-value");
            _verdict = Ui.Require<Label>(root, "battle-verdict");
            _survived = Ui.Require<Label>(root, "battle-survived-value");
            _fallen = Ui.Require<Label>(root, "battle-fallen-value");
            _lost = Ui.Require<Label>(root, "battle-lost-value");
            _lostFact = Ui.Require<VisualElement>(root, "battle-lost");
            _prize = Ui.Require<VisualElement>(root, "battle-prize");
            _ourIcons = new[]
            {
                Ui.Require<VisualElement>(root, "battle-ours-icon"),
                Ui.Require<VisualElement>(root, "battle-survived-icon"),
                Ui.Require<VisualElement>(root, "battle-fallen-icon")
            };
            _theirIcon = Ui.Require<VisualElement>(root, "battle-theirs-icon");
            _prizeIcon = Ui.Require<VisualElement>(root, "battle-prize-icon");
            _stakeFact = Ui.Require<VisualElement>(root, "battle-stake");
            _stakeIcon = Ui.Require<VisualElement>(root, "battle-stake-icon");
            _stake = Ui.Require<Label>(root, "battle-stake-value");
            _restFact = Ui.Require<VisualElement>(root, "battle-rest");
            _rest = Ui.Require<Label>(root, "battle-rest-value");
            _closedFact = Ui.Require<VisualElement>(root, "battle-closed");
            _closed = Ui.Require<Label>(root, "battle-closed-value");
            _pauseGlyph = Ui.Require<VisualElement>(root, "battle-pause-glyph");
            Pause = UiFeel.Bind(Ui.Require<Button>(root, "battle-pause"), pause, silentClick: true);
            for (int i = 0; i < SpeedValues.Length; i++)
            {
                float value = SpeedValues[i];
                _speeds[i] = UiFeel.Bind(Ui.Require<Button>(root, $"battle-speed-{value:0}"), () => speed?.Invoke(value));
                tooltip?.Attach(_speeds[i], () => $"Скорость {value:0}×", null);
            }
            Return = UiFeel.Bind(Ui.Require<Button>(root, "battle-return"), close);
            if (tooltip != null)
            {
                tooltip.Attach(Pause, () => _shown.Paused ? "Продолжить" : "Пауза", null, "Пробел");
                tooltip.Attach(Ui.Require<VisualElement>(root, "battle-ours"), () => "Твои бойцы в строю", null);
                tooltip.Attach(Ui.Require<VisualElement>(root, "battle-theirs"), () => "Враги в строю", null);
                tooltip.Attach(Ui.Require<VisualElement>(root, "battle-survived"), () => "Вернулись", () => "Выжившие бойцы идут домой.");
                tooltip.Attach(Ui.Require<VisualElement>(root, "battle-fallen"), () => "Пали", () => "Погибшие бойцы не вернутся.");
                tooltip.Attach(_lostFact, () => "Потеряно снаряжения", () => "Вещи павших бойцов пропали вместе с ними.");
                tooltip.Attach(_prize, () => "Награда", () => "Сколько золота, узнаешь в колонии.");
                tooltip.Attach(_stakeFact, () => "Ставка сгорела", () => "Поражение и ничья сжигают ставку, победа её возвращает.");
                tooltip.Attach(_restFact, () => "Отдых уровня", () => "После поражения уровень отдыхает вдвое дольше.");
                tooltip.Attach(_closedFact, () => "Уровень закрыт", () => "Поражение на вершине лестницы закрывает её снова до новой победы ниже.");
            }
            Hide();
        }

        public Button Pause { get; }
        public Button Return { get; }
        public bool IsShown => Ui.IsShown(_panel);
        public bool IsPaused => _pauseGlyph.ClassListContains("glyph--play");
        public string Ours => _ours.text;
        public string Theirs => _theirs.text;
        public string Verdict => _verdict.text;
        public string Survived => _survived.text;
        public string Fallen => _fallen.text;
        public bool ShowsLostGear => Ui.IsShown(_lostFact);
        public bool ShowsPrize => Ui.IsShown(_prize);
        /// <summary>The burnt stake as shown ("−60"), or null while the verdict shows none.</summary>
        public string BurnedStake => Ui.IsShown(_stakeFact) ? _stake.text : null;
        public string Rest => Ui.IsShown(_restFact) ? _rest.text : null;
        public string Closed => Ui.IsShown(_closedFact) ? _closed.text : null;

        public Button SpeedButton(float speed) => _speeds[Array.IndexOf(SpeedValues, speed)];

        public void Hide() => Ui.Show(_panel, false);

        /// <summary>The fight begins: the sides' portraits and the controls.</summary>
        public void Begin(Sprite ours, Sprite theirs, Sprite coin)
        {
            _shown = (false, -1f, -1, -1);
            foreach (var icon in _ourIcons) Ui.SetPicture(icon, ours);
            Ui.SetPicture(_theirIcon, theirs);
            Ui.SetPicture(_prizeIcon, coin);
            Ui.SetPicture(_stakeIcon, coin);
            Ui.Show(_panel, true);
            Ui.Show(_score, true);
            Ui.Show(_outcome, false);
            Ui.Show(Pause, true);
            foreach (var button in _speeds) Ui.Show(button, true);
            Ui.Show(Return, false);
        }

        public void Show(bool paused, float speed, int players, int enemies)
        {
            var shown = (paused, speed, players, enemies);
            if (shown == _shown) return;
            _shown = shown;
            Ui.SetText(_ours, players.ToString());
            Ui.SetText(_theirs, enemies.ToString());
            Pause.EnableInClassList("is-on", paused);
            _pauseGlyph.EnableInClassList("glyph--pause", !paused);
            _pauseGlyph.EnableInClassList("glyph--play", paused);
            for (int i = 0; i < _speeds.Length; i++)
                _speeds[i].EnableInClassList("is-on", Mathf.Approximately(SpeedValues[i], speed));
        }

        /// <summary>The verdict takes the place of the controls, with the way home.</summary>
        public void ShowResult(string verdict, int survived, int fallen, int lostGear, bool reward, BattleCost cost = null)
        {
            // what the battle cost: the burnt stake, the level's rest after a defeat and a closed level
            bool burned = cost != null && cost.BurnedStake > 0;
            Ui.Show(_stakeFact, burned);
            if (burned) Ui.SetText(_stake, "−" + cost.BurnedStake);
            bool closed = cost != null && cost.ClosedLevel > 0;
            Ui.Show(_closedFact, closed);
            if (closed) Ui.SetText(_closed, $"Уровень {cost.ClosedLevel} закрыт до победы на {cost.ReopenLevel}-м");
            bool rest = burned && cost.RestMs > 0;
            Ui.Show(_restFact, rest);
            if (rest) Ui.SetText(_rest, TopBar.Duration(cost.RestMs));
            Ui.SetText(_verdict, verdict);
            Ui.SetText(_survived, survived.ToString());
            Ui.SetText(_fallen, fallen.ToString());
            Ui.SetText(_lost, lostGear.ToString());
            Ui.Show(_lostFact, lostGear > 0);
            Ui.Show(_prize, reward);
            Ui.Show(_score, false);
            Ui.Show(_outcome, true);
            Ui.Show(Pause, false);
            foreach (var button in _speeds) Ui.Show(button, false);
            Ui.Show(Return, true);
        }
    }
}
