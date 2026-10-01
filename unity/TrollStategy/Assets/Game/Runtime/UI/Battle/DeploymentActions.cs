using System;
using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The deployment's one line under the board, what to do next or why the last click was refused, and
    /// its two actions on the right: automatic placement and the start of the battle.
    /// </summary>
    public sealed class DeploymentActions
    {
        private static readonly Color RefusalColor = new(.72f, .21f, .14f, 1f);

        private readonly Label _hint;
        private BattleDeployment _deployment;

        public DeploymentActions(VisualElement actions, VisualElement hint, Action start, HudTooltip tooltip = null)
        {
            _hint = Ui.Require<Label>(hint, "battle-hint");
            AutoPlace = UiFeel.Bind(Ui.Require<Button>(actions, "battle-auto"), () => _deployment?.AutoPlace());
            // the battle's own start sound answers a start that goes through
            Start = UiFeel.Bind(Ui.Require<Button>(actions, "battle-start"), start, silentClick: true);
            tooltip?.Attach(AutoPlace, () => "Авторасстановка", () => "Поставить бойцов из резерва на свободные синие клетки.");
            tooltip?.Attach(Start, () => "Начать бой",
                () => "Бой идёт сам. Павшие бойцы и их снаряжение не вернутся.");
        }

        public Button AutoPlace { get; }
        public Button Start { get; }
        public string Hint => _hint.text;

        public void Attach(BattleDeployment deployment)
        {
            _deployment = deployment;
            Refresh();
        }

        public void Refresh()
        {
            if (_deployment == null) return;
            Ui.SetText(_hint, _deployment.Message);
            UiFeel.SetAvailable(AutoPlace, _deployment.CanPlaceMore);
            UiFeel.SetAvailable(Start, _deployment.CanStart);
        }

        /// <summary>"No": the hint shows the reason, shakes and flashes red.</summary>
        public void Refuse()
        {
            if (_deployment != null) Ui.SetText(_hint, _deployment.Message);
            UiMotion.Nudge(_hint, 9f);
            UiMotion.Flash(_hint, RefusalColor, .5f);
        }
    }
}
