using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Support;
using UnityEngine;
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
    /// notice opens even in a version that has shown it. The first launch also asks whether the tutorial's hints
    /// (the hand, the veil and the step cards) should show; «Играть» waits for that answer too.
    ///
    /// With saved colonies the notice is the launch window: the latest save's card (its picture of the island, level,
    /// numbers, when and how long) over «Продолжить» (Enter) that opens it, «Загрузить» that lists every slot on the
    /// same veil (<see cref="SaveSheet"/>, load and delete), and «Новая колония» in place of «Играть». With only saves
    /// this build cannot open, «Играть» and the list. It opens on the run's first colony whenever saves exist, not
    /// after a load or a new start; Esc never starts a new colony over saves, and the questions hold every way in.
    /// While the autosave is the only save that opens, «Новая колония» asks first on the same veil: the new colony
    /// takes the autosave's place after its first order, so the window shows what would be lost and how to keep it.
    /// </summary>
    public sealed class IntroPanel
    {
        public const string AlphaBody =
            "Привет! Это альфа-версия игры: колония троллей и гоблинов на парящем острове. " +
            "Она ещё растёт — что-то не доделано, что-то не сбалансировано. " +
            "Колония сохраняется сама, а в меню её можно сохранить в ячейку.";
        public const string DemoBody =
            "Привет! Это демо-версия игры: колония троллей и гоблинов на парящем острове. " +
            "Полная игра ещё в работе: что-то будет меняться. " +
            "Колония сохраняется сама, а в меню её можно сохранить в ячейку.";
        public const string ConsentText =
            "Можно игре отправлять анонимную статистику: до какого задания доходят игроки? " +
            "Так мы находим, где играть трудно. Ответ можно поменять по F8.";
        public const string NewColonyWarning =
            "Новая колония займёт место автосохранения после первого приказа, и эта колония пропадёт. Чтобы е" +
            "ё оставить, нажмите «Продолжить» и сохраните её в ячейку через меню.";
        public const string HintsText =
            "Показывать в обучении, куда нажимать: рукой, подсветкой и карточкой с шагом? Это можно поменять в меню.";

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
        private readonly VisualElement _hints;
        private readonly Button _hintsOn;
        private readonly Button _hintsOff;
        private Telemetry _stats;
        private bool _asksHints;
        private bool? _hintsChosen;
        // the launch window: the saves it offers, the latest one's card and the list of every slot
        private readonly SaveGames _saves;
        private readonly GameContentCatalog _catalog;
        private readonly HudTooltip _tooltip;
        private readonly VisualElement _dialog;
        private readonly VisualElement _actions;
        private readonly Label _playKey;
        private readonly VisualElement _card;
        private readonly SavePictures _cardPictures = new();
        private readonly VisualElement _savesSheet;
        private readonly Label _savesTitle;
        private bool _offers;
        private bool _hintsAttached;
        private SaveSlotInfo _latest;
        private SaveProblem _latestProblem;
        private Label _cardTitle;
        // «Новая колония» over a colony only the autosave keeps: the question on the list's sheet
        private VisualElement _listPage;
        private VisualElement _freshPage;
        private VisualElement _freshCard;
        private Label _freshText;
        private readonly SavePictures _freshPictures = new();
        private bool _asksFresh;
        private bool _freshRisky;

        public IntroPanel(VisualElement root, BuildEdition edition, Action closed)
            : this(root, edition, closed, null, null, null)
        {
        }

        /// <param name="saves">
        /// The saved colonies the window offers at launch; null: the notice as before (a load or a new start in the
        /// same run, or no saves to manage).
        /// </param>
        public IntroPanel(VisualElement root, BuildEdition edition, Action closed, SaveGames saves,
            GameContentCatalog catalog, HudTooltip tooltip)
        {
            _closed = closed;
            _saves = saves;
            _catalog = catalog;
            _tooltip = tooltip;
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
            // with saves: «Продолжить» the latest, «Загрузить» from the list, and «Играть» becomes «Новая колония»
            _actions = actions;
            _playKey = play.Q<Label>(className: "hotkey");
            ContinueButton = Ui.CaptionButton("Продолжить", "Enter", "btn btn--primary intro-play");
            UiFeel.Bind(ContinueButton, Continue);
            actions.Insert(0, ContinueButton);
            LoadButton = Ui.CaptionButton("Загрузить", null, "btn intro-steam");
            UiFeel.Bind(LoadButton, OpenList);
            actions.Insert(1, LoadButton);
            Ui.Show(ContinueButton, false);
            Ui.Show(LoadButton, false);

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

            // the hints question, above the statistics one; the same words as the menu's setting
            _hints = Ui.Box("intro-consent panel-soft");
            _hints.Add(Ui.Text("Подсказки обучения", "t-caption"));
            _hints.Add(Ui.Text(HintsText, "intro-consent__text"));
            var hintChoices = Ui.Box("intro-consent__choices");
            _hintsOn = Ui.CaptionButton("Показывать", null, "btn intro-consent__choice");
            _hintsOff = Ui.CaptionButton("Скрыть", null, "btn intro-consent__choice");
            UiFeel.Bind(_hintsOn, () => ChooseHints(true));
            UiFeel.Bind(_hintsOff, () => ChooseHints(false));
            hintChoices.Add(_hintsOn);
            hintChoices.Add(_hintsOff);
            _hints.Add(hintChoices);
            actions.parent.Insert(actions.parent.IndexOf(_consent), _hints);
            Ui.Show(_hints, false);

            // the latest save, right above the way into it
            _card = Ui.Box("save-last panel-soft");
            actions.parent.Insert(actions.parent.IndexOf(actions), _card);
            Ui.Show(_card, false);
            // the list of every slot, on the same veil, in place of the window
            _dialog = Ui.Require<VisualElement>(root, "intro-dialog");
            _savesSheet = Ui.Require<VisualElement>(root, "intro-saves");
            _savesTitle = Ui.Require<Label>(root, "intro-saves-title");
            UiFeel.Bind(Ui.Require<Button>(root, "intro-saves-back"), Back, Sfx.UiBack);
            UiFeel.Bind(Ui.Require<Button>(root, "intro-saves-close"), CloseSheet, Sfx.UiBack);
            if (saves != null)
            {
                List = new SaveSheet(Ui.Require<VisualElement>(root, "intro-saves-page"), saves, catalog, false, tooltip,
                    Opened);
                List.Changed += ShowListTitle;
            }
            Ui.Show(_savesSheet, false);

            // the question before a new colony takes the only save's place: the card that would be lost and the way out
            _listPage = Ui.Require<VisualElement>(root, "intro-saves-page");
            _freshPage = Ui.Box("menu-page saves-page");
            _freshCard = Ui.Box("save-last panel-soft save-last--question");
            _freshPage.Add(_freshCard);
            var risk = Ui.Box("callout callout--danger save-callout");
            risk.Add(GameLinks.Glyph("restart", "callout__glyph"));
            _freshText = Ui.Text(NewColonyWarning, "callout__text");
            risk.Add(_freshText);
            _freshPage.Add(risk);
            var freshButtons = Ui.Box("menu-buttons");
            NewColonyCancelButton = Ui.CaptionButton("Отмена", null, "btn menu-buttons__button");
            NewColonyConfirmButton = Ui.CaptionButton("Новая колония", null, "btn btn--danger menu-buttons__button");
            UiFeel.Bind(NewColonyCancelButton, CloseFresh, Sfx.UiBack);
            UiFeel.Bind(NewColonyConfirmButton, Close);
            freshButtons.Add(NewColonyCancelButton);
            freshButtons.Add(NewColonyConfirmButton);
            _freshPage.Add(freshButtons);
            _savesSheet.Add(_freshPage);
            Ui.Show(_freshPage, false);
            // «Новая колония» (and «Играть») asks first while the autosave is the only save that opens
            UiFeel.Bind(play, PlayOrAsk, Sfx.UiClick);

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
        /// <summary>The hints question is on the notice and still waits for an answer.</summary>
        public bool AsksHints => _asksHints && _hintsChosen == null;
        public bool ShowsHints => Ui.IsShown(_hints);
        public Button HintsOnButton => _hintsOn;
        public Button HintsOffButton => _hintsOff;
        /// <summary>«Продолжить»: opens the latest save; shown while the window offers one.</summary>
        public Button ContinueButton { get; }
        /// <summary>«Загрузить»: every slot, on a sheet over the window.</summary>
        public Button LoadButton { get; }
        /// <summary>The slots at launch (load and delete); null without saves to offer.</summary>
        public SaveSheet List { get; }
        /// <summary>The window offers saved colonies: it is the launch window.</summary>
        public bool OffersSaves => _offers;
        /// <summary>The save «Продолжить» opens, as its card shows it; null when no save can be opened.</summary>
        public SaveSlotInfo Latest => _latest;
        /// <summary>The card's title («Уровень 14: Новая земля»); null without a card.</summary>
        public string CardTitle => _offers && _cardTitle != null ? _cardTitle.text : null;
        /// <summary>The list of slots is up in place of the window.</summary>
        public bool ShowsList => Ui.IsShown(_savesSheet) && !_asksFresh;
        /// <summary>The question before a new colony takes the only save's place is up.</summary>
        public bool AsksNewColony => _asksFresh && Ui.IsShown(_savesSheet);
        /// <summary>«Новая колония» would ask first: the autosave is the only save this build opens.</summary>
        public bool NewColonyAsks => _freshRisky;
        /// <summary>The question's warning, as shown.</summary>
        public string NewColonyQuestion => _freshText?.text;
        /// <summary>The question's «Отмена»: back to the window, nothing changes.</summary>
        public Button NewColonyCancelButton { get; private set; }
        /// <summary>The question's danger button: the new colony starts, as «Новая колония» without a question.</summary>
        public Button NewColonyConfirmButton { get; private set; }
        /// <summary>The card's picture of the island, while it shows one.</summary>
        public IReadOnlyList<Texture2D> CardPictures => _cardPictures.Made;

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
            // the launch window's three ways in leave no room for Steam
            Ui.Show(_steam, GameLinks.HasSteamPage && !_offers);
            Ui.Show(_note, GameLinks.HasSteamPage && !_offers);
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
            ShowAnswers();
        }

        /// <summary>Puts the hints question on the notice (the first launch) or takes it off.</summary>
        public void AskHints(bool ask)
        {
            _asksHints = ask;
            _hintsChosen = null;
            Ui.Show(_hints, ask);
            ShowAnswers();
        }

        /// <summary>
        /// Opens the notice unless this version has shown it and no question waits for an answer; returns whether
        /// it is open.
        /// </summary>
        public bool OpenOnce(Telemetry stats = null)
        {
            AskConsent(stats);
            AskHints(!GameSettings.TutorialHintsChosen);
            // saved colonies open the launch window whether or not this version has shown its notice
            bool offers = OfferSaves();
            if (!offers && GameSettings.IntroSeen(BuildInfo.Current.VersionLabel) && !AsksConsent && !AsksHints) return false;
            Ui.Show(_overlay, true);
            return true;
        }

        /// <summary>Shows the notice whether or not this version showed it (screenshots).</summary>
        public void Open() => Ui.Show(_overlay, true);

        public void Close()
        {
            if (!IsOpen || AsksConsent || AsksHints) return;
            Ui.Show(_overlay, false);
            GameSettings.MarkIntroSeen(BuildInfo.Current.VersionLabel);
            ReleasePictures();
            _closed?.Invoke();
        }

        /// <summary>
        /// Enter: «Продолжить» when the window offers a save to go on with, else «Играть»; nothing while the list is up.
        /// </summary>
        public void Confirm()
        {
            // Enter answers no question on the sheet, the new colony's least of all
            if (!IsOpen || Ui.IsShown(_savesSheet)) return;
            if (Ui.IsShown(ContinueButton)) UiFeel.Press(ContinueButton);
            else Close();
        }

        /// <summary>
        /// Esc and the back arrow: a question goes back to the list, the list back to the window. The window itself
        /// stays while it offers saves (Esc never starts a new colony over them); otherwise Esc closes it as «Играть».
        /// </summary>
        public void Back()
        {
            if (!IsOpen) return;
            if (AsksNewColony)
            {
                CloseFresh();
                return;
            }
            if (ShowsList)
            {
                if (List == null || !List.Back()) CloseList();
                return;
            }
            if (!_offers) Close();
        }

        /// <summary>
        /// Sets the window by the saves now: the latest one's card and the three ways in, a line about saves this
        /// build cannot open, or the notice as before; returns whether it offers saves. OpenOnce calls it.
        /// </summary>
        public bool OfferSaves()
        {
            var slots = _saves?.List();
            _offers = slots != null && slots.Count > 0;
            _latest = _offers ? _saves.Latest() : null;
            // the autosave is the only save that opens: a new colony would take its place, so «Новая колония» asks
            _freshRisky = _latest != null;
            if (slots != null)
                foreach (var slot in slots)
                    if (slot.Loadable && !slot.IsAutosave) _freshRisky = false;
            _latestProblem = SaveProblem.None;
            ShowCard();
            bool goOn = _latest != null;
            Ui.Show(_card, _offers);
            Ui.Show(ContinueButton, goOn);
            Ui.Show(LoadButton, _offers);
            Ui.SetCaption(PlayButton, goOn ? "Новая колония" : "Играть");
            PlayButton.EnableInClassList("btn--primary", !goOn);
            Ui.Show(_playKey, !goOn);
            _actions.EnableInClassList("intro-dialog__actions--saves", _offers);
            _dialog.EnableInClassList("intro-dialog--saves", _offers);
            Ui.Show(_steam, GameLinks.HasSteamPage && !_offers);
            Ui.Show(_note, GameLinks.HasSteamPage && !_offers);
            if (_offers && !_hintsAttached && _tooltip != null)
            {
                _hintsAttached = true;
                _tooltip.Attach(ContinueButton, () => "Продолжить", () => "Открыть последнее сохранение и играть дальше.", "Enter");
                _tooltip.Attach(LoadButton, () => "Загрузить", () => "Выбрать колонию из автосохранения или ячейки.");
                _tooltip.Attach(PlayButton, () => _latest != null ? "Новая колония" : "Играть",
                    () => "Начать остров с начала. Ячейки останутся, а автосохранение займёт новая колония.",
                    _latest != null ? null : "Enter");
            }
            ShowAnswers();
            return _offers;
        }

        /// <summary>The window is put away for good: the card's picture and the list's go.</summary>
        public void ReleasePictures()
        {
            _cardPictures.Release();
            List?.Release();
            _freshPictures.Release();
            _asksFresh = false;
            Ui.Show(_freshPage, false);
            Ui.Show(_listPage, true);
            Ui.Show(_savesSheet, false);
            Ui.Show(_dialog, true);
        }

        // the latest save: its picture, its level and title, its numbers, when and how long
        private void ShowCard()
        {
            _cardPictures.Release();
            _card.Clear();
            _cardTitle = null;
            if (!_offers) return;
            if (_latest == null)
            {
                var shut = Ui.Box("callout save-last__callout");
                shut.Add(GameLinks.Glyph("lock", "callout__glyph"));
                shut.Add(Ui.Text("Сохранённые колонии этой версией не открыть. Почему, видно в списке.", "callout__text"));
                _card.Add(shut);
                return;
            }
            _card.Add(LatestRow(_cardPictures, true));
        }

        // the latest save's row: its picture, its slot, its level and title, its numbers, when and how long
        private VisualElement LatestRow(SavePictures pictures, bool isCard)
        {
            var summary = _latest.Header?.Summary;
            bool locked = _latestProblem != SaveProblem.None;
            var row = Ui.Box("save-last__row");
            var thumb = SaveParts.Thumb(locked ? null : pictures.From(_latest.Thumbnail), "save-last__thumb");
            if (locked) thumb.Add(SaveParts.Lock());
            row.Add(thumb);
            var text = Ui.Box("save-last__text");
            var head = Ui.Box("save-last__head");
            head.Add(Ui.Text(SaveParts.SlotName(_latest.SlotId), "t-caption"));
            if (!locked && _latest.CarriedFrom != null)
            {
                string from = _latest.CarriedFrom;
                var chip = Ui.Text(SaveParts.CarriedWord(from), "chip save-row__carried");
                _tooltip?.Attach(chip, () => "Из другой версии", () => SaveParts.CarriedHint(from));
                head.Add(chip);
            }
            text.Add(head);
            var title = Ui.Text(SaveParts.Title(summary), "save-last__title t-black");
            if (isCard) _cardTitle = title;
            text.Add(title);
            if (locked)
                text.Add(Ui.Text(SaveParts.ProblemNote(_latest, _latestProblem, _saves.Stamp.Build), "save-last__note t-warn t-bold"));
            else text.Add(SaveParts.Facts(summary, _catalog, _tooltip, "save-facts--small save-last__facts"));
            text.Add(SaveParts.WhenLine(SaveParts.Played(_latest, DateTime.Now), "save-last__when"));
            row.Add(text);
            return row;
        }

        // «Продолжить»: the latest save opens in place of this colony; one the full check turns down locks its card
        private void Continue()
        {
            if (_saves == null || _latest == null) return;
            var result = _saves.Load(_latest.SlotId);
            if (result.Ok)
            {
                Opened();
                return;
            }
            _latestProblem = result.Problem == SaveProblem.None ? SaveProblem.Damaged : result.Problem;
            ShowCard();
            ShowAnswers();
            GameAudio.Play(Sfx.UiDenied);
            UiMotion.Nudge(_card, 8f);
        }

        // a save passed its check and opens in place of this colony: the window goes, and no new colony flies in
        private void Opened()
        {
            Ui.Show(_overlay, false);
            GameSettings.MarkIntroSeen(BuildInfo.Current.VersionLabel);
            ReleasePictures();
        }

        private void OpenList()
        {
            if (List == null) return;
            Ui.Show(_dialog, false);
            Ui.Show(_savesSheet, true);
            List.Open();
            ShowListTitle();
            UiMotion.PopIn(_savesSheet, .16f);
        }

        // back to the window, which follows what the list changed (a deleted slot may have been the card's)
        private void CloseList()
        {
            if (!ShowsList) return;
            List?.Release();
            Ui.Show(_savesSheet, false);
            Ui.Show(_dialog, true);
            OfferSaves();
            UiMotion.PopIn(_dialog, .16f);
        }

        // «Новая колония» (or «Играть»): straight in, unless the autosave is the only save that opens; then the
        // question first, since the new colony takes the autosave's place after its first order
        private void PlayOrAsk()
        {
            if (_offers && _freshRisky && _latest != null) AskFresh();
            else Close();
        }

        private void AskFresh()
        {
            if (!IsOpen || AsksConsent || AsksHints) return;
            _asksFresh = true;
            _freshPictures.Release();
            _freshCard.Clear();
            _freshCard.Add(LatestRow(_freshPictures, false));
            Ui.Show(_dialog, false);
            Ui.Show(_listPage, false);
            Ui.Show(_freshPage, true);
            Ui.Show(_savesSheet, true);
            Ui.SetText(_savesTitle, "Начать новую колонию?");
            UiMotion.PopIn(_savesSheet, .16f);
        }

        // back to the window: nothing was written, the autosave stays as it was
        private void CloseFresh()
        {
            if (!_asksFresh) return;
            _asksFresh = false;
            _freshPictures.Release();
            _freshCard.Clear();
            Ui.Show(_freshPage, false);
            Ui.Show(_listPage, true);
            Ui.Show(_savesSheet, false);
            Ui.Show(_dialog, true);
            UiMotion.PopIn(_dialog, .16f);
        }

        // the sheet's ×: the question or the list goes, the window comes back
        private void CloseSheet()
        {
            if (_asksFresh) CloseFresh();
            else CloseList();
        }

        private void ShowListTitle()
        {
            if (List != null) Ui.SetText(_savesTitle, List.Title);
        }

        private void Answer(bool collect)
        {
            _stats?.Answer(collect);
            ShowAnswers();
        }

        private void ChooseHints(bool on)
        {
            _hintsChosen = on;
            GameSettings.SetTutorialHints(on);
            ShowAnswers();
        }

        // the chosen answers stay lit; «Играть» waits for both
        private void ShowAnswers()
        {
            bool answered = _stats != null && _stats.Answered;
            _consentYes.EnableInClassList("is-on", answered && _stats.Collecting);
            _consentNo.EnableInClassList("is-on", answered && !_stats.Collecting);
            _hintsOn.EnableInClassList("is-on", _hintsChosen == true);
            _hintsOff.EnableInClassList("is-on", _hintsChosen == false);
            UiFeel.SetAvailable(PlayButton, !AsksConsent && !AsksHints);
            // the saves wait for the answers too; a latest save the full check turned down stays shut
            UiFeel.SetAvailable(ContinueButton, !AsksConsent && !AsksHints && _latest != null &&
                                                _latestProblem == SaveProblem.None);
            UiFeel.SetAvailable(LoadButton, !AsksConsent && !AsksHints);
        }

        private void OpenPrivacy()
        {
            if (!string.IsNullOrEmpty(_stats?.PrivacyUrl)) UnityEngine.Application.OpenURL(_stats.PrivacyUrl);
        }
    }
}
