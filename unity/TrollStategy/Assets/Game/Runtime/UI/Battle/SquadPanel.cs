using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Content;
using System;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Who to put on the board, along the bottom left: a round button with the portrait of each kind of
    /// creature the colony has and how many of them are still in the reserve. The lit one is who a click on
    /// a free blue cell brings; a press picks another. A kind can also be dragged out onto the board: its
    /// portrait follows the pointer, and where it is let go the battle puts a fighter of that kind. Beside
    /// them: how many stand on the board of how many the mission takes. What each kind is good for in a fight
    /// is in its hint.
    /// </summary>
    public sealed class SquadPanel
    {
        private sealed class Kind
        {
            public UnitKind Value;
            public Button Button;
            public Label Count;
        }

        private readonly VisualElement _row;
        private readonly Label _squad;
        private readonly HudTooltip _tooltip;
        private readonly List<Kind> _kinds = new();
        private readonly VisualElement _ghost;
        private BattleDeployment _deployment;
        // a press on a kind: it becomes a drag once the pointer moves past the threshold
        private Kind _pressed;
        private Vector2 _pressedAt;
        private bool _dragging;

        public SquadPanel(VisualElement root, HudTooltip tooltip = null)
        {
            _row = Ui.Require<VisualElement>(root, "battle-kinds");
            _squad = Ui.Require<Label>(root, "battle-squad-value");
            _tooltip = tooltip;
            _ghost = Ui.Box("battle-kind__ghost");
            _ghost.pickingMode = PickingMode.Ignore;
            root.Add(_ghost);
            Ui.Show(_ghost, false);
            tooltip?.Attach(Ui.Require<VisualElement>(root, "battle-squad-size"), () => "Отряд",
                () => _deployment == null ? null
                    : $"На поле {_deployment.Placements.Count}, в бой идут до {_deployment.MaxUnits}.");
        }

        /// <summary>A kind was dragged out and let go; the pointer shows where.</summary>
        public event Action<UnitKind> KindDropped;

        public int Count => _kinds.Count;
        public bool IsDragging => _dragging;
        /// <summary>On the board of the mission's limit, as the panel shows it.</summary>
        public string Squad => _squad.text;

        public Button KindButton(UnitKind kind) => _kinds.Find(candidate => candidate.Value == kind)?.Button;

        public string ReserveOf(UnitKind kind) => _kinds.Find(candidate => candidate.Value == kind)?.Count.text;

        public void Build(BattleDeployment deployment)
        {
            _deployment = deployment;
            _row.Clear();
            _kinds.Clear();
            foreach (var value in deployment.Kinds)
            {
                var definition = deployment.DefinitionOf(value);
                var kind = new Kind { Value = value, Button = Ui.TextButton(string.Empty, "btn btn-disc battle-kind__button") };
                var portrait = Ui.Box("battle-kind__portrait");
                portrait.pickingMode = PickingMode.Ignore;
                Ui.SetPicture(portrait, RewardArt.Tight(definition.PortraitSprite));
                kind.Button.Add(portrait);
                kind.Count = Ui.Text(string.Empty, "battle-fact__value t-black halo");
                kind.Count.pickingMode = PickingMode.Ignore;
                var box = Ui.Box("battle-kind");
                box.pickingMode = PickingMode.Ignore;
                box.Add(kind.Button);
                box.Add(kind.Count);
                UiFeel.Bind(kind.Button, () => Pick(kind.Value), silentClick: true);
                // the button keeps the pointer from the press on, so it hears the whole drag
                kind.Button.RegisterCallback<PointerDownEvent>(e => Press(kind, e.position), TrickleDown.TrickleDown);
                kind.Button.RegisterCallback<PointerMoveEvent>(e => Drag(e.position), TrickleDown.TrickleDown);
                kind.Button.RegisterCallback<PointerUpEvent>(_ => LetGo(), TrickleDown.TrickleDown);
                kind.Button.RegisterCallback<PointerCaptureOutEvent>(_ => Cancel());
                _tooltip?.Attach(kind.Button, () => definition.DisplayName, () => RoleOf(kind.Value));
                _row.Add(box);
                _kinds.Add(kind);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (_deployment == null) return;
            foreach (var kind in _kinds)
            {
                int left = _deployment.ReserveOf(kind.Value);
                Ui.SetText(kind.Count, left.ToString());
                kind.Button.EnableInClassList("is-on", kind.Value == _deployment.PickedKind);
                UiFeel.SetAvailable(kind.Button, left > 0);
            }
            Ui.SetText(_squad, $"{_deployment.Placements.Count}/{_deployment.MaxUnits}");
        }

        private void Press(Kind kind, Vector2 at)
        {
            Cancel();
            if (_deployment == null || _deployment.ReserveOf(kind.Value) == 0) return;
            _pressed = kind;
            _pressedAt = at;
        }

        /// <summary>Moves the press to <paramref name="at"/> (panel coordinates); tests call it directly.</summary>
        public void Drag(Vector2 at)
        {
            if (_pressed == null) return;
            if (!_dragging && Vector2.Distance(at, _pressedAt) < UIInputUtils.DragThresholdPixels) return;
            if (!_dragging)
            {
                _dragging = true;
                _ghost.style.backgroundImage = _pressed.Button.Q(className: "battle-kind__portrait").resolvedStyle.backgroundImage;
                Ui.Show(_ghost, true);
                _ghost.BringToFront();
            }
            var local = _ghost.parent.WorldToLocal(at);
            _ghost.style.left = local.x - 36f;
            _ghost.style.top = local.y - 36f;
        }

        /// <summary>Ends the press: a drag hands its kind to the battle, a click stays the button's.</summary>
        public void LetGo()
        {
            var dropped = _dragging ? _pressed : null;
            Cancel();
            if (dropped != null) KindDropped?.Invoke(dropped.Value);
        }

        /// <summary>Starts a press on the kind's button at <paramref name="at"/>; tests call it directly.</summary>
        public void Press(UnitKind kind, Vector2 at)
        {
            var found = _kinds.Find(candidate => candidate.Value == kind);
            if (found != null) Press(found, at);
        }

        private void Cancel()
        {
            _pressed = null;
            _dragging = false;
            Ui.Show(_ghost, false);
        }

        private void Pick(UnitKind kind)
        {
            if (_deployment?.Pick(kind) == DeploymentResult.Selected) GameAudio.Play(Sfx.Select);
        }

        // what the kind is good for in a fight, in words the player can act on
        private string RoleOf(UnitKind kind)
        {
            if (_deployment == null) return null;
            string role = _deployment.DefinitionOf(kind).AttackRange > 1
                ? "Бьёт издалека: ставь позади, за спинами тех, кто бьёт вблизи."
                : "Бьёт вблизи: ставь впереди, на пути врага.";
            return $"{role} В резерве {_deployment.ReserveOf(kind)}.";
        }
    }
}
