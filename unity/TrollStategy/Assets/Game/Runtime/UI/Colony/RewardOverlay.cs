using System.Collections.Generic;
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
    /// The reward reveal for a finished quest: a disc in the light, with no card around it. The island goes
    /// under a deep veil and the quest's name rises at the top. A large white disc pops up in the middle with
    /// the reward's dark silhouette, which brightens while a soft glow breathes and pale rays turn behind it;
    /// a chime marks the moment. A building turns as its 3D model, a creature or gold shows as its picture.
    /// Below it, as text on the veil: the kind of reward, its name, its facts (a building's size, workers and
    /// recipes as pictures and numbers) and where to find it, then a button to take it. Taking it sends the
    /// claim; the session applies the rewards. Everything is a function of the time since opening, advanced
    /// by <see cref="Tick"/> on unscaled time, so EditMode tests play it without a panel. A click on the disc
    /// skips to the end.
    /// </summary>
    public sealed class RewardOverlay
    {
        // Timeline, in seconds since the reveal opened.
        private const float HeadAt = .1f;
        private const float RevealAt = .2f;
        private const float RevealSeconds = .6f;
        private const float ChimeAt = .55f;
        private const float DetailsAt = .7f;
        private const float DetailStep = .08f;
        private const float RiseSeconds = .3f;
        private const float ReadyAt = 1.05f;
        private const float CloseSeconds = .22f;
        // The rays turn this many degrees a second; the glow swells by this share and back.
        private const float RaySpeed = 9f;
        private const float GlowBreath = .06f;
        // How tightly the turning model fills the disc (see BuildingShowcase.Show).
        private const float ModelFraming = 1.12f;
        private static readonly Color Silhouette = new(.08f, .09f, .11f, 1f);

        private readonly ColonyHudContext _context;
        private readonly ShowcasePanel _catalogShowcase;
        private readonly BuildingShowcase _renderer;
        private readonly VisualElement _overlay;
        private readonly VisualElement _dialog;
        private readonly VisualElement _disc;
        private readonly VisualElement _prize;
        private readonly VisualElement _glow;
        private readonly VisualElement _rays;
        private readonly VisualElement _facts;
        private readonly VisualElement _extras;
        private readonly Label _eyebrow;
        private readonly Label _quest;
        private readonly Label _kind;
        private readonly Label _name;
        private readonly Label _description;
        private readonly Label _where;
        private readonly Button _claim;
        private readonly VisualElement[] _head;
        private readonly VisualElement[] _details;
        private readonly List<string> _factTexts = new();
        private VisualElement _line;

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
            _disc = Ui.Require<VisualElement>(root, "reward-disc");
            _prize = Ui.Require<VisualElement>(root, "reward-prize");
            _glow = Ui.Require<VisualElement>(root, "reward-glow");
            _rays = Ui.Require<VisualElement>(root, "reward-rays");
            _facts = Ui.Require<VisualElement>(root, "reward-facts");
            _extras = Ui.Require<VisualElement>(root, "reward-extras");
            _eyebrow = Ui.Require<Label>(root, "reward-eyebrow");
            _quest = Ui.Require<Label>(root, "reward-quest");
            _kind = Ui.Require<Label>(root, "reward-kind");
            _name = Ui.Require<Label>(root, "reward-name");
            _description = Ui.Require<Label>(root, "reward-description");
            _where = Ui.Require<Label>(root, "reward-where");
            _claim = UiFeel.Bind(Ui.Require<Button>(root, "reward-claim"), Claim, silentClick: true);
            _head = new VisualElement[] { _eyebrow, _quest };
            _details = new VisualElement[] { _kind, _name, _facts, _description, _where, _extras };

            stage.RegisterCallback<PointerDownEvent>(_ => Skip());
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => _time >= 0f;
        /// <summary>The reward is shown and can be taken.</summary>
        public bool IsReady => IsOpen && _closing < 0f && _time >= ReadyAt;
        public string QuestId => _shown?.Id;
        public string RewardName => _name.text;
        public string RewardKind => _kind.text;
        /// <summary>A building's facts under its name: size, workers, then each recipe, as words.</summary>
        public IReadOnlyList<string> Facts => _factTexts;
        /// <summary>Where the reward is found now, with its price.</summary>
        public string Where => _where.text;
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
                ? $"Обучение: уровень {quest.Level} пройден"
                : $"Уровень {quest.Level} пройден");
            Ui.SetText(_quest, quest.Title);
            Ui.SetText(_kind, _headline?.Caption ?? string.Empty);
            Ui.SetText(_name, _headline?.Title ?? string.Empty);
            Describe(_headline);
            BuildExtras(quest);
            ShowPrize();
            SetClaimShown(false);
            Ui.Show(_overlay, true);
            _overlay.BringToFront();
            Render(0f);
            GameAudio.Play(Sfx.Select, .6f, GameAudio.Step(-1));
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
            _dialog.style.scale = StyleKeyword.Null;
            foreach (var element in _head) Rise(element, t - HeadAt);

            // the disc pops up; the picture steps out of its silhouette (the model's has an opaque backdrop, so it fades in)
            float pop = Mathf.Clamp01((t - RevealAt) / RevealSeconds);
            _disc.style.opacity = Mathf.Clamp01(pop / .25f);
            _disc.style.scale = new Scale(Vector3.one * Mathf.Max(.001f, Mathf.LerpUnclamped(.45f, 1f, Ease.OutBack(pop, 1.8f))));
            float reveal = Ease.OutCubic(pop);
            if (_prizeIsModel) _prize.style.opacity = reveal;
            else if (_prizeImage != null) _prizeImage.tintColor = Color.Lerp(Silhouette, Color.white, reveal);
            float drift = t > ReadyAt ? Mathf.Sin((t - ReadyAt) * 1.6f) * 3f : 0f;
            _disc.style.translate = new Translate(0f, drift);

            // the light behind it: a glow that breathes, pale rays that barely turn
            float glow = Mathf.Clamp01((t - .25f) / .6f);
            _glow.style.opacity = glow;
            _glow.style.scale = new Scale(Vector3.one * (1f + GlowBreath * glow * Mathf.Sin(t * 2f)));
            _rays.style.opacity = Mathf.Clamp01((t - .35f) / .8f);
            _rays.style.rotate = new Rotate(t * RaySpeed);

            for (int i = 0; i < _details.Length; i++) Rise(_details[i], t - DetailsAt - i * DetailStep);
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

        // A line of text comes up from a little below as it fades in.
        private static void Rise(VisualElement element, float since)
        {
            float k = Mathf.Clamp01(since / RiseSeconds);
            element.style.opacity = k;
            element.style.translate = new Translate(0f, (1f - Ease.OutCubic(k)) * 14f);
        }

        // The button keeps its place while hidden, so the column does not jump when it appears.
        private void SetClaimShown(bool shown)
        {
            _claimShown = shown;
            _claim.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
        }

        private void RenderClosing()
        {
            float k = Mathf.Clamp01(_closing / CloseSeconds);
            _overlay.style.opacity = 1f - k;
            _dialog.style.scale = new Scale(Vector3.one * Mathf.Lerp(1f, .96f, Ease.InCubic(k)));
            if (k >= 1f) Hide();
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

        // What the reward is and where to find it. A building speaks in facts from its content; the session's
        // description stays for what has no facts (a creature, a battle, gold, a building that only stores).
        private void Describe(RewardSnapshot reward)
        {
            _facts.Clear();
            _factTexts.Clear();
            _line = null;
            _facts.RemoveFromClassList("reward__facts--chain");
            string description = reward?.Description ?? string.Empty;
            string where = string.Empty;
            var session = _context.Session;
            switch (reward?.Kind)
            {
                case QuestRewardKind.UnlockBuilding:
                    var building = Building(reward.Reward.Building);
                    if (building == null) break;
                    where = $"Уже в каталоге, вкладка «Здания», {session.BuildingPrice(building.Kind)} золота";
                    if (building.Recipes.Count == 0)
                    {
                        description = session.DescribeBuilding(building);
                        break;
                    }
                    description = string.Empty;
                    AddChain(building);
                    break;
                case QuestRewardKind.UnlockUnit:
                    var unit = Unit(reward.Reward.Unit);
                    if (unit == null) break;
                    where = $"Уже в каталоге, вкладка «Существа», {session.HirePrice(unit.Kind)} золота";
                    if (!string.IsNullOrWhiteSpace(unit.Description)) description = unit.Description.Trim();
                    break;
            }
            Ui.SetText(_description, description);
            Ui.Show(_description, !string.IsNullOrEmpty(description));
            Ui.SetText(_where, where);
            Ui.Show(_where, !string.IsNullOrEmpty(where));
            Ui.Show(_facts, _facts.childCount > 0);
        }

        private void AddFact(string glyph, string text)
        {
            var fact = Fact();
            var icon = Ui.Box("glyph " + glyph);
            icon.pickingMode = PickingMode.Ignore;
            fact.Add(icon);
            fact.Add(FactText(text));
            _factTexts.Add(text);
        }

        // The building's place in the chain, one line each: its size and crew, its main recipe, where its goods
        // come from and where they go, what it finds by chance and spoils. The other recipes are counted, not
        // drawn: the card of the building lists them.
        private void AddChain(BuildingDefinition building)
        {
            _facts.AddToClassList("reward__facts--chain");
            Line(null);
            AddFact("glyph--grid", $"{building.Width}×{building.Height}");
            if (building.MaxWorkers > 0) AddFact("glyph--idle", $"до {building.MaxWorkers} рабочих");

            var main = GameSession.MainRecipe(building);
            Line(main.Inputs.Length > 0 ? "Делает" : "Добывает");
            AddRecipe(main);

            var sources = Four(ChainLinks.Makers(_context.Catalog, ChainLinks.Kinds(main.Inputs), building.Kind));
            if (sources.Count > 0)
            {
                Line("Откуда сырьё");
                foreach (var source in sources) AddBuilding(source);
            }
            var users = Four(ChainLinks.Takers(_context.Catalog, ChainLinks.Kinds(main.Outputs), building.Kind));
            if (users.Count > 0)
            {
                Line("Куда товар");
                foreach (var user in users) AddBuilding(user);
            }
            if (main.Extras.Length > 0)
            {
                Line("Иногда");
                foreach (var extra in main.Extras)
                {
                    var bonus = Fact();
                    string text = Amount(bonus, extra.Output, "+");
                    string chance = extra.MinLevel > 1 ? $"{extra.ChancePercent}%, с {extra.MinLevel} уровня" : $"{extra.ChancePercent}%";
                    var note = FactText(chance);
                    note.AddToClassList("reward__fact-note");
                    bonus.Add(note);
                    _factTexts.Add(text + " " + chance);
                }
            }
            if (main.FailChancePercent > 0 && main.FailOutputs.Length > 0)
            {
                Line($"Брак {main.FailChancePercent}%");
                _factTexts.Add(Amount(Fact(), main.FailOutputs[0], string.Empty));
            }
            int later = building.Recipes.Count - 1;
            if (later > 0)
            {
                Line(null);
                string more = $"Других рецептов: {later}, они в карточке здания";
                var note = FactText(more);
                note.AddToClassList("reward__fact-note");
                Fact().Add(note);
                _factTexts.Add(more);
            }
            _line = null;
        }

        // at most four neighbours a line, in catalog order
        private static List<BuildingDefinition> Four(List<BuildingDefinition> list)
        {
            if (list.Count > 4) list.RemoveRange(4, list.Count - 4);
            return list;
        }

        private void AddBuilding(BuildingDefinition building)
        {
            var fact = Fact();
            var icon = RewardArt.BuildingIcon(building);
            if (icon != null)
            {
                var art = new Image { sprite = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                art.AddToClassList("reward__fact-building");
                fact.Add(art);
            }
            fact.Add(FactText(building.DisplayName));
            _factTexts.Add(building.DisplayName);
        }

        // A new line of facts with its caption at the left; without a caption the line just starts.
        private void Line(string caption)
        {
            _line = Ui.Box("reward__line");
            _line.pickingMode = PickingMode.Ignore;
            _facts.Add(_line);
            if (string.IsNullOrEmpty(caption)) return;
            var label = Ui.Text(caption, "reward__line-caption t-bold");
            label.pickingMode = PickingMode.Ignore;
            _line.Add(label);
        }

        // A recipe as pictures and numbers: what goes in, an arrow, what comes out.
        private void AddRecipe(ProductionRecipe recipe)
        {
            var fact = Fact();
            var words = new List<string>();
            foreach (var input in recipe.Inputs) words.Add(Amount(fact, input, string.Empty));
            if (recipe.Inputs.Length > 0)
            {
                fact.Add(Ui.Text("→", "reward__fact-arrow t-bold"));
                words.Add("→");
            }
            foreach (var output in recipe.Outputs) words.Add(Amount(fact, output, string.Empty));
            if (recipe.MinLevel > 1)
            {
                var level = FactText($"с {recipe.MinLevel} уровня");
                level.AddToClassList("reward__fact-note");
                fact.Add(level);
                words.Add($"с {recipe.MinLevel} уровня");
            }
            _factTexts.Add(string.Join(" ", words));
        }

        private string Amount(VisualElement fact, ResourceAmount amount, string sign)
        {
            var resource = _context.Catalog.TryGetResource(amount.Resource);
            string name = resource?.DisplayName ?? amount.Resource.ToString();
            if (resource?.Icon != null)
            {
                var icon = new Image { sprite = resource.Icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("reward__fact-icon");
                fact.Add(icon);
                fact.Add(FactText(sign + amount.Amount));
            }
            else
            {
                fact.Add(FactText($"{sign}{amount.Amount} {name}"));
            }
            return $"{sign}{amount.Amount} {name}";
        }

        private VisualElement Fact()
        {
            var fact = Ui.Box("reward__fact");
            fact.pickingMode = PickingMode.Ignore;
            (_line ?? _facts).Add(fact);
            return fact;
        }

        private static Label FactText(string text)
        {
            var label = Ui.Text(text, "reward__fact-text t-medium");
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        private void BuildExtras(QuestSnapshot quest)
        {
            _extras.Clear();
            foreach (var reward in quest.Rewards)
            {
                if (reward == _headline) continue;
                string text = reward.Kind == QuestRewardKind.Gold ? "+" + reward.Title : "Открыто: " + reward.Title;
                _extras.Add(Ui.Text(text, "reward__extra t-bold"));
            }
            Ui.Show(_extras, _extras.childCount > 0);
        }

        private BuildingDefinition Building(BuildingKind kind)
        {
            foreach (var building in _context.Catalog.Buildings)
                if (building != null && building.Kind == kind) return building;
            return null;
        }

        private UnitDefinition Unit(UnitKind kind)
        {
            foreach (var unit in _context.Catalog.Units)
                if (unit != null && unit.Kind == kind) return unit;
            return null;
        }
    }
}
