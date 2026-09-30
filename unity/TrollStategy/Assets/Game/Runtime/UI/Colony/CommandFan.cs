using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Right-click fan of commands for the selected creatures, opened where the player clicked. It shows
    /// only while the interaction controller keeps the commands open; any other intent closes it.
    /// </summary>
    public sealed class CommandFan
    {
        private const float EdgeMargin = 140f;

        private readonly ColonyHudContext _context;
        private readonly VisualElement _layer;
        private readonly VisualElement _fan;
        private readonly List<Button> _buttons = new();

        public CommandFan(VisualElement root, ColonyHudContext context)
        {
            _context = context;
            _layer = Ui.Require<VisualElement>(root, "fan-layer");
            _fan = Ui.Require<VisualElement>(root, "command-fan");
            var interaction = context.Interaction;
            Add("Работа", "E", new Vector2(-122f, -8f), interaction.BeginWorkTarget, silent: false);
            Add("Перенос", "H", new Vector2(0f, -66f), interaction.BeginHaulTarget, silent: false);
            Add("Свободны", "R", new Vector2(122f, -8f), interaction.ReleaseSelected, silent: true);
            Add("В бараки", null, new Vector2(0f, 54f), interaction.SendSelectedToBarracks, silent: true);
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

        private void Add(string text, string hotkey, Vector2 offset, Action action, bool silent)
        {
            var button = Ui.CaptionButton(text, hotkey, "btn fan__button");
            button.style.left = offset.x;
            button.style.top = offset.y;
            var interaction = _context.Interaction;
            UiFeel.Bind(button, () =>
            {
                interaction.ToggleCommands(false);
                action();
            }, silentClick: silent);
            _fan.Add(button);
            _buttons.Add(button);
        }
    }
}
