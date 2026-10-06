using System;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Support;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The notice that opens a new version of the game: what this build is (the itch.io alpha or the Steam demo,
    /// from <see cref="BuildInfo.Edition"/>), the line about bug reports, "Играть", and the way to Steam: the
    /// alpha points to the demo there, the demo to the wishlist; both stay hidden while
    /// <see cref="GameLinks.SteamPage"/> is empty. Beside it, where to find the author and other players. Shown
    /// once per version; closing it lets the first-launch camera flight start. Until the player has answered
    /// whether anonymous statistics may be sent, the notice asks it and «Играть» waits for the answer; such a
    /// notice opens even in a version that has shown it.
    /// </summary>
    public sealed class IntroPanel
    {
        public const string AlphaBody =
            "Привет! Это альфа-версия игры: колония троллей и гоблинов на парящем острове. " +
            "Она ещё растёт — что-то не доделано, что-то не сбалансировано, а прогресс пока не сохраняется.";
        public const string DemoBody =
            "Привет! Это демо-версия игры: колония троллей и гоблинов на парящем острове. " +
            "Полная игра ещё в работе: что-то будет меняться, а прогресс пока не сохраняется.";
        public const string ConsentText =
            "Можно игре отправлять анонимную статистику: до какого задания доходят игроки? " +
            "Так мы находим, где играть трудно. Ответ можно поменять по F8.";

        private readonly VisualElement _overlay;
        private readonly VisualElement _rotate;
        private readonly Label _title;
        private readonly Label _body;
        private readonly Label _note;
        private readonly Button _steam;
        private readonly Label _steamCaption;
        private readonly VisualElement _steamGlyph;
        private readonly Action _closed;
        private readonly VisualElement _consent;
        private readonly Button _consentYes;
        private readonly Button _consentNo;
        private Telemetry _stats;

        public IntroPanel(VisualElement root, BuildEdition edition, Action closed)
        {
            _closed = closed;
            _overlay = Ui.Require<VisualElement>(root, "intro-overlay");
            _rotate = Ui.Require<VisualElement>(root, "rotate-overlay");
            _title = Ui.Require<Label>(root, "intro-title");
            _body = Ui.Require<Label>(root, "intro-body");
            _note = Ui.Require<Label>(root, "intro-note");
            Ui.Show(_rotate, false);
            Ui.SetText(Ui.Require<Label>(root, "intro-version"), BuildInfo.Current.VersionLabel);
            Ui.Require<VisualElement>(root, "intro-help").Add(GameLinks.HelpLine("intro-dialog__help"));
            GameLinks.AddRows(Ui.Require<VisualElement>(root, "intro-links"));

            var actions = Ui.Require<VisualElement>(root, "intro-actions");
            var play = Ui.CaptionButton("Играть", "Enter", "btn btn--primary intro-play");
            UiFeel.Bind(play, Close, Sfx.UiClick);
            actions.Add(play);
            PlayButton = play;
            _steam = Ui.CaptionButton(string.Empty, null, "btn intro-steam");
            _steamGlyph = GameLinks.Glyph("external", "intro-steam__glyph");
            _steam.Insert(0, _steamGlyph);
            _steamCaption = _steam.Q<Label>(className: "btn__caption");
            UiFeel.Bind(_steam, GameLinks.OpenSteamPage);
            actions.Add(_steam);

            // the statistics question, above the way in
            _consent = Ui.Box("intro-consent panel-soft");
            _consent.Add(Ui.Text("Анонимная статистика", "t-caption"));
            _consent.Add(Ui.Text(ConsentText, "intro-consent__text"));
            var choices = Ui.Box("intro-consent__choices");
            _consentYes = Ui.CaptionButton("Отправлять", null, "btn intro-consent__choice");
            _consentNo = Ui.CaptionButton("Не отправлять", null, "btn intro-consent__choice");
            var more = Ui.CaptionButton("Подробнее", null, "btn-link intro-consent__more");
            UiFeel.Bind(_consentYes, () => Answer(true));
            UiFeel.Bind(_consentNo, () => Answer(false));
            UiFeel.Bind(more, OpenPrivacy);
            choices.Add(_consentYes);
            choices.Add(_consentNo);
            choices.Add(more);
            _consent.Add(choices);
            actions.parent.Insert(actions.parent.IndexOf(actions), _consent);
            Ui.Show(_consent, false);

            SetEdition(edition);
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => Ui.IsShown(_overlay);
        public bool AsksToRotate => Ui.IsShown(_rotate);
        public BuildEdition Edition { get; private set; }
        public string Title => _title.text;
        public Button PlayButton { get; }
        /// <summary>"Демо в Steam" in the alpha, "В желаемое" in the demo; hidden without a Steam page.</summary>
        public Button SteamButton => _steam;
        public bool OffersSteam => Ui.IsShown(_steam);
        /// <summary>The statistics question is on the notice and still waits for an answer.</summary>
        public bool AsksConsent => Ui.IsShown(_consent) && _stats != null && !_stats.Answered;
        public bool ShowsConsent => Ui.IsShown(_consent);
        public Button ConsentYesButton => _consentYes;
        public Button ConsentNoButton => _consentNo;

        /// <summary>The words of one edition; the constructor takes the build's, tools show the other.</summary>
        public void SetEdition(BuildEdition edition)
        {
            Edition = edition;
            bool demo = edition == BuildEdition.SteamDemo;
            Ui.SetText(_title, demo ? "Демо-версия" : "Альфа-версия");
            Ui.SetText(_body, demo ? DemoBody : AlphaBody);
            Ui.SetText(_steamCaption, demo ? "В желаемое" : "Демо в Steam");
            _steamGlyph.EnableInClassList("glyph--heart", demo);
            _steamGlyph.EnableInClassList("glyph--external", !demo);
            Ui.SetText(_note, demo
                ? "Добавьте игру в желаемое, чтобы не пропустить выход."
                : "В Steam можно добавить игру в желаемое, чтобы не пропустить выход.");
            Ui.Show(_steam, GameLinks.HasSteamPage);
            Ui.Show(_note, GameLinks.HasSteamPage);
        }

        /// <summary>A screen taller than wide shows the request to turn the device.</summary>
        public void SetPortrait(bool portrait) => Ui.Show(_rotate, portrait);

        /// <summary>
        /// Puts the statistics question on the notice while <paramref name="stats"/> can collect and the player has
        /// not answered it; without a service (the editor) there is nothing to ask.
        /// </summary>
        public void AskConsent(Telemetry stats)
        {
            _stats = stats;
            Ui.Show(_consent, stats != null && stats.Available && !stats.Answered);
            ShowConsent();
        }

        /// <summary>
        /// Opens the notice unless this version has shown it and no question waits for an answer; returns whether
        /// it is open.
        /// </summary>
        public bool OpenOnce(Telemetry stats = null)
        {
            AskConsent(stats);
            if (GameSettings.IntroSeen(BuildInfo.Current.VersionLabel) && !AsksConsent) return false;
            Ui.Show(_overlay, true);
            return true;
        }

        /// <summary>Shows the notice whether or not this version showed it (screenshots).</summary>
        public void Open() => Ui.Show(_overlay, true);

        public void Close()
        {
            if (!IsOpen || AsksConsent) return;
            Ui.Show(_overlay, false);
            GameSettings.MarkIntroSeen(BuildInfo.Current.VersionLabel);
            _closed?.Invoke();
        }

        private void Answer(bool collect)
        {
            _stats?.Answer(collect);
            ShowConsent();
        }

        // the chosen answer stays lit; «Играть» waits for one
        private void ShowConsent()
        {
            bool answered = _stats != null && _stats.Answered;
            _consentYes.EnableInClassList("is-on", answered && _stats.Collecting);
            _consentNo.EnableInClassList("is-on", answered && !_stats.Collecting);
            UiFeel.SetAvailable(PlayButton, !AsksConsent);
        }

        private void OpenPrivacy()
        {
            if (!string.IsNullOrEmpty(_stats?.PrivacyUrl)) UnityEngine.Application.OpenURL(_stats.PrivacyUrl);
        }
    }
}
