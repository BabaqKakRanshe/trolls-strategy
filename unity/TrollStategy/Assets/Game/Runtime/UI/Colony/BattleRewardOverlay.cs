using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Feel;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The prize for a won battle, back in the colony, with no card around it. "Победа!" in the battle's
    /// banner type over a deep veil, then one white disc per digit: each reel spins and stops left to right
    /// on the amount the battle rolled within its range, throwing a ring of coin sparks as it lands. The amount
    /// appears with a button to take it. Taking it sends the claim and the session adds the gold; the coins
    /// then burst from the amount and fly in a straight line into the treasury's counter, which counts up
    /// as they land, while the veil lifts. A function of the time since opening (and since the claim),
    /// advanced by <see cref="Tick"/>, so EditMode tests play it without a panel; a click skips ahead.
    /// </summary>
    public sealed class BattleRewardOverlay
    {
        private const float OpenSeconds = .3f;
        private const float FirstStopAt = 1.3f;
        private const float StopGap = .55f;
        private const float CloseSeconds = .22f;
        private const int MinReels = 3;
        private const int BaseLoops = 3;
        // Must match Progression.uss: .jackpot__digit height (one digit fills the disc's window).
        private const float DigitHeight = 180f;
        // A ring of sparks round a disc as its reel lands: from just outside the disc, outwards.
        private const int SparksPerReel = 8;
        private const float SparkSeconds = .65f;
        private const float SparkFrom = 84f;
        private const float SparkTo = 132f;
        // The flight to the treasury, in seconds since the claim: each coin bursts out of the amount, then
        // flies straight to the counter, faster as it goes, a short trail behind it.
        private const int FlyingCoins = 14;
        private const float FlightStart = .05f;
        private const float CoinStagger = .045f;
        private const float BurstSeconds = .2f;
        private const float FlySeconds = .5f;
        private const int TrailLength = 2;
        private const float TrailLag = .1f;
        private const float VeilFadeAt = .1f;
        private const float VeilFadeSeconds = .45f;
        private const float FirstArrival = FlightStart + BurstSeconds + FlySeconds;
        private const float LastArrival = FirstArrival + CoinStagger * (FlyingCoins - 1);
        private const float FlightEnd = LastArrival + .15f;

        private sealed class Reel
        {
            public VisualElement Disc;
            public VisualElement Strip;
            public readonly List<VisualElement> Sparks = new();
            public int Target;
            public int LastShown;
        }

        private sealed class Coin
        {
            public Image Body;
            public Image[] Trail;
            public Vector2 Scatter;
        }

        private readonly ColonyHudContext _context;
        private readonly TopBar _treasury;
        private readonly VisualElement _overlay;
        private readonly VisualElement _dialog;
        private readonly VisualElement _reelsRoot;
        private readonly VisualElement _flight;
        private readonly Label _banner;
        private readonly Label _title;
        private readonly Label _range;
        private readonly Label _amount;
        private readonly VisualElement _trophies;
        private readonly Button _claim;
        private readonly List<Reel> _reels = new();
        private readonly List<Coin> _coins = new();

        private BattleRewardSnapshot _reward;
        private float _time = -1f;
        private float _closing = -1f;
        private bool _claimShown;
        private bool _amountShown;
        private bool _flying;
        private Vector2 _from;
        private Vector2 _to;

        public BattleRewardOverlay(VisualElement root, ColonyHudContext context, TopBar treasury = null)
        {
            _context = context;
            _treasury = treasury;
            _overlay = Ui.Require<VisualElement>(root, "battle-reward-overlay");
            _dialog = Ui.Require<VisualElement>(root, "battle-reward-dialog");
            _reelsRoot = Ui.Require<VisualElement>(root, "battle-reward-reels");
            _flight = Ui.Require<VisualElement>(root, "battle-reward-flight");
            _banner = Ui.Require<Label>(root, "battle-reward-banner");
            _title = Ui.Require<Label>(root, "battle-reward-title");
            _range = Ui.Require<Label>(root, "battle-reward-range");
            _amount = Ui.Require<Label>(root, "battle-reward-amount");
            _trophies = Ui.Require<VisualElement>(root, "battle-reward-trophies");
            _claim = UiFeel.Bind(Ui.Require<Button>(root, "battle-reward-claim"), Claim, silentClick: true);
            Ui.Require<VisualElement>(root, "battle-reward-machine").RegisterCallback<PointerDownEvent>(_ => Skip());
            BuildCoins();
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => _time >= 0f;
        public bool IsReady => IsOpen && _closing < 0f && _time >= ReadyAt;
        public Button ClaimButton => _claim;
        public string AmountText => _amount.text;
        /// <summary>The trophies under the amount, as the player reads them: "Ржавый меч +1, ...".</summary>
        public string TrophyText { get; private set; } = string.Empty;
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
            Ui.SetText(_title, reward.FirstWin ? $"{reward.MissionName}, первая победа" : reward.MissionName);
            Ui.SetText(_range, reward.MaxGold > reward.MinGold
                ? $"Награда за бой: от {reward.MinGold} до {reward.MaxGold} золота"
                : $"Награда за бой: {reward.MinGold} золота");
            Ui.SetText(_amount, string.Empty);
            _amountShown = false;
            BuildReels(reward);
            BuildTrophies(reward);
            SetClaimShown(false);
            StopFlight();
            _overlay.style.opacity = 0f;
            _dialog.style.opacity = StyleKeyword.Null;
            _dialog.style.scale = StyleKeyword.Null;
            Ui.Show(_overlay, true);
            _overlay.BringToFront();
            // the coins fly over the veil, which lifts under them
            _flight.BringToFront();
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
                float before = _closing;
                _closing += unscaledDeltaTime;
                RenderClosing(before, _closing);
                return;
            }
            Advance(unscaledDeltaTime);
        }

        public void Hide()
        {
            _time = -1f;
            _closing = -1f;
            _reward = null;
            StopFlight();
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
            // the counter waits for the first coin to land, then counts while the rest come in
            _flying = AimFlight();
            if (_flying) _treasury.ExpectGold(FirstArrival, LastArrival - FirstArrival + .1f);
            var result = _context.Interaction.ClaimBattleReward();
            if (!result.Ok)
            {
                if (_flying) _treasury.ExpectGold(0f, 0f);
                _flying = false;
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
            // the banner pops as in the battle, the line under it rises
            float pop = Mathf.Clamp01((t - .1f) / .6f);
            _banner.style.opacity = Mathf.Clamp01(pop / .3f);
            _banner.style.scale = new Scale(Vector3.one * Mathf.Max(.001f, Mathf.LerpUnclamped(.45f, 1f, Ease.OutBack(pop, 1.8f))));
            float rise = Mathf.Clamp01((t - .4f) / OpenSeconds);
            _title.style.opacity = rise;
            _title.style.translate = new Translate(0f, (1f - Ease.OutCubic(rise)) * 14f);

            for (int i = 0; i < _reels.Count; i++) RenderReel(_reels[i], i, before, t);

            bool won = t >= LastStopAt;
            if (before < LastStopAt && won)
            {
                GameAudio.Play(Sfx.Victory, .9f);
                GameAudio.Play(Sfx.Coins, 1f, 1.1f);
            }

            // the amount appears once the last reel lands
            float shown = Mathf.Clamp01((t - LastStopAt) / .35f);
            _amount.style.opacity = shown;
            _amount.style.scale = new Scale(Vector3.one * Mathf.Lerp(.6f, 1f, Ease.OutBack(shown, 2.4f)));
            _trophies.style.opacity = shown;
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

        // A reel runs a few whole turns more than the one before it, slows down and locks on its digit; the
        // disc gives a small bounce and throws its sparks.
        private void RenderReel(Reel reel, int index, float before, float t)
        {
            float stopAt = FirstStopAt + StopGap * index;
            int finalIndex = (BaseLoops + index) * 10 + reel.Target;
            float k = Mathf.Clamp01(t / stopAt);
            float eased = 1f - Mathf.Pow(1f - k, 4f);
            float offset = finalIndex * DigitHeight * eased;
            if (t >= stopAt) offset = finalIndex * DigitHeight - 14f * Ease.Spring((t - stopAt) / .35f, 1.5f);
            reel.Strip.style.translate = new Translate(0f, -offset);

            int shown = Mathf.FloorToInt(offset / DigitHeight + .5f);
            if (t < stopAt && shown != reel.LastShown)
            {
                reel.LastShown = shown;
                // the ticks climb the scale as the reel slows
                GameAudio.Play(Sfx.Select, .35f, GameAudio.Step(Mathf.Min(5, Mathf.FloorToInt(k * 6f))));
            }
            if (before < stopAt && t >= stopAt) GameAudio.Play(Sfx.Land, .8f);

            float landed = t - stopAt;
            float bounce = landed >= 0f && landed < .45f ? Ease.Spring(landed / .45f, 1.5f) * .06f : 0f;
            reel.Disc.style.scale = new Scale(Vector3.one * (1f + bounce));
            RenderSparks(reel, index, landed);
        }

        private static void RenderSparks(Reel reel, int index, float s)
        {
            bool live = s >= 0f && s < SparkSeconds;
            for (int i = 0; i < reel.Sparks.Count; i++)
            {
                var spark = reel.Sparks[i];
                Ui.Show(spark, live);
                if (!live) continue;
                float k = s / SparkSeconds;
                float angle = (i * 360f / SparksPerReel + index * 15f) * Mathf.Deg2Rad;
                float distance = Mathf.Lerp(SparkFrom, SparkTo, Ease.OutCubic(k));
                spark.style.translate = new Translate(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
                spark.style.opacity = k < .4f ? k / .4f : 1f - (k - .4f) / .6f;
                spark.style.scale = new Scale(Vector3.one * Mathf.Lerp(1f, .5f, k));
            }
        }

        private void SetClaimShown(bool shown)
        {
            _claimShown = shown;
            _claim.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
        }

        // Where the coins start and land, in the flight layer: the amount and the treasury's coin. Without a
        // laid-out panel (EditMode tests) there is no flight and the window simply closes.
        private bool AimFlight()
        {
            var icon = _treasury?.GoldIcon;
            if (icon == null || _flight.panel == null) return false;
            Rect source = _amount.worldBound, target = icon.worldBound;
            if (!Laid(source) || !Laid(target)) return false;
            _from = _flight.WorldToLocal(source.center);
            _to = _flight.WorldToLocal(target.center);
            return true;
        }

        private static bool Laid(Rect rect) =>
            rect.width > 0f && rect.height > 0f && !float.IsNaN(rect.x) && !float.IsNaN(rect.y);

        private void RenderClosing(float before, float c)
        {
            if (!_flying)
            {
                float k = Mathf.Clamp01(c / CloseSeconds);
                _overlay.style.opacity = 1f - k;
                _dialog.style.scale = new Scale(Vector3.one * Mathf.Lerp(1f, .9f, Ease.InCubic(k)));
                if (k >= 1f) Hide();
                return;
            }
            // the veil lifts so the treasury shows; the words leave first
            _overlay.style.opacity = 1f - Ease.OutCubic(Mathf.Clamp01((c - VeilFadeAt) / VeilFadeSeconds));
            float leave = Mathf.Clamp01(c / .25f);
            _dialog.style.opacity = 1f - leave;
            _dialog.style.scale = new Scale(Vector3.one * Mathf.Lerp(1f, .94f, Ease.OutCubic(leave)));
            for (int i = 0; i < _coins.Count; i++) RenderCoin(_coins[i], i, before, c);
            if (c >= FlightEnd) Hide();
        }

        // One coin: out of the amount to its own spot, then straight into the treasury, faster as it goes,
        // spinning and shrinking, two fading copies behind it on the same line. The counter bumps as it lands.
        private void RenderCoin(Coin coin, int index, float before, float c)
        {
            float start = FlightStart + index * CoinStagger;
            float landAt = start + BurstSeconds + FlySeconds;
            if (before < landAt && c >= landAt)
            {
                UiMotion.Punch(_treasury.GoldIcon, .14f, .2f);
                if (index % 3 == 0) GameAudio.Play(Sfx.Coins, .4f, GameAudio.Step(index / 3 % 5));
            }
            float s = c - start;
            bool live = s >= 0f && s < BurstSeconds + FlySeconds;
            Ui.Show(coin.Body, live);
            if (!live)
            {
                foreach (var ghost in coin.Trail) Ui.Show(ghost, false);
                return;
            }
            Vector2 scatter = _from + coin.Scatter;
            if (s < BurstSeconds)
            {
                float k = s / BurstSeconds;
                Place(coin.Body, Vector2.Lerp(_from, scatter, Ease.OutCubic(k)), Mathf.LerpUnclamped(.3f, 1f, Ease.OutBack(k, 2f)), 0f, 1f);
                foreach (var ghost in coin.Trail) Ui.Show(ghost, false);
                return;
            }
            float f = (s - BurstSeconds) / FlySeconds;
            float spin = (index % 2 == 0 ? 1f : -1f) * 260f;
            Place(coin.Body, Vector2.Lerp(scatter, _to, f * f * f), Mathf.Lerp(1f, .5f, f), f * spin, 1f);
            for (int g = 0; g < coin.Trail.Length; g++)
            {
                float lag = f - TrailLag * (g + 1);
                var ghost = coin.Trail[g];
                Ui.Show(ghost, lag > 0f);
                if (lag <= 0f) continue;
                Place(ghost, Vector2.Lerp(scatter, _to, lag * lag * lag), Mathf.Lerp(.85f, .45f, lag) - .12f * g, lag * spin,
                    .45f / (g + 1));
            }
        }

        private static void Place(VisualElement element, Vector2 point, float scale, float rotate, float opacity)
        {
            element.style.left = point.x;
            element.style.top = point.y;
            element.style.scale = new Scale(Vector3.one * Mathf.Max(.001f, scale));
            element.style.rotate = new Rotate(rotate);
            element.style.opacity = opacity;
        }

        private void StopFlight()
        {
            _flying = false;
            foreach (var coin in _coins)
            {
                Ui.Show(coin.Body, false);
                foreach (var ghost in coin.Trail) Ui.Show(ghost, false);
            }
        }

        // The arena's trophies under the amount: a picture and a number each, then where they wait.
        private void BuildTrophies(BattleRewardSnapshot reward)
        {
            _trophies.Clear();
            var words = new List<string>();
            foreach (var trophy in reward.Trophies)
            {
                var icon = _context.Catalog.TryGetResource(trophy.Resource)?.Icon;
                if (icon != null)
                {
                    var image = new Image { sprite = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    image.AddToClassList("jackpot__trophy-icon");
                    _trophies.Add(image);
                }
                _trophies.Add(Ui.Text($"+{trophy.Amount}", "jackpot__trophy-count t-black"));
                words.Add($"{trophy.Name} +{trophy.Amount}");
            }
            if (words.Count > 0) _trophies.Add(Ui.Text("ждут в бараках", "jackpot__trophy-note t-medium"));
            TrophyText = string.Join(", ", words);
            Ui.Show(_trophies, words.Count > 0);
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
                var disc = Ui.Box("jackpot__reel");
                disc.pickingMode = PickingMode.Ignore;
                var window = Ui.Box("jackpot__window");
                window.pickingMode = PickingMode.Ignore;
                var strip = Ui.Box("jackpot__strip");
                strip.pickingMode = PickingMode.Ignore;
                int length = (BaseLoops + i + 1) * 10;
                for (int d = 0; d < length; d++)
                {
                    var digit = Ui.Text((d % 10).ToString(), "jackpot__digit t-black");
                    digit.pickingMode = PickingMode.Ignore;
                    strip.Add(digit);
                }
                window.Add(strip);
                disc.Add(window);
                var burst = Ui.Box("jackpot__burst");
                burst.pickingMode = PickingMode.Ignore;
                disc.Add(burst);
                var reel = new Reel { Disc = disc, Strip = strip, Target = digits[i] - '0' };
                for (int s = 0; s < SparksPerReel; s++)
                {
                    var spark = Ui.Box("jackpot__spark");
                    spark.pickingMode = PickingMode.Ignore;
                    Ui.Show(spark, false);
                    burst.Add(spark);
                    reel.Sparks.Add(spark);
                }
                _reelsRoot.Add(disc);
                _reels.Add(reel);
            }
        }

        private void BuildCoins()
        {
            var sprite = RewardArt.Coin(_context.Catalog);
            for (int i = 0; i < FlyingCoins; i++)
            {
                // each coin bursts to its own spot round the amount: a golden-angle spiral, wider than tall
                float angle = i * 137.5f * Mathf.Deg2Rad;
                float radius = 46f + i * 37 % 64;
                var coin = new Coin
                {
                    Body = FlyingCoin(sprite, "jackpot__flying"),
                    Trail = new Image[TrailLength],
                    Scatter = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .6f),
                };
                for (int g = 0; g < TrailLength; g++) coin.Trail[g] = FlyingCoin(sprite, "jackpot__flying jackpot__flying--trail");
                // the trail goes under its coin
                foreach (var ghost in coin.Trail) ghost.SendToBack();
                _coins.Add(coin);
            }
        }

        private Image FlyingCoin(Sprite sprite, string classes)
        {
            var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            foreach (var name in classes.Split(' ')) image.AddToClassList(name);
            Ui.Show(image, false);
            _flight.Add(image);
            return image;
        }
    }
}
