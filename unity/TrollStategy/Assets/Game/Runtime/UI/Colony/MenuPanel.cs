using System;
using System.Collections.Generic;
using TrollStrategy.Presentation;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Support;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    public enum MenuPage
    {
        Pause,
        Settings,
        Languages,
        About,
        Restart,
        Saves
    }

    /// <summary>
    /// The pause menu (Esc with nothing to cancel, or the menu tool). Its first page has no card: the title on the
    /// halo over the island and round actions (continue, the saves, settings, about the game, a bug report, start over) with their
    /// words in the hint. The other pages are a white sheet with a back arrow: settings (music, sounds, nature,
    /// graphics with an automatic choice, interface size, the language on a page of its own), about the game and
    /// its author, and the question before starting over. The colony stands still while it is open. Settings
    /// live in <see cref="GameSettings"/>; starting over goes to the bootstrap. The saves are a wider sheet of slots
    /// (<see cref="SaveSheet"/>) whose questions take its place and its title.
    /// </summary>
    public sealed class MenuPanel
    {
        private static readonly (int Level, string Name)[] GraphicsChoices =
        {
            (GameSettings.AutoQuality, "Авто"), (1, "Низкая"), (3, "Средняя"), (5, "Высокая")
        };

        // past "Авто" the sizes show as a growing letter; their names are in the hint
        private static readonly (float Scale, string Name)[] SizeChoices =
        {
            (0f, "Авто"), (1f, "Обычный"), (1.2f, "Крупный"), (1.45f, "Очень крупный")
        };

        private static readonly Dictionary<MenuPage, string> Titles = new()
        {
            { MenuPage.Pause, "Пауза" },
            { MenuPage.Settings, "Настройки" },
            { MenuPage.Languages, "Язык" },
            { MenuPage.About, "Об игре" },
            { MenuPage.Restart, "Начать заново?" },
            { MenuPage.Saves, "Сохранения" }
        };

        public const string AboutGame =
            "Колония троллей и гоблинов на парящем острове: добывай, перерабатывай, вози, торгуй и выводи отряд на арену.";
        public const string AboutAlpha =
            "Игру делает один человек — Владимир Милютин. Это альфа: что-то не готово, что-то не сбалансировано.";
        public const string AboutDemo =
            "Игру делает один человек — Владимир Милютин. Это демо-версия: полная игра ещё в работе.";
        public const string TranslatedByAi =
            "Тексты на других языках переведены ИИ — если увидите ошибку, напишите автору.";
        public const string RestartWarning =
            "Колония начнётся с самого начала: здания, существа, золото и задания пропадут.";

        private readonly ColonyHudContext _context;
        private readonly HudTooltip _tooltip;
        private readonly VisualElement _overlay;
        private readonly VisualElement _pause;
        private readonly VisualElement _dialog;
        private readonly Label _title;
        private readonly Dictionary<MenuPage, VisualElement> _pages = new();
        private readonly List<Button> _graphics = new();
        private readonly List<Button> _sizes = new();
        // the tutorial pointer on or off (specs/006-tutorial-guidance, FR-014)
        private readonly List<(bool On, Button Button)> _hints = new();
        private readonly List<(string Code, Button Button)> _languages = new();
        private SettingSlider _music;
        private SettingSlider _sound;
        private SettingSlider _ambience;
        private Label _graphicsNote;
        private Label _aboutText;

        public MenuPanel(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)
        {
            _context = context;
            _tooltip = tooltip;
            _overlay = Ui.Require<VisualElement>(root, "menu-overlay");
            _pause = Ui.Require<VisualElement>(root, "menu-pause");
            _dialog = Ui.Require<VisualElement>(root, "menu-dialog");
            _title = Ui.Require<Label>(root, "menu-title");
            _pages[MenuPage.Settings] = Ui.Require<VisualElement>(root, "menu-settings");
            _pages[MenuPage.Languages] = Ui.Require<VisualElement>(root, "menu-languages");
            _pages[MenuPage.About] = Ui.Require<VisualElement>(root, "menu-about");
            _pages[MenuPage.Restart] = Ui.Require<VisualElement>(root, "menu-restart");
            _pages[MenuPage.Saves] = Ui.Require<VisualElement>(root, "menu-saves");
            UiFeel.Bind(Ui.Require<Button>(root, "menu-close"), Close, Sfx.UiBack);
            UiFeel.Bind(Ui.Require<Button>(root, "menu-back"), Back, Sfx.UiBack);

            var actions = Ui.Require<VisualElement>(root, "menu-actions");
            ContinueButton = AddAction(actions, "play", "Продолжить", "Esc", "is-primary", Close, null);
            if (context.Saves != null)
            {
                SavesButton = AddAction(actions, "save", "Сохранения", null, null, () => Show(MenuPage.Saves),
                    "Сохранить колонию в ячейку или открыть прежнюю.");
                // a load that passed its check closes the menu: the scene opens the save
                SaveSlots = new SaveSheet(_pages[MenuPage.Saves], context.Saves, context.Catalog, true, tooltip, Close);
                SaveSlots.Changed += ShowSavesTitle;
            }
            SettingsButton = AddAction(actions, "settings", "Настройки", null, null, () => Show(MenuPage.Settings),
                "Звук, графика, размер интерфейса и язык.");
            AboutButton = AddAction(actions, "info", "Об игре", null, null, () => Show(MenuPage.About),
                "Кто делает игру и где его найти.");
            if (context.ReportBug != null)
                ReportButton = AddAction(actions, "mail", "Сообщить об ошибке", null, null, () =>
                {
                    Close();
                    _context.ReportBug();
                }, "Отправить автору снимок экрана и журнал игры.");
            RestartButton = AddAction(actions, "restart", "Начать заново", null, "is-danger", () => Show(MenuPage.Restart),
                RestartWarning);

            BuildSettings(_pages[MenuPage.Settings]);
            BuildLanguages(_pages[MenuPage.Languages]);
            BuildAbout(_pages[MenuPage.About]);
            BuildRestart(_pages[MenuPage.Restart]);
            Ui.Show(_overlay, false);
        }

        public bool IsOpen => Ui.IsShown(_overlay);
        public MenuPage Page { get; private set; } = MenuPage.Pause;
        public Button ContinueButton { get; }
        public Button SettingsButton { get; }
        public Button AboutButton { get; }
        /// <summary>"Сообщить об ошибке"; null while the game has no way to send a report.</summary>
        public Button ReportButton { get; }
        public Button RestartButton { get; }
        /// <summary>«Сохранения»; null while the game has no saves (tests).</summary>
        public Button SavesButton { get; }
        /// <summary>The saves page's slots and questions; null without saves.</summary>
        public SaveSheet SaveSlots { get; }
        /// <summary>The «Начать заново?» page's warning, as shown.</summary>
        public string RestartText => _restartText?.text;
        public const string RestartOverAutosave =
            "Колония начнётся с самого начала, а нынешняя пропадёт: новая займёт место автосохранения после п" +
            "ервого приказа. Чтобы оставить нынешнюю, сначала сохраните её в ячейку.";
        private Label _restartText;
        public Button LanguageButton { get; private set; }
        public Button RestartConfirmButton { get; private set; }
        public Slider MusicSlider => _music.Slider;
        /// <summary>The settings' "Подсказки обучения": show, then hide.</summary>
        public IReadOnlyList<Button> HintButtons => _hints.ConvertAll(hint => hint.Button);
        /// <summary>The music bar's filled share, 0..1, as drawn.</summary>
        public float MusicFill => _music.Fill.style.width.value.value / 100f;
        public string MusicValue => _music.Value.text;
        public string AboutText => _aboutText.text;
        public IReadOnlyList<(string Code, Button Button)> LanguageButtons => _languages;

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            Show(MenuPage.Pause);
            Ui.Show(_overlay, true);
            UiMotion.PopIn(_overlay, .18f);
            _context.SetPaused?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            Ui.Show(_overlay, false);
            SaveSlots?.Release();
            _context.SetPaused?.Invoke(false);
        }

        /// <summary>Esc and the back arrow: one page up (the language back to the settings), closed from the first.</summary>
        public void Back()
        {
            switch (Page)
            {
                case MenuPage.Pause:
                    Close();
                    break;
                case MenuPage.Languages:
                    Show(MenuPage.Settings);
                    break;
                // a question on the saves' sheet goes back to its list first
                case MenuPage.Saves when SaveSlots != null && SaveSlots.Back():
                    break;
                default:
                    Show(MenuPage.Pause);
                    break;
            }
        }

        /// <summary>Shows a page of the open menu; tools and tests use it to reach a page directly.</summary>
        public void Show(MenuPage page)
        {
            if (page == MenuPage.Saves && SaveSlots == null) page = MenuPage.Pause;
            // the saves' pictures go with their page
            if (Page == MenuPage.Saves && page != MenuPage.Saves) SaveSlots?.Release();
            Page = page;
            bool sheet = page != MenuPage.Pause;
            Ui.Show(_pause, !sheet);
            Ui.Show(_dialog, sheet);
            foreach (var (key, element) in _pages) Ui.Show(element, key == page);
            _dialog.EnableInClassList("saves-dialog", page == MenuPage.Saves);
            if (page == MenuPage.Saves) SaveSlots.Open();
            if (page == MenuPage.Restart) Ui.SetText(_restartText, RestartWarningNow());
            Ui.SetText(_title, page == MenuPage.Saves ? SaveSlots.Title : Titles[page]);
            if (page == MenuPage.Settings || page == MenuPage.Languages) RefreshSettings();
            if (page == MenuPage.About)
                Ui.SetText(_aboutText, AboutGame + "\n" + (_context.Edition == BuildEdition.SteamDemo ? AboutDemo : AboutAlpha));
            if (IsOpen) UiMotion.PopIn(sheet ? _dialog : _pause, .16f);
        }

        // While only the autosave keeps this colony, starting over loses it once the new colony gives its first
        // order: the page says so and how to keep it, as the launch window's «Новая колония» does.
        private string RestartWarningNow()
        {
            var saves = _context.Saves;
            if (saves == null) return RestartWarning;
            foreach (var slot in saves.List())
                if (slot.Loadable && !slot.IsAutosave) return RestartWarning;
            return RestartOverAutosave;
        }

        /// <summary>The saves changed while their page may be up (an autosave): the list follows.</summary>
        public void SavesChanged()
        {
            if (IsOpen && Page == MenuPage.Saves) SaveSlots?.Refresh();
        }

        // a question opened or closed on the saves' sheet: its title is the page's
        private void ShowSavesTitle()
        {
            if (Page != MenuPage.Saves || SaveSlots == null) return;
            Ui.SetText(_title, SaveSlots.Title);
            if (IsOpen) UiMotion.PopIn(_dialog, .16f);
        }

        private Button AddAction(VisualElement parent, string glyph, string name, string key, string modifier,
            Action action, string hint)
        {
            var item = Ui.Box("pause-action");
            item.pickingMode = PickingMode.Ignore;
            var button = Ui.TextButton(string.Empty, "btn btn-disc pause-action__disc" + (modifier != null ? " " + modifier : string.Empty));
            button.Add(GameLinks.Glyph(glyph));
            if (key != null)
            {
                var badge = Ui.Text(key, "disc-key t-black");
                badge.pickingMode = PickingMode.Ignore;
                button.Add(badge);
            }
            UiFeel.Bind(button, action);
            var caption = Ui.Text(name, "pause-action__caption halo t-bold");
            caption.pickingMode = PickingMode.Ignore;
            item.Add(button);
            item.Add(caption);
            parent.Add(item);
            _tooltip?.Attach(button, () => name, hint != null ? () => hint : null, key);
            return button;
        }

        private void BuildSettings(VisualElement page)
        {
            page.Add(Ui.Text("Звук", "menu-caption t-caption"));
            _music = AddSlider(page, "music", "Музыка", GameSettings.Music, GameSettings.SetMusic);
            _sound = AddSlider(page, "sound", "Звуки", GameSettings.Sound, GameSettings.SetSound);
            _ambience = AddSlider(page, "nature", "Природа", GameSettings.Ambience, GameSettings.SetAmbience);
            page.Add(Ui.Box("divider"));

            page.Add(Ui.Text("Экран", "menu-caption t-caption"));
            var graphics = Ui.Box("segmented setting-row__control");
            foreach (var (level, name) in GraphicsChoices)
            {
                var choice = Ui.CaptionButton(name, null, "btn");
                int picked = level;
                UiFeel.Bind(choice, () =>
                {
                    GameSettings.SetQuality(picked);
                    RefreshSettings();
                });
                _graphics.Add(choice);
                graphics.Add(choice);
            }
            AddRow(page, "graphics", "Графика").Add(graphics);
            _graphicsNote = Ui.Text(string.Empty, "setting-note t-muted");
            page.Add(_graphicsNote);

            var sizes = Ui.Box("segmented setting-row__control");
            for (int i = 0; i < SizeChoices.Length; i++)
            {
                var (scale, name) = SizeChoices[i];
                var choice = i == 0
                    ? Ui.CaptionButton(name, null, "btn")
                    : Ui.CaptionButton("A", null, $"btn segmented__letter segmented__letter--{i}");
                if (i > 0) _tooltip?.Attach(choice, () => name, null);
                float picked = scale;
                UiFeel.Bind(choice, () =>
                {
                    GameSettings.SetUiScale(picked);
                    RefreshSettings();
                });
                _sizes.Add(choice);
                sizes.Add(choice);
            }
            AddRow(page, "text-size", "Интерфейс").Add(sizes);

            // the tutorial's hand, veil and hint cards; the quests and their short ways stay the same either way
            var hints = Ui.Box("segmented setting-row__control");
            foreach (var (on, name) in new[] { (true, "Показывать"), (false, "Скрыть") })
            {
                var choice = Ui.CaptionButton(name, null, "btn");
                bool picked = on;
                UiFeel.Bind(choice, () =>
                {
                    GameSettings.SetTutorialHints(picked);
                    RefreshSettings();
                });
                _hints.Add((on, choice));
                hints.Add(choice);
            }
            AddRow(page, "book", "Подсказки обучения").Add(hints);
            page.Add(Ui.Box("divider"));

            LanguageButton = Ui.CaptionButton(string.Empty, null, "btn btn-picker setting-row__control");
            LanguageButton.Add(GameLinks.Glyph("chevron", "btn-picker__chevron"));
            UiFeel.Bind(LanguageButton, () => Show(MenuPage.Languages));
            AddRow(page, "language", "Язык").Add(LanguageButton);
        }

        // every language under its own name; the choice applies at once and goes back to the settings
        private void BuildLanguages(VisualElement page)
        {
            var grid = Ui.Box("language-grid");
            foreach (var (code, name) in Languages)
            {
                var choice = Ui.CaptionButton(name, null, "btn language-choice" + (code == "hi" ? " lang-complex" : string.Empty));
                string picked = code;
                UiFeel.Bind(choice, () =>
                {
                    _context.SetLanguage?.Invoke(picked);
                    Show(MenuPage.Settings);
                });
                _languages.Add((code, choice));
                grid.Add(choice);
            }
            page.Add(grid);
        }

        private void BuildAbout(VisualElement page)
        {
            _aboutText = Ui.Text(string.Empty, "menu-text");
            page.Add(_aboutText);
            page.Add(GameLinks.HelpLine("menu-help"));
            page.Add(Ui.Text(TranslatedByAi, "menu-note t-muted"));
            page.Add(Ui.Text(GameLinks.Invite, "menu-caption t-caption"));
            GameLinks.AddDiscs(page);
            var info = BuildInfo.Current;
            page.Add(Ui.Text($"Версия {info.VersionNumber}, {info.CommitLabel}", "menu-version t-faint"));
        }

        private void BuildRestart(VisualElement page)
        {
            var warning = Ui.Box("callout callout--danger");
            warning.Add(GameLinks.Glyph("restart", "callout__glyph"));
            _restartText = Ui.Text(RestartWarning, "callout__text");
            warning.Add(_restartText);
            page.Add(warning);
            var buttons = Ui.Box("menu-buttons");
            var cancel = Ui.CaptionButton("Отмена", null, "btn menu-buttons__button");
            UiFeel.Bind(cancel, () => Show(MenuPage.Pause), Sfx.UiBack);
            RestartConfirmButton = Ui.CaptionButton("Начать заново", null, "btn btn--danger menu-buttons__button");
            UiFeel.Bind(RestartConfirmButton, () =>
            {
                Close();
                _context.Restart?.Invoke();
            });
            buttons.Add(cancel);
            buttons.Add(RestartConfirmButton);
            page.Add(buttons);
        }

        private List<(string Code, string Name)> Languages =>
            _context.Languages ?? new List<(string Code, string Name)> { ("ru", "Русский") };

        private void RefreshSettings()
        {
            _music.Set(GameSettings.Music);
            _sound.Set(GameSettings.Sound);
            _ambience.Set(GameSettings.Ambience);
            for (int i = 0; i < _graphics.Count; i++)
                _graphics[i].EnableInClassList("is-on", GraphicsChoices[i].Level == GameSettings.Quality);
            for (int i = 0; i < _sizes.Count; i++)
                _sizes[i].EnableInClassList("is-on", Mathf.Approximately(SizeChoices[i].Scale, GameSettings.UiScale));
            foreach (var (on, button) in _hints) button.EnableInClassList("is-on", on == GameSettings.TutorialHints);
            var names = QualitySettings.names;
            int effective = GameSettings.EffectiveQuality;
            Ui.SetText(_graphicsNote, GameSettings.Quality == GameSettings.AutoQuality && effective < names.Length
                ? $"Подобрано для этого устройства: {QualityName(effective)}."
                : string.Empty);
            Ui.Show(_graphicsNote, !string.IsNullOrEmpty(_graphicsNote.text));

            string current = _context.CurrentLanguage?.Invoke();
            var languages = Languages;
            int index = Math.Max(0, languages.FindIndex(l => l.Code == current));
            Ui.SetCaption(LanguageButton, languages[index].Name);
            LanguageButton.EnableInClassList("lang-complex", languages[index].Code == "hi");
            foreach (var (code, button) in _languages) button.EnableInClassList("is-on", code == languages[index].Code);
        }

        private static string QualityName(int level) =>
            level <= 1 ? "низкая" : level <= 3 ? "средняя" : "высокая";

        private static VisualElement AddRow(VisualElement page, string glyph, string label)
        {
            var row = Ui.Box("setting-row");
            row.Add(GameLinks.Glyph(glyph, "setting-row__glyph glyph--muted"));
            row.Add(Ui.Text(label, "setting-row__label t-bold"));
            page.Add(row);
            return row;
        }

        private static SettingSlider AddSlider(VisualElement page, string glyph, string label, float value, Action<float> set)
        {
            var row = AddRow(page, glyph, label);
            var slider = new SettingSlider(value, set);
            row.Add(slider.Slider);
            row.Add(slider.Value);
            return slider;
        }

        /// <summary>
        /// A volume: Unity's slider with a filled bar drawn into its track (the game has no Unity theme, so
        /// Theme.uss styles the parts) and its value as a number after it.
        /// </summary>
        private sealed class SettingSlider
        {
            public readonly Slider Slider;
            public readonly VisualElement Fill;
            public readonly Label Value;

            public SettingSlider(float value, Action<float> set)
            {
                Slider = new Slider(0f, 1f) { value = value };
                Slider.AddToClassList("slider");
                Slider.AddToClassList("setting-row__control");
                Fill = Ui.Box("slider__fill");
                Fill.pickingMode = PickingMode.Ignore;
                Slider.Q(className: "unity-base-slider__tracker")?.Add(Fill);
                Value = Ui.Text(string.Empty, "setting-row__value t-bold t-muted");
                Slider.RegisterValueChangedCallback(evt =>
                {
                    set(evt.newValue);
                    Draw(evt.newValue);
                });
                Draw(value);
            }

            public void Set(float value)
            {
                Slider.SetValueWithoutNotify(value);
                Draw(value);
            }

            private void Draw(float value)
            {
                float share = Mathf.Clamp01(value);
                Fill.style.width = Length.Percent(share * 100f);
                Ui.SetText(Value, Mathf.RoundToInt(share * 100f).ToString());
            }
        }
    }
}
