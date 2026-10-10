using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Support;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The notice that closes a short public build once its last quest is taken (Progression.asset names it for the
    /// itch.io alpha and for the Steam demo): the version is done, what the full game adds as pictures of the
    /// buildings and creatures still closed, the way to Steam (hidden while <see cref="GameLinks.SteamPage"/> is
    /// empty), in the alpha a request to rate the game on itch.io, and where to find the author and other players. «Остаться на острове» closes it and the colony goes on
    /// without quests; it opens once a game. It lives in the intro's document, beside the notice that opens a version.
    /// </summary>
    public sealed class DemoEndPanel
    {
        public const string AlphaBody =
            "Вы прошли обучение, и на этом альфа-версия кончается. В полной игре колония растёт дальше: " +
            "новые здания, существа и уровни арены.";
        public const string DemoBody =
            "На этом демо-версия кончается. В полной игре колония растёт дальше: новые здания, существа и уровни арены.";
        public const string StayNote =
            "Можно остаться на острове: колония живёт, рынок и арена работают, но новых заданий в этой версии нет.";
        // whole pictures in a row; the rest are counted in a chip at its end
        private const int RowLimit = 7;

        private readonly ColonyHudContext _context;
        private readonly HudTooltip _tooltip;
        private readonly VisualElement _overlay;
        private readonly Label _title;
        private readonly Label _body;
        private readonly VisualElement _buildings;
        private readonly VisualElement _creatures;
        private readonly Button _steam;
        private readonly Label _steamCaption;
        private readonly VisualElement _steamGlyph;
        private readonly VisualElement _rate;

        public DemoEndPanel(VisualElement root, ColonyHudContext context, HudTooltip tooltip)
        {
            _context = context;
            _tooltip = tooltip;
            _overlay = Ui.Require<VisualElement>(root, "end-overlay");
            _title = Ui.Require<Label>(root, "end-title");
            _body = Ui.Require<Label>(root, "end-body");
            _buildings = Ui.Require<VisualElement>(root, "end-buildings");
            _creatures = Ui.Require<VisualElement>(root, "end-creatures");
            Ui.SetText(Ui.Require<Label>(root, "end-note"), StayNote);
            GameLinks.AddRows(Ui.Require<VisualElement>(root, "end-links"));

            _rate = Ui.Require<VisualElement>(root, "end-rate");
            RateButton = Ui.CaptionButton("Оценить на itch.io", null, "btn end-rate__button");
            RateButton.Insert(0, GameLinks.Glyph("external", "end-rate__glyph"));
            UiFeel.Bind(RateButton, GameLinks.OpenItchRating);
            Ui.Require<VisualElement>(root, "end-rate-action").Add(RateButton);

            var actions = Ui.Require<VisualElement>(root, "end-actions");
            StayButton = Ui.CaptionButton("Остаться на острове", "Esc", "btn btn--primary intro-play");
            UiFeel.Bind(StayButton, Close, Sfx.UiClick);
            actions.Add(StayButton);
            _steam = Ui.CaptionButton(string.Empty, null, "btn intro-steam");
            _steamGlyph = GameLinks.Glyph("external", "intro-steam__glyph");
            _steam.Insert(0, _steamGlyph);
            _steamCaption = _steam.Q<Label>(className: "btn__caption");
            UiFeel.Bind(_steam, GameLinks.OpenSteamPage);
            actions.Add(_steam);

            SetEdition(context.Edition);
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => Ui.IsShown(_overlay);
        /// <summary>The notice has opened in this game; it does not open again.</summary>
        public bool WasOffered { get; private set; }
        public BuildEdition Edition { get; private set; }
        public string Title => _title.text;
        public Button StayButton { get; }
        /// <summary>"Демо в Steam" in the alpha, "В желаемое" in the demo; hidden without a Steam page.</summary>
        public Button SteamButton => _steam;
        public bool OffersSteam => Ui.IsShown(_steam);
        /// <summary>«Оценить на itch.io»: the alpha's only, and only with <see cref="GameLinks.ItchPage"/> set.</summary>
        public Button RateButton { get; }
        public bool OffersRating => Ui.IsShown(_rate);
        /// <summary>Pictures of the buildings the full game adds; the chip with the rest is not counted.</summary>
        public int BuildingsShown => Pictures(_buildings);
        /// <summary>Pictures of the creatures still closed; the chip with the rest is not counted.</summary>
        public int CreaturesShown => Pictures(_creatures);

        /// <summary>The words of one edition; the constructor takes the build's, tools show the other.</summary>
        public void SetEdition(BuildEdition edition)
        {
            Edition = edition;
            bool demo = edition == BuildEdition.SteamDemo;
            Ui.SetText(_title, demo ? "Демо-версия пройдена" : "Альфа-версия пройдена");
            Ui.SetText(_body, demo ? DemoBody : AlphaBody);
            Ui.SetText(_steamCaption, demo ? "В желаемое" : "Демо в Steam");
            _steamGlyph.EnableInClassList("glyph--heart", demo);
            _steamGlyph.EnableInClassList("glyph--external", !demo);
            Ui.Show(_steam, GameLinks.HasSteamPage);
            Ui.Show(_rate, edition == BuildEdition.Alpha && GameLinks.HasItchPage);
        }

        /// <summary>Opens the notice the first time the snapshot says the version is over; returns whether it opened.</summary>
        public bool Offer(GameSnapshot snapshot)
        {
            if (WasOffered || snapshot?.Progress == null || !snapshot.Progress.Over) return false;
            Open(snapshot);
            return true;
        }

        /// <summary>Shows the notice with what the snapshot still keeps closed, over or not (screenshots).</summary>
        public void Open(GameSnapshot snapshot)
        {
            WasOffered = true;
            ShowClosed(snapshot.Progress);
            Ui.Show(_overlay, true);
            UiMotion.PopIn(_overlay, .25f);
            GameAudio.Play(Sfx.Victory, .7f);
        }

        /// <summary>«Остаться на острове»: the colony goes on behind it.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            _tooltip?.Dismiss();
            Ui.Show(_overlay, false);
        }

        // The buildings the rest of the chain opens, in its order, and the creatures still closed, in the catalog's.
        private void ShowClosed(ProgressSnapshot progress)
        {
            var catalog = _context.Catalog;
            var chain = catalog.Progression != null ? catalog.Progression.Quests : Array.Empty<QuestDefinition>();
            int from = progress.LastLevel > 0 ? progress.LastLevel : Math.Max(0, progress.Level - 1);
            var buildings = new List<(Sprite, string, string)>();
            var seen = new HashSet<BuildingKind>();
            for (int i = from; i < chain.Count; i++)
            {
                if (chain[i] == null) continue;
                foreach (var reward in chain[i].Rewards)
                {
                    if (reward.Kind != QuestRewardKind.UnlockBuilding || progress.IsBuildingUnlocked(reward.Building) ||
                        !seen.Add(reward.Building)) continue;
                    var building = catalog.GetBuilding(reward.Building);
                    buildings.Add((RewardArt.BuildingIcon(building), building.DisplayName, "Новая постройка"));
                }
            }
            var creatures = new List<(Sprite, string, string)>();
            foreach (var unit in catalog.Units)
                if (unit != null && !progress.IsUnitUnlocked(unit.Kind))
                    creatures.Add((RewardArt.Tight(unit.PortraitSprite), unit.DisplayName, "Новое существо"));
            Fill(_buildings, buildings);
            Fill(_creatures, creatures);
        }

        // Discs with the pictures, their names in the hint card; a row that has nothing to show goes with its caption.
        private void Fill(VisualElement row, IReadOnlyList<(Sprite Art, string Name, string Kind)> items)
        {
            row.Clear();
            int shown = items.Count > RowLimit ? RowLimit - 1 : items.Count;
            for (int i = 0; i < shown; i++)
            {
                var (art, name, kind) = items[i];
                var disc = Ui.Box("end-teaser");
                disc.pickingMode = PickingMode.Position;
                if (art != null)
                {
                    var image = new Image { sprite = art, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    image.AddToClassList("end-teaser__art");
                    disc.Add(image);
                }
                _tooltip?.Attach(disc, () => name, () => kind);
                row.Add(disc);
            }
            if (items.Count > shown) row.Add(Ui.Text($"+{items.Count - shown}", "chip end-more"));
            Ui.Show(row.parent, items.Count > 0);
        }

        private static int Pictures(VisualElement row)
        {
            int count = 0;
            foreach (var child in row.Children())
                if (child.ClassListContains("end-teaser")) count++;
            return count;
        }
    }
}
