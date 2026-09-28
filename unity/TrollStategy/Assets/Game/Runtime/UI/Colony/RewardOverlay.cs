using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Buildings;
using TrollStrategy.Presentation.Feel;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The reward reveal for a finished quest. The card opens on the reward's dark silhouette in a round frame;
    /// it brightens and rises while a soft glow fades in behind it and a chime marks the moment. A building
    /// then turns as its 3D model, a creature or gold shows as its picture, with its name, what it gives and a
    /// button to take it. Taking it sends the claim; the session applies the rewards. Everything is a function
    /// of the time since opening, advanced by <see cref="Tick"/> on unscaled time, so EditMode tests play it
    /// without a panel. A click on the picture skips to the end.
    /// </summary>
    public sealed class RewardOverlay
    {
        // Timeline, in seconds since the reveal opened.
        private const float OpenSeconds = .3f;
        private const float RevealAt = .2f;
        private const float RevealSeconds = .6f;
        private const float ChimeAt = .55f;
        private const float DetailsAt = .7f;
        private const float ReadyAt = 1.05f;
        private const float CloseSeconds = .22f;

        // Must match Progression.uss: .reward__stage and .reward__spark.
        private const int SparkCount = 10;
        private const int RayCount = 8;
        // How tightly the turning model fills the frame (see BuildingShowcase.Show).
        private const float ModelFraming = 1.12f;
        private static readonly Color Silhouette = new(.08f, .09f, .11f, 1f);

        private readonly ColonyHudContext _context;
        private readonly ShowcasePanel _catalogShowcase;
        private readonly BuildingShowcase _renderer;
        private readonly VisualElement _overlay;
        private readonly VisualElement _dialog;
        private readonly VisualElement _window;
        private readonly VisualElement _prize;
        private readonly VisualElement _rays;
        private readonly VisualElement _extras;
        private readonly Label _eyebrow;
        private readonly Label _quest;
        private readonly Label _kind;
        private readonly Label _name;
        private readonly Label _description;
        private readonly Button _claim;
        private readonly VisualElement[] _details;
        private readonly System.Collections.Generic.List<Image> _sparks = new();

        private QuestSnapshot _shown;
        private RewardSnapshot _headline;
        private Image _prizeImage;
        private float _time = -1f;
        private float _closing = -1f;
        private int _shownGold;
        private bool _prizeIsModel;
        private bool _claimShown;

        public RewardOverlay(VisualElement root, ColonyHudContext context, ShowcasePanel catalogShowcase)
        {
            _context = context;
            _catalogShowcase = catalogShowcase;
            _renderer = context.Showcase;
            _overlay = Ui.Require<VisualElement>(root, "reward-overlay");
            _dialog = Ui.Require<VisualElement>(root, "reward-dialog");
            var stage = Ui.Require<VisualElement>(root, "reward-stage");
            _window = Ui.Require<VisualElement>(root, "reward-window");
            _prize = Ui.Require<VisualElement>(root, "reward-prize");
            _rays = Ui.Require<VisualElement>(root, "reward-rays");
            _extras = Ui.Require<VisualElement>(root, "reward-extras");
            _eyebrow = Ui.Require<Label>(root, "reward-eyebrow");
            _quest = Ui.Require<Label>(root, "reward-quest");
            _kind = Ui.Require<Label>(root, "reward-kind");
            _name = Ui.Require<Label>(root, "reward-name");
            _description = Ui.Require<Label>(root, "reward-description");
            _claim = UiFeel.Bind(Ui.Require<Button>(root, "reward-claim"), Claim, silentClick: true);
            _details = new VisualElement[] { _kind, _name, _description, _extras };

            stage.RegisterCallback<PointerDownEvent>(_ => Skip());
            BuildRays();
            BuildSparks(Ui.Require<VisualElement>(root, "reward-sparks"));
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => _time >= 0f;
        /// <summary>The reward is shown and can be taken.</summary>
        public bool IsReady => IsOpen && _closing < 0f && _time >= ReadyAt;
        public string QuestId => _shown?.Id;
        public string RewardName => _name.text;
        public string RewardKind => _kind.text;
        public Button ClaimButton => _claim;

        public void Open(QuestSnapshot quest)
        {
            if (quest == null) return;
            _shown = quest;
            _headline = quest.Headline;
            // the catalog preview and the reveal share one model renderer
            _catalogShowcase?.Hide();
            _time = 0f;
            _closing = -1f;
            _shownGold = -1;

            Ui.SetText(_eyebrow, quest.IsTutorial
                ? $"ОБУЧЕНИЕ · УРОВЕНЬ {quest.Level} ПРОЙДЕН"
                : $"УРОВЕНЬ {quest.Level} ПРОЙДЕН");
            Ui.SetText(_quest, quest.Title);
            Ui.SetText(_kind, _headline?.Caption ?? string.Empty);
            Ui.SetText(_name, _headline?.Title ?? string.Empty);
            Ui.SetText(_description, Describe(_headline));
            BuildExtras(quest);
            ShowPrize();
            SetClaimShown(false);
            Ui.Show(_overlay, true);
            _overlay.BringToFront();
            Render(0f);
            GameAudio.Play(Sfx.Select, .6f, .9f);
        }

        /// <summary>Shows everything at once.</summary>
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
                RenderClosing();
                return;
            }
            Advance(unscaledDeltaTime);
            if (_prizeIsModel) _renderer?.Tick(unscaledDeltaTime);
        }

        /// <summary>Takes the reward away without claiming it (the HUD was hidden or rebuilt).</summary>
        public void Hide()
        {
            _time = -1f;
            _closing = -1f;
            _shown = null;
            if (_prizeIsModel) _renderer?.Hide();
            _prizeIsModel = false;
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
            var result = _context.Interaction.ClaimQuestReward();
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
            if (before < ChimeAt && _time >= ChimeAt) GameAudio.Play(Sfx.Upgrade, .8f);
            Render(_time);
        }

        private void Render(float t)
        {
            _overlay.style.opacity = Mathf.Clamp01(t / .2f);
            float open = Mathf.Clamp01(t / OpenSeconds);
            _dialog.style.scale = new Scale(Vector3.one * Mathf.Max(.001f, Ease.OutBack(open, 1.2f)));

            // the reward steps out of its silhouette and rises a little
            float reveal = Mathf.Clamp01((t - RevealAt) / RevealSeconds);
            // a picture steps out of its silhouette; the model's picture has an opaque backdrop, so it fades in
            if (_prizeIsModel) _prize.style.opacity = Ease.OutCubic(reveal);
            else if (_prizeImage != null) _prizeImage.tintColor = Color.Lerp(Silhouette, Color.white, Ease.OutCubic(reveal));
            float rise = Ease.OutBack(reveal, 1.2f);
            float drift = t > ReadyAt ? Mathf.Sin((t - ReadyAt) * 1.6f) * 3f : 0f;
            _prize.style.scale = new Scale(Vector3.one * Mathf.Lerp(.86f, 1f, rise));
            _prize.style.translate = new Translate(0f, Mathf.Lerp(10f, 0f, rise) + drift);
            _window.EnableInClassList("is-revealed", reveal >= 1f);

            // a soft glow behind the frame, barely turning
            float glow = Mathf.Clamp01((t - RevealAt) / .5f);
            _rays.style.opacity = glow;
            _rays.style.rotate = new Rotate(t * 6f);

            RenderSparks(t - ChimeAt);

            float details = Mathf.Clamp01((t - DetailsAt) / .3f);
            foreach (var element in _details)
            {
                element.style.opacity = details;
                element.style.translate = new Translate(0f, (1f - Ease.OutCubic(details)) * 10f);
            }
            if (_headline != null && _headline.Kind == QuestRewardKind.Gold)
            {
                // gold counts up as it is revealed; the text changes only with the number
                int shown = Mathf.RoundToInt(_headline.Reward.Gold * Mathf.Clamp01((t - DetailsAt) / .5f));
                if (shown != _shownGold)
                {
                    _shownGold = shown;
                    Ui.SetText(_name, $"{shown} золота");
                }
            }

            bool ready = t >= ReadyAt;
            if (ready != _claimShown)
            {
                SetClaimShown(ready);
                if (ready) UiMotion.PopIn(_claim, .25f);
            }
        }

        // The button keeps its place while hidden, so the dialog does not jump when it appears.
        private void SetClaimShown(bool shown)
        {
            _claimShown = shown;
            _claim.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
        }

        private void RenderClosing()
        {
            float k = Mathf.Clamp01(_closing / CloseSeconds);
            _overlay.style.opacity = 1f - k;
            _dialog.style.scale = new Scale(Vector3.one * Mathf.Lerp(1f, .9f, Ease.InCubic(k)));
            if (k >= 1f) Hide();
        }

        // A few motes drift out from the frame as the reward appears.
        private void RenderSparks(float s)
        {
            bool live = s >= 0f && s < 1.2f;
            for (int i = 0; i < _sparks.Count; i++)
            {
                var spark = _sparks[i];
                Ui.Show(spark, live);
                if (!live) continue;
                float k = s / 1.2f;
                float angle = (i * 360f / SparkCount + 18f) * Mathf.Deg2Rad;
                float distance = (118f + i * 29 % 40) * Ease.OutCubic(k);
                spark.style.translate = new Translate(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
                spark.style.opacity = 1f - k * k;
                spark.style.scale = new Scale(Vector3.one * Mathf.Lerp(.9f, .3f, k));
            }
        }

        private void ShowPrize()
        {
            _prize.Clear();
            _prize.style.backgroundImage = StyleKeyword.Null;
            _prize.style.opacity = StyleKeyword.Null;
            _prizeImage = null;
            _prizeIsModel = false;
            if (_headline != null && _headline.Kind == QuestRewardKind.UnlockBuilding && _renderer != null)
            {
                var texture = _renderer.Show(Building(_headline.Reward.Building), ModelFraming) as RenderTexture;
                if (texture != null)
                {
                    _prize.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
                    _prizeIsModel = true;
                    return;
                }
            }
            var sprite = RewardArt.For(_headline, _context.Catalog);
            if (sprite != null)
            {
                _prizeImage = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                _prizeImage.AddToClassList("reward__prize-image");
                _prize.Add(_prizeImage);
                return;
            }
            string title = _headline?.Title;
            var monogram = Ui.Text(string.IsNullOrEmpty(title) ? "?" : title.Substring(0, 1).ToUpperInvariant(),
                "reward__monogram t-bold");
            monogram.pickingMode = PickingMode.Ignore;
            _prize.Add(monogram);
        }

        private void BuildExtras(QuestSnapshot quest)
        {
            _extras.Clear();
            foreach (var reward in quest.Rewards)
            {
                if (reward == _headline) continue;
                string text = reward.Kind == QuestRewardKind.Gold ? "+" + reward.Title : "Открыто: " + reward.Title;
                _extras.Add(Ui.Text(text, "reward__extra t-medium"));
            }
            Ui.Show(_extras, _extras.childCount > 0);
        }

        private void BuildRays()
        {
            for (int i = 0; i < RayCount; i++)
            {
                var ray = Ui.Box("reward__ray");
                ray.style.rotate = new Rotate(i * 180f / RayCount);
                _rays.Add(ray);
            }
        }

        private void BuildSparks(VisualElement parent)
        {
            for (int i = 0; i < SparkCount; i++)
            {
                var spark = new Image { sprite = FeelSprites.Spark, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                spark.AddToClassList("reward__spark");
                if (i % 2 == 1) spark.AddToClassList("reward__spark--pale");
                Ui.Show(spark, false);
                parent.Add(spark);
                _sparks.Add(spark);
            }
        }

        private string Describe(RewardSnapshot reward)
        {
            if (reward == null) return string.Empty;
            if (reward.Kind != QuestRewardKind.UnlockUnit) return reward.Description;
            foreach (var unit in _context.Catalog.Units)
                if (unit != null && unit.Kind == reward.Reward.Unit)
                    return string.IsNullOrEmpty(reward.Description)
                        ? UnitStatsText.Compact(unit)
                        : reward.Description + "\n" + UnitStatsText.Compact(unit);
            return reward.Description;
        }

        private BuildingDefinition Building(BuildingKind kind)
        {
            foreach (var building in _context.Catalog.Buildings)
                if (building != null && building.Kind == kind) return building;
            return null;
        }
    }
}
