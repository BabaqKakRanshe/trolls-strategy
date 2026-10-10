using System;
using System.Collections.Generic;
using System.Globalization;
using TrollStrategy.Application;
using TrollStrategy.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// What a save shows wherever the HUD lists it (the slot sheet, the launch window, the questions before an
    /// overwrite, a delete or a load): «Уровень N: title», the colony's facts as a picture and a number with their
    /// names in the hint, when it was saved and how long it was played. Words only; the rules stay in
    /// <see cref="SaveGames"/>.
    /// </summary>
    public static class SaveParts
    {
        /// <summary>The colony's place in its campaign: «Уровень 14: Новая земля».</summary>
        public static string Title(SaveSummary summary)
        {
            if (summary == null) return "Колония";
            if (!summary.Campaign) return "Свободная игра";
            if (summary.Finished || string.IsNullOrEmpty(summary.QuestTitle)) return "Все задания пройдены";
            // the quest's title is its Russian key: the template translates it as a name
            return $"Уровень {summary.QuestLevel}: {summary.QuestTitle}";
        }

        /// <summary>The slot's name: the autosave, «Ячейка N» for the sheet's slots, a plain word for any other.</summary>
        public static string SlotName(string slotId)
        {
            if (SaveGames.IsAutosave(slotId)) return "Автосохранение";
            int number = SlotNumber(slotId);
            return number > 0 ? $"Ячейка {number}" : "Сохранение";
        }

        /// <summary>1–4 for the sheet's slots, 0 for any other.</summary>
        public static int SlotNumber(string slotId) => Array.IndexOf(SaveSheet.ManualSlots, slotId) + 1;

        /// <summary>When it was saved, in the player's clock: «Сегодня, 14:32», «Вчера, 21:10» or the date.</summary>
        public static string When(DateTime savedAtUtc, DateTime localNow)
        {
            var local = savedAtUtc.Kind == DateTimeKind.Local ? savedAtUtc : savedAtUtc.ToLocalTime();
            string time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (local.Date == localNow.Date) return $"Сегодня, {time}";
            if (local.Date == localNow.Date.AddDays(-1)) return $"Вчера, {time}";
            return local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + ", " + time;
        }

        /// <summary>The time a save's question names: «14:32» today, the date and time on another day.</summary>
        public static string Moment(DateTime savedAtUtc, DateTime localNow)
        {
            var local = savedAtUtc.Kind == DateTimeKind.Local ? savedAtUtc : savedAtUtc.ToLocalTime();
            string time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
            return local.Date == localNow.Date ? time : local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + " " + time;
        }

        /// <summary>Colony time played: «19 мин», «2 ч 05 мин».</summary>
        public static string PlayTime(int activeMs)
        {
            int minutes = Math.Max(0, activeMs) / 60000;
            if (minutes < 1) return "меньше минуты";
            if (minutes < 60) return $"{minutes} мин";
            return $"{minutes / 60} ч {minutes % 60:00} мин";
        }

        /// <summary>When it was saved and how long it was played: «Сегодня, 14:32, в игре 19 мин».</summary>
        public static string Played(SaveSlotInfo slot, DateTime localNow)
        {
            string when = When(slot.SavedAtUtc, localNow);
            var summary = slot.Header?.Summary;
            return summary != null ? $"{when}, в игре {PlayTime(summary.ActiveTimeMs)}" : when;
        }

        /// <summary>The heading of a lock's hint.</summary>
        public static string ProblemTitle(SaveProblem problem) =>
            problem == SaveProblem.Damaged ? "Файл повреждён" : "Не открыть в этой версии";

        /// <summary>Why a slot stays shut, in the few words under its title; null when it opens.</summary>
        public static string ProblemNote(SaveSlotInfo slot, SaveProblem problem, string currentBuild)
        {
            switch (problem)
            {
                case SaveProblem.Damaged:
                    return slot.IsAutosave ? "Колонию из него не открыть." : "Колонию из него не открыть. Ячейку можно удалить.";
                case SaveProblem.OtherEdition:
                    return EditionSentence(slot.Header?.Edition);
                case SaveProblem.NewerBuild:
                    return string.IsNullOrEmpty(slot.WrittenBy)
                        ? "Сохранено более новой версией игры"
                        : $"Сохранено версией {slot.WrittenBy}, у вас {currentBuild}";
                default:
                    return null;
            }
        }

        /// <summary>What the player can do about a shut slot, for its lock's hint.</summary>
        public static string ProblemHint(SaveSlotInfo slot, SaveProblem problem, string currentBuild)
        {
            switch (problem)
            {
                case SaveProblem.Damaged:
                    return ProblemNote(slot, problem, currentBuild);
                case SaveProblem.OtherEdition:
                    return EditionHint(slot.Header?.Edition);
                case SaveProblem.NewerBuild:
                    return string.IsNullOrEmpty(slot.WrittenBy)
                        ? "Её сохранила более новая версия игры. Обновите игру, и колония откроется."
                        : $"Ячейку сохранила версия {slot.WrittenBy}, а у вас {currentBuild}. Обновите игру, и колония откроется.";
                default:
                    return null;
            }
        }

        /// <summary>A save a longer edition wrote (the full game's in the demo): which one, and that it stays shut here.</summary>
        public static string EditionSentence(string edition) => edition switch
        {
            "Full" => "Эту колонию сохранила полная игра, здесь её не открыть.",
            "SteamDemo" => "Эту колонию сохранила демо-версия из Steam, здесь её не открыть.",
            "Alpha" => "Эту колонию сохранила альфа-версия, здесь её не открыть.",
            _ => "Эту колонию сохранила другая версия игры, здесь её не открыть."
        };

        private static string EditionHint(string edition) => edition switch
        {
            "Full" => "Откройте её в полной игре: там колония продолжится с того же места.",
            "SteamDemo" => "Откройте её в демо-версии из Steam или в полной игре: там колония продолжится.",
            _ => "Откройте её в той версии игры, что её сохранила."
        };

        /// <summary>A game a shorter edition saved and this one carries on, in a word beside the slot's name.</summary>
        public static string CarriedWord(string edition) => edition switch
        {
            "Alpha" => "из альфа-версии",
            "SteamDemo" => "из демо-версии",
            _ => "из другой версии"
        };

        public static string CarriedHint(string edition) => edition switch
        {
            "Alpha" => "Колонию сохранила альфа-версия. Здесь она продолжится, и заданий будет больше.",
            "SteamDemo" => "Колонию сохранила демо-версия. Здесь она продолжится, и заданий будет больше.",
            _ => "Колонию сохранила другая версия игры. Здесь она продолжится."
        };

        /// <summary>
        /// The colony's numbers as a picture and a number each: gold, creatures, buildings, land and arena wins;
        /// what each one is goes into the hint.
        /// </summary>
        public static VisualElement Facts(SaveSummary summary, GameContentCatalog catalog, HudTooltip tooltip,
            string classes)
        {
            var row = Ui.Box("save-facts " + classes);
            row.pickingMode = PickingMode.Ignore;
            if (summary == null) return row;
            row.Add(Fact(RewardArt.Coin(catalog), null, summary.Gold, "Золото", "Казна колонии.", tooltip));
            row.Add(Fact(RewardArt.BuildingIcon(catalog, BuildingKind.Barracks), null, summary.Creatures,
                "Существа", "Тролли, гоблины и другие жители колонии.", tooltip));
            row.Add(Fact(RewardArt.BuildingIcon(catalog, BuildingKind.Warehouse), null, summary.Buildings,
                "Здания", "Сколько зданий стоит на острове.", tooltip));
            if (summary.LandOwned > 0)
                row.Add(Fact(null, "land", summary.LandOwned, "Земля", "Блоков земли у колонии.", tooltip));
            row.Add(Fact(null, "battle", summary.BattlesWon, "Победы на арене", "Сколько боёв выиграл отряд.", tooltip));
            return row;
        }

        /// <summary>A clock and «Сегодня, 14:32, в игре 19 мин».</summary>
        public static VisualElement WhenLine(string text, string classes = null)
        {
            var line = Ui.Box("save-when" + (classes != null ? " " + classes : string.Empty));
            line.pickingMode = PickingMode.Ignore;
            line.Add(GameLinks.Glyph("rest", "save-when__glyph"));
            var label = Ui.Text(text, "save-when__text");
            label.pickingMode = PickingMode.Ignore;
            line.Add(label);
            return line;
        }

        /// <summary>A picture box: the save's island, or the paper behind it while there is none.</summary>
        public static VisualElement Thumb(Texture2D picture, string classes)
        {
            var thumb = Ui.Box(classes);
            thumb.pickingMode = PickingMode.Ignore;
            var image = Ui.Box("save-thumb__picture");
            image.pickingMode = PickingMode.Ignore;
            if (picture != null) image.style.backgroundImage = new StyleBackground(picture);
            thumb.Add(image);
            return thumb;
        }

        /// <summary>A lock on a picture: the save cannot be opened here; the hint says why.</summary>
        public static VisualElement Lock()
        {
            var badge = Ui.Box("save-lock");
            badge.Add(GameLinks.Glyph("lock"));
            return badge;
        }

        private static VisualElement Fact(Sprite sprite, string glyph, int value, string name, string hint,
            HudTooltip tooltip)
        {
            var fact = Ui.Box("save-fact");
            if (sprite != null)
            {
                var icon = Ui.Box("save-fact__icon");
                icon.pickingMode = PickingMode.Ignore;
                icon.style.backgroundImage = new StyleBackground(sprite);
                fact.Add(icon);
            }
            else fact.Add(GameLinks.Glyph(glyph ?? "star", "save-fact__glyph"));
            var number = Ui.Text(value.ToString(CultureInfo.InvariantCulture), "save-fact__value t-black");
            number.pickingMode = PickingMode.Ignore;
            fact.Add(number);
            if (tooltip != null) tooltip.Attach(fact, () => name, () => hint);
            else fact.pickingMode = PickingMode.Ignore;
            return fact;
        }
    }

    /// <summary>
    /// Textures made from saves' PNG pictures for one list. A list that is rebuilt or closed releases the ones it
    /// made, so reopening the sheet never piles textures up.
    /// </summary>
    public sealed class SavePictures
    {
        private readonly List<Texture2D> _made = new();

        /// <summary>The textures alive now, oldest first.</summary>
        public IReadOnlyList<Texture2D> Made => _made;

        /// <summary>The PNG as a texture, or null for no picture or one that does not decode.</summary>
        public Texture2D From(byte[] png)
        {
            if (png == null || png.Length == 0) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "SavePicture",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            bool decoded;
            try
            {
                decoded = texture.LoadImage(png, true);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                decoded = false;
            }
            if (!decoded)
            {
                Destroy(texture);
                return null;
            }
            _made.Add(texture);
            return texture;
        }

        public void Release()
        {
            foreach (var texture in _made) Destroy(texture);
            _made.Clear();
        }

        private static void Destroy(UnityEngine.Object texture)
        {
            if (texture == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
