using System;
using System.Collections.Generic;
using System.Linq;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.Bots.SaveUi
{
    /// <summary>
    /// The save-game mockups, built at run time over the real HUD documents from Theme.uss and ColonyHud.uss classes
    /// plus SaveMock.uss (the few new ones). Each shot is a list of steps a moment apart (a hint card needs its
    /// target laid out first). Kit.Pin marks the parts the canvas labels.
    /// </summary>
    public static class SaveUiMocks
    {
        public sealed class Shot
        {
            public string Name;
            public Action<Kit>[] Steps;
            public float Wait = 1.4f;
            public bool WorldOnly;
        }

        private const string Current = "v1.0.58";
        private const string Newer = "v1.0.61";

        public static readonly Shot[] All =
        {
            // the game as it is today
            new Shot { Name = "island", WorldOnly = true, Steps = new Action<Kit>[] { k => { } } },
            new Shot { Name = "now-hud", Steps = new Action<Kit>[] { Hud } },
            new Shot { Name = "now-intro", Steps = new Action<Kit>[] { OpenIntro } },
            new Shot { Name = "now-menu", Steps = new Action<Kit>[] { k => k.View.Menu.Open() } },
            new Shot { Name = "now-restart", Steps = new Action<Kit>[] { k => { k.View.Menu.Open(); k.View.Menu.Show(MenuPage.Restart); } } },

            // 1. Само: one autosave, nothing to manage
            new Shot { Name = "v1-launch", Steps = new Action<Kit>[] { V1Launch } },
            new Shot { Name = "v1-saved", Steps = new Action<Kit>[] { V1Saved } },
            new Shot { Name = "v1-pause", Steps = new Action<Kit>[] { V1Pause, V1PauseHint } },
            new Shot { Name = "v1-new", Steps = new Action<Kit>[] { V1New } },
            new Shot { Name = "v1-broken", Steps = new Action<Kit>[] { V1Broken } },

            // 2. Ячейки: the autosave and four slots in the menu
            new Shot { Name = "v2-launch", Steps = new Action<Kit>[] { V2Launch } },
            new Shot { Name = "v2-pause", Steps = new Action<Kit>[] { V2Pause, V2PauseHint } },
            new Shot { Name = "v2-list", Steps = new Action<Kit>[] { k => V2List(k, false) } },
            new Shot { Name = "v2-saved", Steps = new Action<Kit>[] { k => V2List(k, true) } },
            new Shot { Name = "v2-overwrite", Steps = new Action<Kit>[] { V2Overwrite } },
            new Shot { Name = "v2-load", Steps = new Action<Kit>[] { V2Load, V2LoadHint } },

            // 3. Летопись: a chapter for every quest, the colony can go back to any of them
            new Shot { Name = "v3-launch", Steps = new Action<Kit>[] { V3Launch } },
            new Shot { Name = "v3-reward", Wait = 2.6f, Steps = new Action<Kit>[] { V3Reward } },
            new Shot { Name = "v3-hud", Steps = new Action<Kit>[] { V3Hud, V3HudHint } },
            new Shot { Name = "v3-chronicle", Steps = new Action<Kit>[] { V3Chronicle } },
            new Shot { Name = "v3-return", Steps = new Action<Kit>[] { V3Return } },
            new Shot { Name = "v3-broken", Steps = new Action<Kit>[] { V3Broken, V3BrokenHint } },
        };

        // ================================================================ the game today

        private static void Hud(Kit k) => k.View.Refresh(k.Session.CurrentSnapshot);

        private static void OpenIntro(Kit k)
        {
            var intro = k.View.Intro;
            intro.AskConsent(null);
            intro.AskHints(false);
            intro.Open();
            k.Text(k.Q<Label>("intro-version"), Current);
        }

        // ================================================================ 1. Само

        private const string V1Body =
            "Привет! Это альфа-версия игры: колония троллей и гоблинов на парящем острове. " +
            "Она ещё растёт — что-то не доделано, что-то не сбалансировано. Колония сохраняется сама.";

        private static void V1Launch(Kit k)
        {
            OpenIntro(k);
            k.Text(k.Q<Label>("intro-body"), V1Body);
            var card = ColonyCard(k, k.Now, "Сохранено сегодня в 14:32, в игре " + k.Now.PlayTime, false);
            InsertBeforeActions(k, card);
            var (go, fresh) = LaunchButtons(k, "Продолжить", "Новая колония", null);
            k.Pin(k.Q("intro-dialog"), "Окно запуска: .sheet на .overlay--deep (Intro.uxml)");
            k.Pin(card, "Карточка колонии: .panel-soft, картинка + число");
            k.Pin(go, "«Продолжить»: .btn--primary с клавишей Enter");
            k.Pin(fresh, "«Новая колония»: .btn, ведёт к подтверждению");
        }

        private static void V1Saved(Kit k)
        {
            Hud(k);
            var tick = StatusTick(k, "check", "Колония сохранена");
            k.Pin(tick, "Тихая галочка: StatusLine (.float.status) + .glyph--check");
        }

        private static void V1Pause(Kit k)
        {
            k.View.Menu.Open();
            var pause = k.Q("menu-pause");
            var note = pause.Q<Label>(className: "menu-pause__note");
            var line = Ui.Box("save-pause-note");
            line.Add(GameLinks.Glyph("check", "save-pause-note__glyph"));
            line.Add(Ui.Text("Сохранено в 14:32", "save-pause-note__text t-bold halo"));
            k.Add(pause, line, pause.IndexOf(note) + 1);
            k.Pin(line, "Когда сохранено: строка на ореоле (.halo)");
        }

        private static void V1PauseHint(Kit k)
        {
            var restart = k.View.Menu.RestartButton;
            k.Tooltip(restart, "Начать заново",
                "Новая колония займёт место сохранённой: здания, существа, золото и задания пропадут.");
            k.Pin(k.TooltipCard, "HudTooltip: слова о сохранении в подсказке");
        }

        private static void V1New(Kit k)
        {
            OpenIntro(k);
            k.Hide(k.Q("intro-dialog"));
            var sheet = Sheet(k, k.Q("intro-overlay"), "Начать новую колонию?", "menu-dialog");
            var card = ColonyCard(k, k.Now, "Сохранено сегодня в 14:32, в игре " + k.Now.PlayTime, false);
            sheet.Add(card);
            sheet.Add(Callout("restart", "Эта колония пропадёт: здания, существа, золото и задания. Остров начнётся с начала.", true));
            var (cancel, ok) = TwoButtons(sheet, "Отмена", "Начать заново", "btn--danger");
            k.Pin(sheet, "Подтверждение: .sheet со стрелкой назад, как «Начать заново?» в меню");
            k.Pin(card, "Что пропадёт: та же карточка колонии");
            k.Pin(ok, ".callout--danger + .btn--danger");
        }

        private static void V1Broken(Kit k)
        {
            OpenIntro(k);
            k.Text(k.Q<Label>("intro-body"), V1Body);
            var card = ColonyCard(k, k.Now, null, true);
            card.Add(Callout("lock",
                $"Эту колонию сохранила версия {Newer}, она новее вашей ({Current}). Обновите игру, чтобы продолжить.",
                false, "colony-card__callout"));
            InsertBeforeActions(k, card);
            var (go, fresh) = LaunchButtons(k, "Продолжить", "Новая колония", null);
            UiFeel.SetAvailable(go, false);
            var note = Ui.Text("Новая колония не сотрёт старую: та останется в папке игры до обновления.", "intro-dialog__note t-muted");
            k.Add(go.parent.parent, note, go.parent.parent.IndexOf(go.parent));
            k.Pin(card, "Не открыть: карточка бледнеет, .callout с замком");
            k.Pin(go, "«Продолжить» недоступно: UiFeel.SetAvailable(false)");
            k.Pin(note, "Обещание логике: нечитаемый файл не перезаписывать");
        }

        // ================================================================ 2. Ячейки

        private const string V2Body =
            "Привет! Это альфа-версия игры: колония троллей и гоблинов на парящем острове. " +
            "Она ещё растёт — что-то не доделано, что-то не сбалансировано. Игра сама сохраняет колонию после каждого задания, а в меню её можно сохранить в ячейку.";

        private static void V2Launch(Kit k)
        {
            OpenIntro(k);
            k.Text(k.Q<Label>("intro-body"), V2Body);
            var last = Ui.Box("save-last");
            last.Add(Thumb(k.Now, "save-last__thumb"));
            var text = Ui.Box("save-last__text");
            text.Add(Ui.Text("Автосохранение, сегодня в 14:32", "t-caption"));
            text.Add(Ui.Text(k.Now.Title, "save-last__title t-black"));
            text.Add(Ui.Text(k.Now.Chapter, "save-last__chapter t-muted"));
            last.Add(text);
            InsertBeforeActions(k, last);
            var (go, fresh) = LaunchButtons(k, "Продолжить", "Новая колония", "Загрузить");
            k.Pin(last, "Последнее сохранение: снимок острова + уровень");
            k.Pin(go, "«Продолжить» (Enter) открывает последнее сохранение");
            k.Pin(go.parent.Q<Button>("launch-load"), "«Загрузить»: список ячеек на том же листе");
        }

        private static void V2Pause(Kit k)
        {
            k.View.Menu.Open();
            var actions = k.Q("menu-actions");
            var item = PauseAction("save", "Сохранения", null, null);
            k.Add(actions, item, 1);
            k.Pin(item, "Пятый .btn-disc в паузе (MenuPanel.AddAction)");
        }

        private static void V2PauseHint(Kit k)
        {
            var disc = k.Q("menu-actions").Q<Button>(className: "save-action");
            k.Tooltip(disc, "Сохранения", "Сохранить колонию в ячейку или открыть прежнюю.");
            k.Pin(k.TooltipCard, "HudTooltip: что делает кнопка");
        }

        private static void V2List(Kit k, bool justSaved)
        {
            var dialog = MenuSheet(k, "Сохранения");
            var page = k.Add(dialog, Ui.Box("menu-page saves-page"));
            var list = Ui.Box("save-list");
            page.Add(list);
            var auto = SlotRow(k, "Автосохранение", k.Now, "Сегодня, 14:32", false);
            list.Add(auto);
            list.Add(SlotRow(k, "Ячейка 1", k.Early, "Вчера, 21:10", false));
            var two = SlotRow(k, "Ячейка 2", k.Mid, "Сегодня, 13:05", !justSaved);
            list.Add(two);
            var broken = BrokenRow(k, "Ячейка 3", k.Mid, false);
            list.Add(broken);
            VisualElement four = justSaved ? SlotRow(k, "Ячейка 4", k.Now, null, true, true) : EmptyRow("Ячейка 4");
            list.Add(four);
            Button main;
            if (justSaved)
            {
                var (delete, save, load) = Footer(page, "Удалить", "Перезаписать", "Загрузить");
                main = save;
            }
            else
            {
                var (delete, save, load) = Footer(page, "Удалить", "Перезаписать", "Загрузить");
                main = load;
            }
            k.Pin(dialog, "Лист меню (.sheet .menu-dialog) шире: 980 px");
            k.Pin(auto, "Автосохранение: только «Загрузить»");
            if (justSaved)
            {
                k.Pin(four.Q(className: "save-row__saved"), "Сохранено: строка горит акцентом, .glyph--check");
                k.Pin(main, "Кнопки внизу листа для выбранной ячейки");
            }
            else
            {
                k.Pin(two, "Выбранная ячейка: .is-on (акцент), снимок, уровень, картинка + число");
                k.Pin(broken, "Не открыть: замок на снимке, версия словами");
                k.Pin(four, "Пустая ячейка");
                k.Pin(main, ".menu-buttons: Удалить / Перезаписать / Загрузить");
            }
        }

        private static void V2Overwrite(Kit k)
        {
            var dialog = MenuSheet(k, "Перезаписать ячейку 2?");
            var page = k.Add(dialog, Ui.Box("menu-page saves-page"));
            var compare = Ui.Box("save-compare");
            compare.Add(CompareCard(k, "Сейчас в ячейке", k.Mid, "Сегодня, 13:05"));
            var arrow = Ui.Box("save-compare__arrow");
            arrow.Add(GameLinks.Glyph("chevron", "save-compare__glyph"));
            compare.Add(arrow);
            compare.Add(CompareCard(k, "Будет", k.Now, "Сейчас"));
            page.Add(compare);
            page.Add(Callout("save", "Колония из ячейки 2 пропадёт, на её месте будет нынешняя.", true));
            var (cancel, ok) = TwoButtons(page, "Отмена", "Перезаписать", "btn--danger");
            k.Pin(compare, "Было → станет: два снимка с уровнем и числами");
            k.Pin(ok, ".callout--danger + .btn--danger, как «Начать заново?»");
        }

        private static void V2Load(Kit k)
        {
            OpenIntro(k);
            k.Hide(k.Q("intro-dialog"));
            var sheet = Sheet(k, k.Q("intro-overlay"), "Загрузить колонию", "menu-dialog saves-dialog");
            var list = Ui.Box("save-list");
            sheet.Add(list);
            list.Add(SlotRow(k, "Автосохранение", k.Now, "Сегодня, 14:32", true));
            list.Add(SlotRow(k, "Ячейка 1", k.Early, "Вчера, 21:10", false));
            list.Add(SlotRow(k, "Ячейка 2", k.Mid, "Сегодня, 13:05", false));
            var broken = BrokenRow(k, "Ячейка 3", k.Mid, false);
            broken.name = "slot-broken";
            list.Add(broken);
            var damaged = BrokenRow(k, "Ячейка 4", null, true);
            list.Add(damaged);
            var (delete, load) = TwoButtons(sheet, "Удалить", "Загрузить", "btn--primary");
            delete.AddToClassList("btn--danger");
            k.Pin(sheet, "Тот же список из окна запуска, на полной вуали");
            k.Pin(damaged, "Файл повреждён: без снимка и чисел, только удалить");
        }

        private static void V2LoadHint(Kit k)
        {
            var badge = k.Q("slot-broken").Q(className: "save-row__lock");
            k.Tooltip(badge, "Не открыть в этой версии",
                $"Ячейку сохранила версия {Newer}, а у вас {Current}. Обновите игру, и колония откроется.");
            k.Pin(k.TooltipCard, "HudTooltip над замком объясняет, что делать");
        }

        // ================================================================ 3. Летопись

        private static void V3Launch(Kit k)
        {
            OpenIntro(k);
            k.Hide(k.Q("intro-dialog"));
            var column = k.Add(k.Q("intro-overlay"), Ui.Box("chronicle"));
            column.Add(Ui.Text(k.Now.Chapter, "chronicle__eyebrow t-bold t-muted halo"));
            var title = Ui.Text(k.Now.Title, "chronicle__title t-black halo");
            column.Add(title);
            column.Add(Ui.Text("Сохранено сегодня в 14:32, в игре " + k.Now.PlayTime, "chronicle__note t-bold t-muted halo"));
            var rail = Rail(k, k.Now.Level - 1, -1, -1, false);
            column.Add(rail);
            var actions = Ui.Box("menu-pause__actions chronicle__actions");
            var go = PauseAction("play", "Продолжить", "Enter", "is-primary");
            var fresh = PauseAction("restart", "Новая колония", null, "is-danger");
            actions.Add(go);
            actions.Add(fresh);
            column.Add(actions);
            k.Pin(title, "Без листа: заголовок на ореоле, как пауза");
            k.Pin(rail, "Летопись: жетоны глав (.token) с картинкой награды, страницы как в каталоге");
            k.Pin(go, "Круглые действия паузы (.btn-disc)");
        }

        private static void V3Reward(Kit k)
        {
            var quest = k.Session.CurrentSnapshot.Progress.Quest;
            if (quest == null) return;
            k.View.Reward.Open(quest);
            var dialog = k.Q("reward-dialog");
            var line = Ui.Box("chronicle-line");
            line.Add(GameLinks.Glyph("scroll", "chronicle-line__glyph"));
            line.Add(Ui.Text($"Глава {k.Now.Level} ляжет в летопись", "chronicle-line__text t-bold"));
            k.Add(dialog, line);
            k.Pin(line, "Сохранение = награда: строка под «Забрать»");
        }

        private static void V3Hud(Kit k)
        {
            Hud(k);
            var header = k.Q("quest").Q(className: "quest__header");
            var link = Ui.CaptionButton("Летопись", null, "btn btn-link quest__toggle chronicle-link");
            link.Insert(0, GameLinks.Glyph("scroll"));
            k.Add(header, link);
            var tick = StatusTick(k, "scroll", $"Глава {k.Now.Level - 1} записана в летопись");
            k.Pin(link, ".btn-link в шапке задания");
            k.Pin(tick, "StatusLine: глава записана");
        }

        private static void V3HudHint(Kit k)
        {
            var link = k.Q("quest").Q(className: "chronicle-link");
            k.Tooltip(link, "Летопись", "Колония после каждого задания. Можно вернуться к любой главе и пройти её заново.");
            k.Pin(k.TooltipCard, "HudTooltip: что такое летопись");
        }

        private static void V3Chronicle(Kit k)
        {
            k.View.Menu.Open();
            k.Hide(k.Q("menu-pause"));
            var column = k.Add(k.Q("menu-overlay"), Ui.Box("chronicle chronicle--game"));
            column.Add(Ui.Text("Колония сохраняется после каждого задания", "chronicle__eyebrow t-bold t-muted halo"));
            column.Add(Ui.Text("Летопись", "chronicle__title t-black halo"));
            int picked = k.Mid.Level - 1;
            var rail = Rail(k, k.Now.Level - 1, picked, -1, true);
            column.Add(rail);
            var card = ChapterCard(k, k.Mid, picked, "Записана вчера в 21:10, в игре " + k.Mid.PlayTime, "Вернуться сюда");
            column.Add(card);
            k.Pin(rail, "Жетон выбранной главы: .is-suggested");
            k.Pin(card, "Подробности на дымке (.float): картинка + число");
            k.Pin(card.Q<Button>(className: "btn--primary"), "«Вернуться сюда» → подтверждение");
        }

        private static void V3Return(Kit k)
        {
            OpenIntro(k);
            k.Hide(k.Q("intro-dialog"));
            int chapter = k.Mid.Level - 1;
            var sheet = Sheet(k, k.Q("intro-overlay"), $"Вернуться к главе {chapter}?", "menu-dialog");
            var row = Ui.Box("chapter-row");
            row.Add(ChapterDisc(k, chapter, false, false));
            var text = Ui.Box("chapter-row__text");
            text.Add(Ui.Text(QuestTitle(k, chapter), "chapter-row__title t-black"));
            text.Add(Facts(k, k.Mid, "save-facts--small"));
            text.Add(Ui.Text("Записана вчера в 21:10", "chapter-row__when t-muted"));
            row.Add(text);
            sheet.Add(row);
            int last = k.Now.Level - 1;
            sheet.Add(Callout("restart",
                $"Колония станет такой, какой была после главы {chapter}. Главы {chapter + 1}–{last} останутся в летописи, а сделанное после главы {last} пропадёт.",
                true));
            var (cancel, ok) = TwoButtons(sheet, "Отмена", "Вернуться", "btn--danger");
            k.Pin(row, "Глава: жетон с картинкой награды и числа");
            k.Pin(ok, "Лист-вопрос на полной вуали, .callout--danger");
        }

        private static void V3Broken(Kit k)
        {
            OpenIntro(k);
            k.Hide(k.Q("intro-dialog"));
            int last = k.Now.Level - 1;
            var column = k.Add(k.Q("intro-overlay"), Ui.Box("chronicle"));
            column.Add(Ui.Text("Последнее сохранение повреждено", "chronicle__eyebrow t-bold t-bad halo"));
            column.Add(Ui.Text(QuestTitle(k, last), "chronicle__title t-black halo"));
            column.Add(Ui.Text($"Можно продолжить с главы {last}: она записана сегодня в 14:05.", "chronicle__note t-bold t-muted halo"));
            var rail = Rail(k, last, last, last - 6, false, true);
            column.Add(rail);
            var actions = Ui.Box("menu-pause__actions chronicle__actions");
            var go = PauseAction("play", $"С главы {last}", "Enter", "is-primary");
            actions.Add(go);
            actions.Add(PauseAction("restart", "Новая колония", null, "is-danger"));
            column.Add(actions);
            k.Pin(rail.Q(className: "chapter--now"), "«Сейчас» не открыть: замок, жетон бледный");
            k.Pin(go, "Продолжить с последней целой главы");
        }

        private static void V3BrokenHint(Kit k)
        {
            var broken = k.Q("intro-overlay").Q(className: "chapter--broken");
            var disc = broken?.Q(className: "token__disc");
            if (disc == null) return;
            k.Tooltip(disc, $"Главу {k.Now.Level - 7} не открыть", "Файл главы повреждён. Остальные главы целы.");
            k.Pin(disc, "Повреждённая глава: замок на жетоне");
            k.Pin(k.TooltipCard, "HudTooltip: остальные главы целы");
        }

        // ================================================================ parts

        private static void InsertBeforeActions(Kit k, VisualElement element)
        {
            var actions = k.Q("intro-actions");
            var main = actions.parent;
            k.Add(main, element, main.IndexOf(actions));
        }

        // the notice's way in: the real «Играть» and Steam buttons give way to the save's
        private static (Button Go, Button Fresh) LaunchButtons(Kit k, string go, string fresh, string load)
        {
            var actions = k.Q("intro-actions");
            foreach (var child in actions.Children().ToList()) k.Hide(child);
            var goButton = Ui.CaptionButton(go, "Enter", "btn btn--primary intro-play" + (load != null ? " save-launch-tight" : ""));
            k.Add(actions, goButton);
            if (load != null)
            {
                var loadButton = Ui.CaptionButton(load, null, "btn intro-steam launch-button");
                loadButton.name = "launch-load";
                k.Add(actions, loadButton);
            }
            var freshButton = Ui.CaptionButton(fresh, null, "btn intro-steam launch-button");
            k.Add(actions, freshButton);
            return (goButton, freshButton);
        }

        private static VisualElement ColonyCard(Kit k, ColonyFacts f, string when, bool faded)
        {
            var card = Ui.Box("colony-card panel-soft" + (faded ? " is-faded" : ""));
            card.Add(Ui.Text(f.Chapter, "t-caption"));
            card.Add(Ui.Text(f.Title, "colony-card__title t-black"));
            card.Add(Facts(k, f, "colony-card__facts"));
            if (when != null)
            {
                var line = Ui.Box("save-when colony-card__when");
                line.Add(GameLinks.Glyph("rest", "save-when__glyph"));
                line.Add(Ui.Text(when, "save-when__text"));
                card.Add(line);
            }
            return card;
        }

        // a picture and a number each; their names are in the hints
        private static VisualElement Facts(Kit k, ColonyFacts f, string classes)
        {
            var row = Ui.Box("save-facts " + classes);
            row.Add(Fact(RewardArt.Coin(k.Catalog), null, f.Gold.ToString(), "save-fact--gold"));
            row.Add(Fact(RewardArt.BuildingIcon(k.Catalog, BuildingKind.Barracks), null, f.Creatures.ToString(), null));
            row.Add(Fact(RewardArt.BuildingIcon(k.Catalog, BuildingKind.Warehouse), null, f.Buildings.ToString(), null));
            row.Add(Fact(null, "land", f.Land.ToString(), null));
            row.Add(Fact(null, "battle", f.Battles.ToString(), null));
            return row;
        }

        private static VisualElement Fact(Sprite sprite, string glyph, string value, string modifier)
        {
            var fact = Ui.Box("save-fact" + (modifier != null ? " " + modifier : ""));
            if (sprite != null)
            {
                var icon = Ui.Box("save-fact__icon");
                icon.style.backgroundImage = new StyleBackground(sprite);
                fact.Add(icon);
            }
            else fact.Add(GameLinks.Glyph(glyph, "save-fact__glyph"));
            fact.Add(Ui.Text(value, "save-fact__value t-black"));
            return fact;
        }

        private static VisualElement Thumb(ColonyFacts f, string classes)
        {
            var thumb = Ui.Box(classes);
            if (f?.Thumb != null) thumb.style.backgroundImage = new StyleBackground(f.Thumb);
            return thumb;
        }

        private static VisualElement StatusTick(Kit k, string glyph, string text)
        {
            var status = k.Q("status");
            k.Show(status);
            k.Class(status, "is-faded", false);
            k.Hide(k.Q("status-text"));
            var row = Ui.Box("save-tick");
            row.Add(GameLinks.Glyph(glyph, "save-tick__glyph"));
            row.Add(Ui.Text(text, "status__text t-medium"));
            k.Add(status, row);
            return row;
        }

        private static VisualElement Callout(string glyph, string text, bool danger, string classes = null)
        {
            var callout = Ui.Box("callout save-callout" + (danger ? " callout--danger" : "") + (classes != null ? " " + classes : ""));
            callout.Add(GameLinks.Glyph(glyph, "callout__glyph"));
            callout.Add(Ui.Text(text, "callout__text"));
            return callout;
        }

        private static (Button, Button) TwoButtons(VisualElement parent, string left, string right, string rightClass)
        {
            var row = Ui.Box("menu-buttons");
            var a = Ui.CaptionButton(left, null, "btn menu-buttons__button");
            var b = Ui.CaptionButton(right, null, "btn " + rightClass + " menu-buttons__button");
            row.Add(a);
            row.Add(b);
            parent.Add(row);
            return (a, b);
        }

        private static (Button, Button, Button) Footer(VisualElement parent, string delete, string save, string load)
        {
            var row = Ui.Box("menu-buttons saves-footer");
            var a = Ui.CaptionButton(delete, null, "btn btn--danger menu-buttons__button");
            a.Insert(0, GameLinks.Glyph("trash", "saves-footer__glyph"));
            var b = Ui.CaptionButton(save, null, "btn menu-buttons__button");
            b.Insert(0, GameLinks.Glyph("save", "saves-footer__glyph"));
            var c = Ui.CaptionButton(load, null, "btn btn--primary menu-buttons__button");
            row.Add(a);
            row.Add(b);
            row.Add(c);
            parent.Add(row);
            return (a, b, c);
        }

        // a sheet of its own, with the menu's header: back arrow, title, close
        private static VisualElement Sheet(Kit k, VisualElement overlay, string title, string classes)
        {
            var sheet = k.Add(overlay, Ui.Box("sheet " + classes));
            var header = Ui.Box("dialog__header");
            var back = Ui.TextButton(string.Empty, "btn btn-close btn-back");
            back.Add(GameLinks.Glyph("chevron"));
            header.Add(back);
            header.Add(Ui.Text(title, "menu-dialog__title t-black"));
            header.Add(Ui.TextButton("×", "btn btn-close"));
            sheet.Add(header);
            return sheet;
        }

        // the menu's own sheet with a page of our own instead of its four
        private static VisualElement MenuSheet(Kit k, string title)
        {
            var menu = k.View.Menu;
            menu.Open();
            menu.Show(MenuPage.Settings);
            var dialog = k.Q("menu-dialog");
            foreach (var name in new[] { "menu-settings", "menu-languages", "menu-about", "menu-restart" }) k.Hide(k.Q(name));
            k.Text(k.Q<Label>("menu-title"), title);
            k.Class(dialog, "saves-dialog", true);
            return dialog;
        }

        private static VisualElement PauseAction(string glyph, string name, string key, string modifier)
        {
            var item = Ui.Box("pause-action");
            item.pickingMode = PickingMode.Ignore;
            var button = Ui.TextButton(string.Empty, "btn btn-disc pause-action__disc" + (modifier != null ? " " + modifier : "") +
                                                     (glyph == "save" ? " save-action" : ""));
            button.Add(GameLinks.Glyph(glyph));
            if (key != null) button.Add(Ui.Text(key, "disc-key t-black"));
            item.Add(button);
            item.Add(Ui.Text(name, "pause-action__caption halo t-bold"));
            return item;
        }

        private static VisualElement SlotRow(Kit k, string name, ColonyFacts f, string when, bool picked, bool fresh = false)
        {
            var row = Ui.Box("save-row" + (picked ? " is-on" : ""));
            row.Add(Thumb(f, "save-row__thumb"));
            var main = Ui.Box("save-row__main");
            main.Add(Ui.Text(name, "save-row__name t-caption"));
            main.Add(Ui.Text($"Уровень {f.Level}: {f.Title}", "save-row__title t-black"));
            main.Add(Facts(k, f, "save-facts--small save-row__facts"));
            row.Add(main);
            var side = Ui.Box("save-row__side");
            if (fresh)
            {
                var saved = Ui.Box("save-row__saved");
                saved.Add(GameLinks.Glyph("check", "save-row__saved-glyph"));
                saved.Add(Ui.Text("Сохранено", "save-row__saved-text t-black"));
                side.Add(saved);
            }
            else side.Add(Ui.Text(when, "save-row__when t-bold"));
            var play = Ui.Box("save-when save-row__play");
            play.Add(GameLinks.Glyph("rest", "save-when__glyph"));
            play.Add(Ui.Text(f.PlayTime, "save-when__text"));
            side.Add(play);
            row.Add(side);
            return row;
        }

        private static VisualElement BrokenRow(Kit k, string name, ColonyFacts f, bool damaged)
        {
            var row = Ui.Box("save-row is-broken");
            var thumb = Thumb(damaged ? null : f, "save-row__thumb");
            var badge = Ui.Box("save-row__lock");
            badge.Add(GameLinks.Glyph("lock"));
            thumb.Add(badge);
            row.Add(thumb);
            var main = Ui.Box("save-row__main");
            main.Add(Ui.Text(name, "save-row__name t-caption"));
            if (damaged)
            {
                main.Add(Ui.Text("Файл повреждён", "save-row__title t-black"));
                main.Add(Ui.Text("Колонию из него не открыть. Ячейку можно удалить.", "save-row__note t-muted"));
            }
            else
            {
                main.Add(Ui.Text($"Уровень {f.Level + 3}: {QuestTitle(k, f.Level + 3)}", "save-row__title save-row__faded t-black"));
                main.Add(Ui.Text($"Сохранено версией {Newer}, у вас {Current}", "save-row__note t-warn t-bold"));
            }
            row.Add(main);
            var side = Ui.Box("save-row__side");
            side.Add(Ui.Text(damaged ? "Позавчера, 19:40" : "Сегодня, 10:12", "save-row__when t-bold t-faint"));
            row.Add(side);
            return row;
        }

        private static VisualElement EmptyRow(string name)
        {
            var row = Ui.Box("save-row is-empty");
            var thumb = Ui.Box("save-row__thumb save-row__thumb--empty");
            thumb.Add(GameLinks.Glyph("save", "save-row__empty-glyph"));
            row.Add(thumb);
            var main = Ui.Box("save-row__main");
            main.Add(Ui.Text(name, "save-row__name t-caption"));
            main.Add(Ui.Text("Пусто", "save-row__title t-black t-faint"));
            row.Add(main);
            return row;
        }

        private static VisualElement CompareCard(Kit k, string caption, ColonyFacts f, string when)
        {
            var card = Ui.Box("save-compare__card panel-soft");
            card.Add(Ui.Text(caption, "t-caption"));
            card.Add(Thumb(f, "save-compare__thumb"));
            card.Add(Ui.Text($"Уровень {f.Level}: {f.Title}", "save-compare__title t-black"));
            card.Add(Facts(k, f, "save-facts--small"));
            var line = Ui.Box("save-when");
            line.Add(GameLinks.Glyph("rest", "save-when__glyph"));
            line.Add(Ui.Text($"{when}, в игре {f.PlayTime}", "save-when__text"));
            card.Add(line);
            return card;
        }

        // ---------------------------------------------------------------- the chronicle

        private static readonly string[] ChapterTimes =
        {
            "сегодня, 14:05", "сегодня, 13:41", "сегодня, 13:12", "вчера, 21:10", "вчера, 20:52", "вчера, 20:31",
            "вчера, 20:04", "вчера, 19:40", "вчера, 19:18", "вчера, 19:02", "вчера, 18:49", "вчера, 18:40"
        };

        private static string ChapterTime(int lastChapter, int chapter)
        {
            int back = lastChapter - chapter;
            return back >= 0 && back < ChapterTimes.Length ? ChapterTimes[back] : "позавчера";
        }

        /// <summary>
        /// The last chapters as a page of tokens and «Сейчас» at its end. <paramref name="picked"/> is the chapter
        /// shown in the card (-1: «Сейчас»); <paramref name="brokenChapter"/> wears a lock.
        /// </summary>
        private static VisualElement Rail(Kit k, int lastChapter, int picked, int brokenChapter, bool nowIsBack,
            bool nowBroken = false)
        {
            var pager = Ui.Box("catalog-pager chronicle__rail");
            var prev = Ui.TextButton(string.Empty, "btn-disc catalog-arrow catalog-arrow--prev");
            prev.Add(GameLinks.Glyph("chevron", "catalog-arrow__glyph"));
            pager.Add(prev);
            var tray = Ui.Box("chronicle__tray");
            int first = Math.Max(1, lastChapter - 6);
            for (int chapter = first; chapter <= lastChapter; chapter++)
                tray.Add(ChapterToken(k, chapter, ChapterTime(lastChapter, chapter), chapter == picked,
                    chapter == brokenChapter));
            tray.Add(NowToken(picked < 0 && !nowBroken, nowIsBack, nowBroken));
            pager.Add(tray);
            var next = Ui.TextButton(string.Empty, "btn-disc catalog-arrow catalog-arrow--next is-unavailable");
            next.Add(GameLinks.Glyph("chevron", "catalog-arrow__glyph"));
            pager.Add(next);
            var dots = Ui.Box("catalog-dots");
            int pages = (lastChapter + 1 + 7) / 8;
            for (int i = 0; i < pages; i++) dots.Add(Ui.Box("catalog-dot" + (i == pages - 1 ? " is-on" : "")));
            pager.Add(dots);
            return pager;
        }

        private static VisualElement ChapterToken(Kit k, int chapter, string when, bool picked, bool broken)
        {
            var root = Ui.Box("token chapter" + (broken ? " chapter--broken" : "") + (picked ? " is-picked" : ""));
            root.Add(ChapterDisc(k, chapter, picked, broken));
            root.Add(Ui.Text(QuestTitle(k, chapter), "token__name t-bold"));
            root.Add(Ui.Text(when, "chapter__when"));
            return root;
        }

        private static VisualElement ChapterDisc(Kit k, int chapter, bool picked, bool broken)
        {
            var disc = Ui.TextButton(string.Empty, "btn btn-disc token__disc" + (picked ? " is-suggested" : ""));
            var sprite = ChapterArt(k, chapter);
            if (sprite != null)
            {
                var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                image.AddToClassList("token__art");
                disc.Add(image);
            }
            disc.Add(Ui.Text(chapter.ToString(), "chapter__number t-black"));
            if (broken)
            {
                var badge = Ui.Box("arena-rail__badge arena-rail__badge--closed chapter__lock");
                badge.Add(GameLinks.Glyph("lock"));
                disc.Add(badge);
            }
            return disc;
        }

        private static VisualElement NowToken(bool picked, bool isBack, bool broken)
        {
            var root = Ui.Box("token chapter chapter--now" + (broken ? " chapter--broken" : "") + (picked ? " is-picked" : ""));
            var disc = Ui.TextButton(string.Empty, "btn btn-disc token__disc" + (picked ? " is-suggested" : ""));
            disc.Add(GameLinks.Glyph(broken ? "save" : "play", "chapter__glyph"));
            if (isBack) disc.Add(Ui.Text("Esc", "disc-key t-black chapter__key"));
            if (broken)
            {
                var badge = Ui.Box("arena-rail__badge arena-rail__badge--closed chapter__lock");
                badge.Add(GameLinks.Glyph("lock"));
                disc.Add(badge);
            }
            root.Add(disc);
            root.Add(Ui.Text("Сейчас", "token__name t-bold"));
            root.Add(Ui.Text(broken ? "не открыть" : isBack ? "вернуться в игру" : "сегодня, 14:32", "chapter__when"));
            return root;
        }

        private static VisualElement ChapterCard(Kit k, ColonyFacts f, int chapter, string when, string action)
        {
            var card = Ui.Box("float chronicle-card");
            var text = Ui.Box("chronicle-card__text");
            text.Add(Ui.Text($"Глава {chapter}", "chronicle-card__eyebrow t-bold t-muted"));
            text.Add(Ui.Text(QuestTitle(k, chapter), "chronicle-card__title t-black"));
            text.Add(Facts(k, f, "chronicle-card__facts"));
            var line = Ui.Box("save-when");
            line.Add(GameLinks.Glyph("rest", "save-when__glyph"));
            line.Add(Ui.Text(when, "save-when__text"));
            text.Add(line);
            card.Add(text);
            var button = Ui.CaptionButton(action, null, "btn btn--primary chronicle-card__button");
            button.Insert(0, GameLinks.Glyph("restart", "chronicle-card__glyph"));
            card.Add(button);
            return card;
        }

        private static QuestDefinition Quest(Kit k, int chapter)
        {
            var quests = k.Catalog.Progression.Quests;
            return chapter >= 1 && chapter <= quests.Count ? quests[chapter - 1] : null;
        }

        private static string QuestTitle(Kit k, int chapter) => Quest(k, chapter)?.Title ?? $"Глава {chapter}";

        // the chapter's picture: what its quest gave, as the reward window shows it
        private static Sprite ChapterArt(Kit k, int chapter)
        {
            var quest = Quest(k, chapter);
            if (quest == null) return RewardArt.Coin(k.Catalog);
            // what the colony built in that chapter, else what the chapter gave
            foreach (var goal in quest.Goals)
                if (goal.Kind == QuestGoalKind.OwnBuildings || goal.Kind == QuestGoalKind.UpgradeBuilding)
                    return RewardArt.BuildingIcon(k.Catalog, goal.Building);
            if (quest.Goals.Any(g => g.Kind == QuestGoalKind.WinBattles || g.Kind == QuestGoalKind.ReachArenaLevel))
                return RewardArt.BuildingIcon(k.Catalog, BuildingKind.Barracks);
            var rewards = quest.Rewards;
            var reward = rewards.FirstOrDefault(r => r.Kind == QuestRewardKind.UnlockBuilding);
            if (reward.Kind == QuestRewardKind.UnlockBuilding && rewards.Any(r => r.Kind == QuestRewardKind.UnlockBuilding))
                return RewardArt.BuildingIcon(k.Catalog, reward.Building);
            if (rewards.Any(r => r.Kind == QuestRewardKind.UnlockUnit))
            {
                var unit = rewards.First(r => r.Kind == QuestRewardKind.UnlockUnit).Unit;
                return RewardArt.Tight(k.Catalog.TryGetUnit(unit)?.PortraitSprite);
            }
            if (rewards.Any(r => r.Kind == QuestRewardKind.UnlockMission))
                return RewardArt.BuildingIcon(k.Catalog, BuildingKind.Barracks);
            return RewardArt.Coin(k.Catalog);
        }
    }
}
