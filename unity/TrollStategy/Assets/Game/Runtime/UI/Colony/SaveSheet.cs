using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The saved colonies on a sheet: the autosave, then the player's four slots, each with its picture of the
    /// island, «Уровень N: title», its facts as a picture and a number, when it was saved and how long it was
    /// played. The chosen row wears the accent; the buttons under the list (delete, save or overwrite, load) follow
    /// what it holds. A slot this build cannot open keeps its row behind a lock and says why: a newer version wrote
    /// it, a longer edition did, or the file is damaged (then only deleting is left). Overwriting, deleting and a
    /// load that would drop unsaved play ask first, on the same sheet. In the game the sheet lives in the pause
    /// menu; at launch the same list (load and delete only) opens over the launch window. Everything goes through
    /// <see cref="SaveGames"/>; the sheet keeps only what it shows.
    /// </summary>
    public sealed class SaveSheet
    {
        /// <summary>The player's slots, in the order the sheet lists them under the autosave.</summary>
        public static readonly string[] ManualSlots = { "slot-1", "slot-2", "slot-3", "slot-4" };

        public enum Question
        {
            None,
            Overwrite,
            Delete,
            Load
        }

        private sealed class Row
        {
            public string Id;
            public SaveSlotInfo Slot;
            public SaveProblem Problem;
            public Button Button;
            public Texture2D Picture;
            public Label Note;
            public bool ShowsSaved;
        }

        private readonly SaveGames _saves;
        private readonly GameContentCatalog _catalog;
        private readonly bool _inGame;
        private readonly HudTooltip _tooltip;
        private readonly Action _opened;
        private readonly Func<DateTime> _now;
        private readonly VisualElement _list;
        private readonly ScrollView _scroll;
        private readonly VisualElement _question;
        private readonly VisualElement _questionBody;
        private readonly VisualElement _callout;
        private readonly VisualElement _calloutGlyph;
        private readonly Label _calloutText;
        private readonly SavePictures _pictures = new();
        private readonly SavePictures _questionPictures = new();
        private readonly List<Row> _rows = new();
        // slots a full check turned down although their header looked fine, and saves that could not be written
        private readonly Dictionary<string, SaveProblem> _refused = new();
        private readonly Dictionary<string, string> _failed = new();
        private string _selected;
        private string _justSaved;
        private string _asked;

        /// <param name="host">The page the sheet fills: the list, its buttons and the questions.</param>
        /// <param name="inGame">The pause menu's sheet (saving, overwriting); false: the launch window's (load, delete).</param>
        /// <param name="opened">A load passed its check and the game opens it: the host steps aside.</param>
        /// <param name="localNow">The player's clock for «Сегодня» and «Вчера»; the system's when null.</param>
        public SaveSheet(VisualElement host, SaveGames saves, GameContentCatalog catalog, bool inGame, HudTooltip tooltip,
            Action opened, Func<DateTime> localNow = null)
        {
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _catalog = catalog;
            _inGame = inGame;
            _tooltip = tooltip;
            _opened = opened;
            _now = localNow ?? (() => DateTime.Now);
            host.AddToClassList("saves-page");

            _list = Ui.Box("saves-list");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("saves-scroll");
            // the theme draws no scroll bars: a list taller than the screen scrolls by the wheel and by touch
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            // the rows are rebuilt on every change; a group transform on their content drew them displaced
            _scroll.contentContainer.usageHints = UsageHints.None;
            _list.Add(_scroll);
            var footer = Ui.Box("menu-buttons saves-footer");
            DeleteButton = Ui.CaptionButton("Удалить", null, "btn btn--danger menu-buttons__button");
            DeleteButton.Insert(0, GameLinks.Glyph("trash", "saves-footer__glyph"));
            SaveButton = Ui.CaptionButton("Сохранить", null, "btn menu-buttons__button");
            SaveButton.Insert(0, GameLinks.Glyph("save", "saves-footer__glyph"));
            LoadButton = Ui.CaptionButton("Загрузить", null, "btn btn--primary menu-buttons__button");
            UiFeel.Bind(DeleteButton, () => Ask(Question.Delete));
            UiFeel.Bind(SaveButton, PressSave);
            UiFeel.Bind(LoadButton, PressLoad);
            footer.Add(DeleteButton);
            footer.Add(SaveButton);
            footer.Add(LoadButton);
            Ui.Show(SaveButton, inGame);
            _list.Add(footer);
            host.Add(_list);
            _tooltip?.Attach(DeleteButton, () => "Удалить", DeleteHint);
            _tooltip?.Attach(SaveButton, () => SaveCaption, SaveHint);
            _tooltip?.Attach(LoadButton, () => "Загрузить", LoadHint);

            _question = Ui.Box("saves-question");
            _questionBody = Ui.Box("saves-question__body");
            _question.Add(_questionBody);
            _callout = Ui.Box("callout callout--danger save-callout");
            _calloutGlyph = GameLinks.Glyph("save", "callout__glyph");
            _calloutText = Ui.Text(string.Empty, "callout__text");
            _callout.Add(_calloutGlyph);
            _callout.Add(_calloutText);
            _question.Add(_callout);
            var buttons = Ui.Box("menu-buttons");
            CancelButton = Ui.CaptionButton("Отмена", null, "btn menu-buttons__button");
            ConfirmButton = Ui.CaptionButton(string.Empty, null, "btn btn--danger menu-buttons__button");
            UiFeel.Bind(CancelButton, () => Back(), Sfx.UiBack);
            UiFeel.Bind(ConfirmButton, Confirm);
            buttons.Add(CancelButton);
            buttons.Add(ConfirmButton);
            _question.Add(buttons);
            host.Add(_question);
            Ui.Show(_question, false);
        }

        public Button DeleteButton { get; }
        /// <summary>«Сохранить» for an empty slot, «Перезаписать» for a taken one; hidden at launch.</summary>
        public Button SaveButton { get; }
        public Button LoadButton { get; }
        public Button CancelButton { get; }
        public Button ConfirmButton { get; }
        /// <summary>The question on the sheet; None while the list shows.</summary>
        public Question Asking { get; private set; }
        /// <summary>The sheet's heading: the list's, or the question's.</summary>
        public string Title => Asking switch
        {
            Question.Overwrite => SlotNumberOf(_asked) > 0 ? $"Перезаписать ячейку {SlotNumberOf(_asked)}?" : "Перезаписать сохранение?",
            Question.Delete => SlotNumberOf(_asked) > 0 ? $"Удалить ячейку {SlotNumberOf(_asked)}?" : "Удалить сохранение?",
            Question.Load => SaveGames.IsAutosave(_asked) ? "Загрузить автосохранение?"
                : SlotNumberOf(_asked) > 0 ? $"Загрузить ячейку {SlotNumberOf(_asked)}?" : "Загрузить сохранение?",
            _ => _inGame ? "Сохранения" : "Загрузить колонию"
        };
        /// <summary>The question's warning, as shown.</summary>
        public string QuestionText => _calloutText.text;
        /// <summary>The slots listed, top to bottom.</summary>
        public IReadOnlyList<string> RowIds => _rows.ConvertAll(row => row.Id);
        public string Selected => _selected;
        /// <summary>The textures the sheet holds now (rows and questions); released when it closes.</summary>
        public IReadOnlyList<Texture2D> Pictures
        {
            get
            {
                var all = new List<Texture2D>(_pictures.Made);
                all.AddRange(_questionPictures.Made);
                return all;
            }
        }
        /// <summary>The title or the page changed (a question opened or closed): the host shows the new heading.</summary>
        public event Action Changed;

        public Button RowButton(string slotId) => Find(slotId)?.Button;
        public SaveSlotInfo SlotOf(string slotId) => Find(slotId)?.Slot;
        /// <summary>The row's slot cannot be opened here (a newer version's, a longer edition's, damaged).</summary>
        public SaveProblem ProblemOf(string slotId) => Find(slotId)?.Problem ?? SaveProblem.None;
        /// <summary>The row says «Сохранено»: the player has just saved into it.</summary>
        public bool ShowsSaved(string slotId) => Find(slotId)?.ShowsSaved == true;
        /// <summary>The row's line under its title (why it is locked, or that the save failed); null when it has none.</summary>
        public string NoteOf(string slotId) => Find(slotId)?.Note?.text;

        /// <summary>Lists the slots afresh and chooses one: a free slot in the game, the latest save at launch.</summary>
        public void Open()
        {
            _justSaved = null;
            _failed.Clear();
            _selected = null;
            CloseQuestion();
            Rebuild();
        }

        /// <summary>The slots changed under the open list (an autosave): the rows follow, the choice stays.</summary>
        public void Refresh()
        {
            if (Asking == Question.None) Rebuild();
        }

        /// <summary>Esc and the back arrow: a question goes back to the list; false when the list shows.</summary>
        public bool Back()
        {
            if (Asking == Question.None) return false;
            CloseQuestion();
            Changed?.Invoke();
            return true;
        }

        /// <summary>The sheet is put away: its rows and their pictures go.</summary>
        public void Release()
        {
            CloseQuestion();
            _pictures.Release();
            _rows.Clear();
            _scroll.Clear();
        }

        public void Select(string slotId)
        {
            if (Find(slotId) == null) return;
            _selected = slotId;
            ShowSelection();
        }

        // ----- the list -----

        private void Rebuild()
        {
            _pictures.Release();
            _rows.Clear();
            _scroll.Clear();
            var listed = new Dictionary<string, SaveSlotInfo>();
            foreach (var slot in _saves.List()) listed[slot.SlotId] = slot;
            var ids = new List<string> { SaveGames.AutosaveSlotId };
            ids.AddRange(ManualSlots);
            // a slot the sheet does not name (a tester's, a tool's) still lists, after the sheet's own
            foreach (var slot in _saves.List())
                if (!ids.Contains(slot.SlotId)) ids.Add(slot.SlotId);
            foreach (string id in ids)
            {
                var slot = listed.TryGetValue(id, out var known) ? known : _saves.Describe(id);
                // at launch only what can be opened or cleared away
                if (!_inGame && slot.IsEmpty) continue;
                var row = new Row
                {
                    Id = id,
                    Slot = slot,
                    Problem = _refused.TryGetValue(id, out var refused) ? refused : slot.Problem
                };
                row.Button = Ui.TextButton(string.Empty, "save-row");
                Fill(row.Button, row, true);
                string picked = id;
                UiFeel.Bind(row.Button, () => Select(picked));
                _rows.Add(row);
                _scroll.Add(row.Button);
            }
            if (Find(_selected) == null) _selected = FirstChoice();
            ShowSelection();
        }

        // A free slot in the game (else the one saved last); the latest save at launch.
        private string FirstChoice()
        {
            if (_inGame)
            {
                foreach (var row in _rows)
                    if (row.Slot.IsEmpty && !row.Slot.IsAutosave) return row.Id;
                Row newest = null;
                foreach (var row in _rows)
                    if (!row.Slot.IsAutosave && (newest == null || row.Slot.SavedAtUtc > newest.Slot.SavedAtUtc)) newest = row;
                if (newest != null) return newest.Id;
            }
            else
            {
                var latest = _saves.Latest();
                if (latest != null && Find(latest.SlotId) != null) return latest.SlotId;
            }
            return _rows.Count > 0 ? _rows[0].Id : null;
        }

        private void ShowSelection()
        {
            foreach (var row in _rows) row.Button.EnableInClassList("is-on", row.Id == _selected);
            var current = Find(_selected);
            UiFeel.SetAvailable(DeleteButton, CanDelete(current));
            UiFeel.SetAvailable(SaveButton, CanSave(current));
            UiFeel.SetAvailable(LoadButton, CanLoad(current));
            Ui.SetCaption(SaveButton, SaveCaption);
        }

        private Row Current => Find(_selected);

        private Row Find(string slotId)
        {
            if (slotId == null) return null;
            foreach (var row in _rows)
                if (row.Id == slotId) return row;
            return null;
        }

        private static bool CanDelete(Row row) => row != null && !row.Slot.IsAutosave && !row.Slot.IsEmpty;

        private bool CanSave(Row row) =>
            _inGame && row != null && !row.Slot.IsAutosave && row.Problem != SaveProblem.Damaged;

        private static bool CanLoad(Row row) => row != null && !row.Slot.IsEmpty && row.Problem == SaveProblem.None;

        private string SaveCaption => Current == null || Current.Slot.IsEmpty ? "Сохранить" : "Перезаписать";

        private string DeleteHint()
        {
            var row = Current;
            if (row == null) return null;
            if (row.Slot.IsAutosave) return "Автосохранение ведёт сама игра, его не удалить.";
            if (row.Slot.IsEmpty) return "В ячейке пусто.";
            return "Убрать колонию из ячейки. Игра спросит ещё раз.";
        }

        private string SaveHint()
        {
            var row = Current;
            if (row == null) return null;
            if (row.Slot.IsAutosave) return "Автосохранение ведёт сама игра. Выберите ячейку, чтобы сохранить колонию.";
            if (row.Problem == SaveProblem.Damaged) return "Файл в ячейке повреждён: сначала удалите его.";
            if (row.Slot.IsEmpty) return "Сохранить нынешнюю колонию в эту ячейку.";
            return "Сохранить нынешнюю колонию на место той, что в ячейке. Игра спросит ещё раз.";
        }

        private string LoadHint()
        {
            var row = Current;
            if (row == null) return null;
            if (row.Slot.IsEmpty) return "В ячейке пусто.";
            if (row.Problem != SaveProblem.None) return LockHint(row);
            return _inGame ? "Открыть эту колонию вместо нынешней." : "Открыть эту колонию.";
        }

        // ----- a row: its picture, its title and numbers, when -----

        private void Fill(VisualElement box, Row row, bool listed)
        {
            var slot = row.Slot;
            var summary = slot.Header?.Summary;
            bool empty = slot.IsEmpty;
            bool damaged = !empty && row.Problem == SaveProblem.Damaged;
            bool locked = !empty && (row.Problem == SaveProblem.NewerBuild || row.Problem == SaveProblem.OtherEdition);
            box.EnableInClassList("is-empty", empty);
            box.EnableInClassList("is-locked", locked);
            box.EnableInClassList("is-broken", damaged);

            if (empty)
            {
                var hollow = Ui.Box("save-row__thumb save-row__thumb--empty");
                hollow.pickingMode = PickingMode.Ignore;
                hollow.Add(GameLinks.Glyph("save", "save-row__empty-glyph"));
                box.Add(hollow);
            }
            else
            {
                // a damaged file shows no picture: what it holds cannot be trusted
                if (listed && !damaged) row.Picture = _pictures.From(slot.Thumbnail);
                var thumb = SaveParts.Thumb(damaged ? null : row.Picture, "save-row__thumb");
                if (locked || damaged)
                {
                    var badge = SaveParts.Lock();
                    _tooltip?.Attach(badge, () => LockTitle(row), () => LockHint(row));
                    thumb.Add(badge);
                }
                box.Add(thumb);
            }

            var main = Ui.Box("save-row__main");
            main.pickingMode = PickingMode.Ignore;
            var head = Ui.Box("save-row__head");
            head.pickingMode = PickingMode.Ignore;
            head.Add(Caption(SaveParts.SlotName(row.Id), "save-row__name t-caption"));
            // a game a shorter edition saved, carried on here with the longer chain
            string carried = !empty && row.Problem == SaveProblem.None ? slot.CarriedFrom : null;
            if (carried != null)
            {
                var chip = Ui.Text(SaveParts.CarriedWord(carried), "chip save-row__carried");
                _tooltip?.Attach(chip, () => "Из другой версии", () => SaveParts.CarriedHint(carried));
                head.Add(chip);
            }
            main.Add(head);
            if (empty) main.Add(Caption("Пусто", "save-row__title t-black t-faint"));
            else if (damaged)
            {
                main.Add(Caption("Файл повреждён", "save-row__title t-black"));
                row.Note = Caption(LockNote(row), "save-row__note t-muted");
                main.Add(row.Note);
            }
            else
            {
                main.Add(Caption(SaveParts.Title(summary), "save-row__title t-black" + (locked ? " save-row__faded" : string.Empty)));
                if (locked)
                {
                    row.Note = Caption(LockNote(row), "save-row__note t-warn t-bold");
                    main.Add(row.Note);
                }
                else main.Add(SaveParts.Facts(summary, _catalog, _tooltip, "save-facts--small save-row__facts"));
            }
            if (listed && _failed.TryGetValue(row.Id, out var failure))
            {
                row.Note = Caption(failure, "save-row__note t-bad t-bold");
                main.Add(row.Note);
            }
            box.Add(main);

            if (empty) return;
            var side = Ui.Box("save-row__side");
            side.pickingMode = PickingMode.Ignore;
            row.ShowsSaved = listed && row.Id == _justSaved;
            if (row.ShowsSaved)
            {
                var saved = Ui.Box("save-row__saved");
                saved.pickingMode = PickingMode.Ignore;
                saved.Add(GameLinks.Glyph("check", "save-row__saved-glyph"));
                saved.Add(Caption("Сохранено", "save-row__saved-text t-black"));
                side.Add(saved);
            }
            else side.Add(Caption(SaveParts.When(slot.SavedAtUtc, _now()),
                "save-row__when t-bold" + (locked || damaged ? " t-faint" : string.Empty)));
            if (!locked && !damaged && summary != null)
                side.Add(SaveParts.WhenLine(SaveParts.PlayTime(summary.ActiveTimeMs), "save-row__play"));
            box.Add(side);
        }

        private static Label Caption(string text, string classes)
        {
            var label = Ui.Text(text, classes);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        private string LockTitle(Row row) => SaveParts.ProblemTitle(row.Problem);

        private string LockNote(Row row) => SaveParts.ProblemNote(row.Slot, row.Problem, _saves.Stamp.Build);

        private string LockHint(Row row) => SaveParts.ProblemHint(row.Slot, row.Problem, _saves.Stamp.Build);

        // ----- the buttons and their questions -----

        private void PressSave()
        {
            var row = Current;
            if (row == null) return;
            if (row.Slot.IsEmpty) Save(row.Id);
            else Ask(Question.Overwrite);
        }

        private void PressLoad()
        {
            var row = Current;
            if (row == null) return;
            // in the game, play since the last save would be lost: ask first
            if (_inGame && _saves.HasUnsavedChanges) Ask(Question.Load);
            else Load(row.Id);
        }

        private void Ask(Question question)
        {
            var row = Current;
            if (row == null) return;
            _asked = row.Id;
            Asking = question;
            _questionPictures.Release();
            _questionBody.Clear();
            int number = SlotNumberOf(row.Id);
            switch (question)
            {
                case Question.Overwrite:
                    var compare = Ui.Box("save-compare");
                    compare.Add(CompareCard("Сейчас в ячейке", row.Picture, row.Slot.Header?.Summary,
                        SaveParts.Played(row.Slot, _now()), row.Problem != SaveProblem.None));
                    var arrow = Ui.Box("save-compare__arrow");
                    arrow.Add(GameLinks.Glyph("chevron", "save-compare__glyph"));
                    compare.Add(arrow);
                    var now = _saves.Preview();
                    compare.Add(CompareCard("Будет", _questionPictures.From(_saves.PictureNow()), now,
                        $"Сейчас, в игре {SaveParts.PlayTime(now?.ActiveTimeMs ?? 0)}", false));
                    _questionBody.Add(compare);
                    SetCallout("save", number > 0
                        ? $"Колония из ячейки {number} пропадёт, на её месте будет нынешняя."
                        : "Колония из этого сохранения пропадёт, на её месте будет нынешняя.");
                    Ui.SetCaption(ConfirmButton, "Перезаписать");
                    break;
                case Question.Delete:
                    _questionBody.Add(StaticRow(row));
                    SetCallout("trash", number > 0
                        ? $"Колония из ячейки {number} пропадёт насовсем."
                        : "Колония из этого сохранения пропадёт насовсем.");
                    Ui.SetCaption(ConfirmButton, "Удалить");
                    break;
                case Question.Load:
                    _questionBody.Add(StaticRow(row));
                    var since = _saves.LastSavedAtUtc;
                    SetCallout("restart", since != null
                        ? $"Всё, что не сохранено с {SaveParts.Moment(since.Value, _now())}, пропадёт."
                        : "Нынешняя колония ещё не сохранена и пропадёт.");
                    Ui.SetCaption(ConfirmButton, "Загрузить");
                    break;
            }
            Ui.Show(_list, false);
            Ui.Show(_question, true);
            Changed?.Invoke();
        }

        private void Confirm()
        {
            string id = _asked;
            switch (Asking)
            {
                case Question.Overwrite:
                    Save(id);
                    break;
                case Question.Delete:
                    Delete(id);
                    break;
                case Question.Load:
                    Load(id);
                    break;
            }
        }

        private void Save(string slotId)
        {
            var result = _saves.Save(slotId);
            _selected = slotId;
            if (result.Ok)
            {
                _failed.Remove(slotId);
                _refused.Remove(slotId);
                _justSaved = slotId;
            }
            else _failed[slotId] = "Сохранить не удалось. Проверьте, есть ли место на диске, и попробуйте ещё раз.";
            BackToList();
            if (!result.Ok) Refuse(Find(slotId)?.Button);
        }

        private void Delete(string slotId)
        {
            bool deleted = _saves.Delete(slotId);
            if (deleted)
            {
                _refused.Remove(slotId);
                _failed.Remove(slotId);
                if (_justSaved == slotId) _justSaved = null;
            }
            BackToList();
            if (!deleted) Refuse(Find(slotId)?.Button);
        }

        private void Load(string slotId)
        {
            var result = _saves.Load(slotId);
            if (result.Ok)
            {
                _opened?.Invoke();
                return;
            }
            // the header looked fine but the whole save did not pass: the row locks and says why
            _refused[slotId] = result.Problem == SaveProblem.None ? SaveProblem.Damaged : result.Problem;
            _selected = slotId;
            BackToList();
            Refuse(Find(slotId)?.Button);
        }

        private void BackToList()
        {
            CloseQuestion();
            Rebuild();
            Changed?.Invoke();
        }

        private void CloseQuestion()
        {
            Asking = Question.None;
            _asked = null;
            _questionPictures.Release();
            _questionBody.Clear();
            Ui.Show(_question, false);
            Ui.Show(_list, true);
        }

        private static void Refuse(VisualElement element)
        {
            GameAudio.Play(Sfx.UiDenied);
            if (element != null) UiMotion.Nudge(element, 6f);
        }

        private void SetCallout(string glyph, string text)
        {
            foreach (var name in new[] { "save", "trash", "restart" })
                _calloutGlyph.EnableInClassList("glyph--" + name, name == glyph);
            Ui.SetText(_calloutText, text);
        }

        private VisualElement CompareCard(string caption, Texture2D picture, SaveSummary summary, string when, bool locked)
        {
            var card = Ui.Box("save-compare__card panel-soft" + (locked ? " is-locked" : string.Empty));
            card.Add(Ui.Text(caption, "t-caption"));
            var thumb = SaveParts.Thumb(picture, "save-compare__thumb");
            if (locked) thumb.Add(SaveParts.Lock());
            card.Add(thumb);
            card.Add(Ui.Text(SaveParts.Title(summary), "save-compare__title t-black"));
            card.Add(SaveParts.Facts(summary, _catalog, _tooltip, "save-facts--small"));
            card.Add(SaveParts.WhenLine(when, "save-compare__when"));
            return card;
        }

        // the slot as its row shows it, not to be pressed
        private VisualElement StaticRow(Row row)
        {
            var box = Ui.Box("save-row save-row--static");
            var copy = new Row { Id = row.Id, Slot = row.Slot, Problem = row.Problem, Picture = row.Picture };
            Fill(box, copy, false);
            return box;
        }

        private static int SlotNumberOf(string slotId) => slotId == null ? 0 : SaveParts.SlotNumber(slotId);
    }
}
