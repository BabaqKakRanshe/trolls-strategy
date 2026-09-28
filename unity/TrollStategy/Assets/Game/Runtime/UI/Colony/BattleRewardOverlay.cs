using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Feel;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The prize for a won battle, shown as a slot machine back in the colony. The battle rolled a surprise
    /// amount within its range; here one reel per digit spins and stops left to right on that amount while
    /// the lights round the machine chase, then all lights flash, a fanfare plays and the amount appears with
    /// a button to take it. Taking it sends the claim; the session adds the gold. A function of the time since
    /// opening, advanced by <see cref="Tick"/>, so EditMode tests play it without a panel; a click skips ahead.
    /// </summary>
    public sealed class BattleRewardOverlay
    {
        private const float OpenSeconds = .3f;
        private const float FirstStopAt = 1.3f;
        private const float StopGap = .55f;
        private const float WinBlinkSeconds = 1.4f;
        private const float CloseSeconds = .22f;
        private const int MinReels = 3;
        private const int BaseLoops = 3;
        // Must match Progression.uss: .jackpot__digit height, and the machine box the lights run round.
        private const float DigitHeight = 104f;
        private const float MachineWidth = 460f;
        private const float MachineHeight = 176f;
        private const float LightInset = 9f;
        private const float LightSize = 12f;
        private const int LightCount = 30;
        private const int SparkCount = 20;

        private sealed class Reel
        {
            public VisualElement Strip;
            public int Target;
            public int LastShown;
        }

        private readonly ColonyHudContext _context;
        private readonly VisualElement _overlay;
        private readonly VisualElement _dialog;
        private readonly VisualElement _reelsRoot;
        private readonly VisualElement _machine;
        private readonly Label _title;
        private readonly Label _range;
        private readonly Label _amount;
        private readonly Button _claim;
        private readonly List<Reel> _reels = new();
        private readonly List<VisualElement> _lights = new();
        private readonly List<Image> _sparks = new();

        private BattleRewardSnapshot _reward;
        private float _time = -1f;
        private float _closing = -1f;
        private bool _claimShown;
        private bool _amountShown;

        public BattleRewardOverlay(VisualElement root, ColonyHudContext context)
        {
            _context = context;
            _overlay = Ui.Require<VisualElement>(root, "battle-reward-overlay");
            _dialog = Ui.Require<VisualElement>(root, "battle-reward-dialog");
            _machine = Ui.Require<VisualElement>(root, "battle-reward-machine");
            _reelsRoot = Ui.Require<VisualElement>(root, "battle-reward-reels");
            _title = Ui.Require<Label>(root, "battle-reward-title");
            _range = Ui.Require<Label>(root, "battle-reward-range");
            _amount = Ui.Require<Label>(root, "battle-reward-amount");
            _claim = UiFeel.Bind(Ui.Require<Button>(root, "battle-reward-claim"), Claim, silentClick: true);
            _machine.RegisterCallback<PointerDownEvent>(_ => Skip());
            BuildLights(Ui.Require<VisualElement>(root, "battle-reward-lights"));
            BuildSparks(Ui.Require<VisualElement>(root, "battle-reward-sparks"));
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => _time >= 0f;
        public bool IsReady => IsOpen && _closing < 0f && _time >= ReadyAt;
        public Button ClaimButton => _claim;
        public string AmountText => _amount.text;
        public int ReelCount => _reels.Count;

        /// <summary>The digits the reels stop on, left to right.</summary>
        public string ShownDigits
        {
            get
            {
                var digits = new char[_reels.Count];
                for (int i = 0; i < _reels.Count; i++) digits[i] = (char)('0' + _reels[i].Target);
                return new string(digits);
            }
        }

        private float LastStopAt => FirstStopAt + StopGap * Mathf.Max(0, _reels.Count - 1);
        private float ReadyAt => LastStopAt + .5f;

        public void Open(BattleRewardSnapshot reward)
        {
            if (reward == null) return;
            _reward = reward;
            _time = 0f;
            _closing = -1f;
            Ui.SetText(_title, reward.FirstWin ? $"{reward.MissionName} · первая победа" : reward.MissionName);
            Ui.SetText(_range, reward.MaxGold > reward.MinGold
                ? $"Награда за бой: от {reward.MinGold} до {reward.MaxGold} золота"
                : $"Награда за бой: {reward.MinGold} золота");
            Ui.SetText(_amount, string.Empty);
            _amountShown = false;
            BuildReels(reward);
            SetClaimShown(false);
            Ui.Show(_overlay, true);
            _overlay.BringToFront();
            Render(0f, 0f);
            GameAudio.Play(Sfx.Coins, .7f, .8f);
        }

        public void Skip()
        {
            if (!IsOpen || _closing >= 0f || _time >= ReadyAt) return;
            Advance(ReadyAt - _time);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsOpen) return;
            if (_closing >= 0f)
            {
                _closing += unscaledDeltaTime;
                float k = Mathf.Clamp01(_closing / CloseSeconds);
                _overlay.style.opacity = 1f - k;
                _dialog.style.scale = new Scale(Vector3.one * Mathf.Lerp(1f, .9f, Ease.InCubic(k)));
                if (k >= 1f) Hide();
                return;
            }
            Advance(unscaledDeltaTime);
        }

        public void Hide()
        {
            _time = -1f;
            _closing = -1f;
            _reward = null;
            Ui.Show(_overlay, false);
        }

        private void Claim()
        {
            if (!IsOpen || _closing >= 0f) return;
            if (_time < ReadyAt)
            {
                Skip();
                return;
            }
            var result = _context.Interaction.ClaimBattleReward();
            if (!result.Ok)
            {
                UiMotion.Nudge(_claim, 8f);
                return;
            }
            GameAudio.Play(Sfx.Coins);
            _closing = 0f;
        }

        private void Advance(float deltaSeconds)
        {
            float before = _time;
            _time += Mathf.Max(0f, deltaSeconds);
            Render(before, _time);
        }

        private void Render(float before, float t)
        {
            _overlay.style.opacity = Mathf.Clamp01(t / .2f);
            float open = Mathf.Clamp01(t / OpenSeconds);
            float jolt = t < LastStopAt ? 0f : Ease.Spring((t - LastStopAt) / .45f, 1f) * .04f;
            _dialog.style.scale = new Scale(Vector3.one * Mathf.Max(.001f, Ease.OutBack(open, 1.6f) + jolt));

            for (int i = 0; i < _reels.Count; i++) RenderReel(_reels[i], i, before, t);

            bool won = t >= LastStopAt;
            if (before < LastStopAt && won)
            {
                GameAudio.Play(Sfx.Victory, .9f);
                GameAudio.Play(Sfx.Coins, 1f, 1.1f);
            }
            _machine.EnableInClassList("is-won", won);
            RenderLights(t);
            RenderSparks(t - LastStopAt);

            // the amount appears once the last reel lands
            float shown = Mathf.Clamp01((t - LastStopAt) / .35f);
            _amount.style.opacity = shown;
            _amount.style.scale = new Scale(Vector3.one * Mathf.Lerp(.6f, 1f, Ease.OutBack(shown, 2.4f)));
            if (won != _amountShown)
            {
                _amountShown = won;
                Ui.SetText(_amount, won && _reward != null ? $"+{_reward.Gold} золота" : string.Empty);
            }

            bool ready = t >= ReadyAt;
            if (ready != _claimShown)
            {
                SetClaimShown(ready);
                if (ready) UiMotion.PopIn(_claim, .3f);
            }
        }

        // A reel runs a few whole turns more than the one before it, slows down and locks on its digit.
        private void RenderReel(Reel reel, int index, float before, float t)
        {
            float stopAt = FirstStopAt + StopGap * index;
            int finalIndex = (BaseLoops + index) * 10 + reel.Target;
            float k = Mathf.Clamp01(t / stopAt);
            float eased = 1f - Mathf.Pow(1f - k, 4f);
            float offset = finalIndex * DigitHeight * eased;
            if (t >= stopAt) offset = finalIndex * DigitHeight - 10f * Ease.Spring((t - stopAt) / .35f, 1.5f);
            reel.Strip.style.translate = new Translate(0f, -offset);

            int shown = Mathf.FloorToInt(offset / DigitHeight + .5f);
            if (t < stopAt && shown != reel.LastShown)
            {
                reel.LastShown = shown;
                GameAudio.Play(Sfx.Select, .35f, .8f + .6f * k, 0f);
            }
            if (before < stopAt && t >= stopAt)
            {
                GameAudio.Play(Sfx.Land, .8f, 1f + .08f * index);
            }
            reel.Strip.parent.EnableInClassList("is-stopped", t >= stopAt);
        }

        private void SetClaimShown(bool shown)
        {
            _claimShown = shown;
            _claim.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
        }

        // Fast chase while the reels run, all lights flashing together on the win, a slow chase after.
        private void RenderLights(float t)
        {
            bool blink = t >= LastStopAt && t < LastStopAt + WinBlinkSeconds;
            bool allOn = blink && (int)((t - LastStopAt) / .14f) % 2 == 0;
            int step = t < LastStopAt ? (int)(t / .05f) : (int)(t / .2f);
            for (int i = 0; i < _lights.Count; i++)
                _lights[i].EnableInClassList("is-lit", blink ? allOn : (i + step) % 3 == 0);
        }

        // A shower of coins' glints from the machine's centre on the win.
        private void RenderSparks(float s)
        {
            bool live = s >= 0f && s < 1.1f;
            for (int i = 0; i < _sparks.Count; i++)
            {
                var spark = _sparks[i];
                Ui.Show(spark, live);
                if (!live) continue;
                float k = s / 1.1f;
                float angle = (i * 360f / SparkCount + i % 3 * 9f) * Mathf.Deg2Rad;
                float distance = (150f + i * 37 % 90) * Ease.OutCubic(k);
                spark.style.translate = new Translate(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * .6f);
                spark.style.opacity = 1f - k * k;
                spark.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.3f, .3f, k));
                spark.style.rotate = new Rotate(k * 240f * (i % 2 == 0 ? 1f : -1f));
            }
        }

        private void BuildReels(BattleRewardSnapshot reward)
        {
            _reelsRoot.Clear();
            _reels.Clear();
            var coin = RewardArt.Coin(_context.Catalog);
            if (coin != null)
            {
                var image = new Image { sprite = coin, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                image.AddToClassList("jackpot__coin");
                _reelsRoot.Add(image);
            }
            int count = Mathf.Max(MinReels, Mathf.Max(reward.MaxGold, reward.Gold).ToString().Length);
            string digits = reward.Gold.ToString().PadLeft(count, '0');
            for (int i = 0; i < count; i++)
            {
                var window = Ui.Box("jackpot__reel");
                window.pickingMode = PickingMode.Ignore;
                var strip = Ui.Box("jackpot__strip");
                strip.pickingMode = PickingMode.Ignore;
                int length = (BaseLoops + i + 1) * 10;
                for (int d = 0; d < length; d++)
                {
                    var digit = Ui.Text((d % 10).ToString(), "jackpot__digit t-bold");
                    digit.pickingMode = PickingMode.Ignore;
                    strip.Add(digit);
                }
                window.Add(strip);
                _reelsRoot.Add(window);
                _reels.Add(new Reel { Strip = strip, Target = digits[i] - '0' });
            }
        }

        private void BuildLights(VisualElement parent)
        {
            // evenly along the machine's rim, clockwise from the top left corner
            float w = MachineWidth - 2f * LightInset, h = MachineHeight - 2f * LightInset;
            float perimeter = 2f * (w + h);
            for (int i = 0; i < LightCount; i++)
            {
                float d = perimeter * i / LightCount;
                float x, y;
                if (d < w) { x = d; y = 0f; }
                else if (d < w + h) { x = w; y = d - w; }
                else if (d < 2f * w + h) { x = w - (d - w - h); y = h; }
                else { x = 0f; y = h - (d - 2f * w - h); }
                var light = Ui.Box("jackpot__light");
                light.style.left = LightInset + x - LightSize * .5f;
                light.style.top = LightInset + y - LightSize * .5f;
                parent.Add(light);
                _lights.Add(light);
            }
        }

        private void BuildSparks(VisualElement parent)
        {
            for (int i = 0; i < SparkCount; i++)
            {
                var spark = new Image { sprite = FeelSprites.Spark, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                spark.AddToClassList("jackpot__spark");
                if (i % 3 == 1) spark.AddToClassList("jackpot__spark--pale");
                Ui.Show(spark, false);
                parent.Add(spark);
                _sparks.Add(spark);
            }
        }
    }
}
