using System.Collections.Generic;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Where to find the author and other players, the game's Steam page, and the line about bug reports, as the
    /// intro and the about page show them.
    /// </summary>
    public static class GameLinks
    {
        /// <summary>
        /// The game's store page in Steam, for the alpha's "Демо в Steam" and the demo's wishlist button. Empty
        /// until the page is public: both buttons stay hidden meanwhile.
        /// </summary>
        public static readonly string SteamPage = "";

        public static bool HasSteamPage => !string.IsNullOrEmpty(SteamPage);

        public static readonly (string Name, string Glyph, string Url)[] Community =
        {
            ("Telegram", "telegram", "https://t.me/milyu_gamedev"),
            ("YouTube", "youtube", "https://www.youtube.com/@prosti_gospodi_gd"),
            ("Discord", "discord", "https://discord.gg/JujneGkDd"),
            ("Чат игроков", "players", "https://t.me/+n8mmLmvWdAI3ZWMy"),
            ("Почта", "mail", "mailto:vladimir.milyutin.98@gmail.com")
        };

        public const string Invite = "Заходите в гости";

        public const string Help = "Если что-то пошло не так, нажмите F8 → «Отправить логи» или напишите мне.";

        /// <summary>The community as round buttons with their names under them; returns the buttons.</summary>
        public static List<Button> AddDiscs(VisualElement parent)
        {
            var buttons = new List<Button>();
            var row = Ui.Box("link-discs");
            foreach (var (name, glyph, url) in Community)
            {
                var item = Ui.Box("link-disc");
                item.pickingMode = PickingMode.Ignore;
                var button = Ui.TextButton(string.Empty, "btn btn-disc link-disc__button");
                button.Add(Glyph(glyph));
                string target = url;
                UiFeel.Bind(button, () => UnityEngine.Application.OpenURL(target));
                var caption = Ui.Text(name, "link-disc__caption t-bold");
                caption.pickingMode = PickingMode.Ignore;
                item.Add(button);
                item.Add(caption);
                row.Add(item);
                buttons.Add(button);
            }
            parent.Add(row);
            return buttons;
        }

        /// <summary>The community as a column of rows: picture, name and the sign of a page outside the game.</summary>
        public static List<Button> AddRows(VisualElement parent)
        {
            var buttons = new List<Button>();
            foreach (var (name, glyph, url) in Community)
            {
                var button = Ui.CaptionButton(name, null, "btn link-row");
                var icon = Ui.Box("link-row__icon");
                icon.pickingMode = PickingMode.Ignore;
                icon.Add(Glyph(glyph));
                button.Insert(0, icon);
                button.Add(Glyph("external", "link-row__out"));
                string target = url;
                UiFeel.Bind(button, () => UnityEngine.Application.OpenURL(target));
                parent.Add(button);
                buttons.Add(button);
            }
            return buttons;
        }

        /// <summary>The F8 key as a keycap beside the sentence about sending logs.</summary>
        public static VisualElement HelpLine(string classes)
        {
            var line = Ui.Box("callout " + classes);
            line.Add(Ui.Text("F8", "keycap"));
            line.Add(Ui.Text(Help, "callout__text"));
            return line;
        }

        public static void OpenSteamPage()
        {
            if (HasSteamPage) UnityEngine.Application.OpenURL(SteamPage);
        }

        public static VisualElement Glyph(string glyph, string classes = null)
        {
            var element = Ui.Box("glyph glyph--" + glyph + (classes != null ? " " + classes : string.Empty));
            element.pickingMode = PickingMode.Ignore;
            return element;
        }
    }
}
