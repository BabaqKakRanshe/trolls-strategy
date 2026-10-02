using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Right-click fan of orders for the selected creatures: a small arc of round buttons over the point the
    /// player clicked, the key's letter on each, its name and meaning in the hint. It shows only while the
    /// interaction controller keeps the commands open; any other intent closes it.
    /// </summary>
    public sealed class CommandFan
    {
        private const float EdgeMargin = 140f;
        // The arc: three discs on a half circle of this radius over the click, kept clear of the pointer.
        private const float ArcRadius = 82f;

        private readonly ColonyHudContext _context;
        private readonly HudTooltip _tooltip;
        private readonly VisualElement _layer;
        private readonly VisualElement _fan;
        private readonly List<Button> _buttons = new();

        public CommandFan(VisualElement root, ColonyHudContext context, HudTooltip tooltip = null)
        {
            _context = context;
            _tooltip = tooltip;
            _layer = Ui.Require<VisualElement>(root, "fan-layer");
            _fan = Ui.Require<VisualElement>(root, "command-fan");
            var interaction = context.Interaction;
            Add("Работа", "E", "glyph--work", primary: true, 200f, interaction.BeginWorkTarget,
                "Потом кликни по зданию: туда встанут работать.", silent: false);
            Add("Перенос", "H", "glyph--haul", primary: true, 270f, interaction.BeginHaulTarget,
                "Кликни по двум зданиям: откуда и куда носить.", silent: false);
            Add("Свободны", "R", "glyph--free", primary: false, 340f, interaction.ReleaseSelected,
                "Снять с работы и маршрута.", silent: true);
            Ui.Show(_fan, false);
        }

        public bool IsShown => Ui.IsShown(_fan);
        public IReadOnlyList<Button> Buttons => _buttons;

        /// <summary>Centres the fan on a point in panel coordinates, kept clear of the screen edges.</summary>
        public void OpenAt(Vector2 panelPoint)
        {
            var local = _layer.WorldToLocal(panelPoint);
            var size = _layer.layout.size;
            if (size.x > 2f * EdgeMargin) local.x = Mathf.Clamp(local.x, EdgeMargin, size.x - EdgeMargin);
            if (size.y > 2f * EdgeMargin) local.y = Mathf.Clamp(local.y, EdgeMargin * .75f, size.y - EdgeMargin * .75f);
            _fan.style.left = local.x;
            _fan.style.top = local.y;
            Refresh();
            if (!IsShown) return;
            _fan.BringToFront();
            foreach (var button in _buttons) UiMotion.PopIn(button, .22f);
        }

        public void Refresh()
        {
            var interaction = _context.Interaction;
            Ui.Show(_fan, interaction.CommandsOpen && interaction.SelectedIds.Count > 0 &&
                          interaction.Mode.Type == InteractionModeType.Neutral);
        }

        // One round button at an angle on the arc (screen degrees: 270 is straight up), its key on its rim.
        private void Add(string name, string hotkey, string glyph, bool primary, float degrees, Action action, string hint,
            bool silent)
        {
            var button = Ui.TextButton(string.Empty, "btn btn-disc fan__disc" + (primary ? " is-primary" : string.Empty));
            float angle = degrees * Mathf.Deg2Rad;
            button.style.left = Mathf.Cos(angle) * ArcRadius;
            button.style.top = Mathf.Sin(angle) * ArcRadius;
            var icon = Ui.Box("glyph " + glyph);
            icon.pickingMode = PickingMode.Ignore;
            button.Add(icon);
            var key = Ui.Text(hotkey, "fan__key t-black");
            key.pickingMode = PickingMode.Ignore;
            button.Add(key);
            var interaction = _context.Interaction;
            UiFeel.Bind(button, () =>
            {
                interaction.ToggleCommands(false);
                action();
            }, silentClick: silent);
            _tooltip?.Attach(button, () => name, () => hint, hotkey);
            _fan.Add(button);
            _buttons.Add(button);
        }
    }
}
