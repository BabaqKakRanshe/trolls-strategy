using System;
using TrollStrategy.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>Step 3: the squad count, what to do next, automatic placement and the start of the battle.</summary>
    public sealed class DeploymentActions
    {
        private static readonly Color RefusalColor = new(1f, .55f, .45f, 1f);

        private readonly Label _squad;
        private readonly Label _hint;
        private BattleDeployment _deployment;

        public DeploymentActions(VisualElement root, Action start)
        {
            _squad = Ui.Require<Label>(root, "battle-squad");
            _hint = Ui.Require<Label>(root, "battle-hint");
            AutoPlace = UiFeel.Bind(Ui.Require<Button>(root, "battle-auto"), () => _deployment?.AutoPlace());
            // the battle's own start sound answers a start that goes through
            Start = UiFeel.Bind(Ui.Require<Button>(root, "battle-start"), start, silentClick: true);
        }

        public Button AutoPlace { get; }
        public Button Start { get; }
        public string Squad => _squad.text;
        public string Hint => _hint.text;

        public void Attach(BattleDeployment deployment)
        {
            _deployment = deployment;
            Refresh();
        }

        public void Refresh()
        {
            if (_deployment == null) return;
            int count = _deployment.Placements.Count;
            Ui.SetText(_squad, $"3 · ОТРЯД {count} / {_deployment.MaxUnits}");
            Ui.SetText(_hint, _deployment.Message);
            UiFeel.SetAvailable(AutoPlace, _deployment.CanAutoPlace);
            UiFeel.SetAvailable(Start, _deployment.CanStart);
            Ui.SetCaption(Start, count == 0 ? "СНАЧАЛА РАССТАВЬ БОЙЦОВ" : $"НАЧАТЬ БОЙ · {count} В ОТРЯДЕ");
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
