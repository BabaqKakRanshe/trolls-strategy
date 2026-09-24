using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrollStrategy.Content;
using TrollStrategy.Application;
using TrollStrategy.Presentation;

namespace TrollStrategy.UI
{
    public class HudPresenter : MonoBehaviour
    {
        [SerializeField] private ResourceBarView _resourceBar;
        [SerializeField] private UnitRosterView _unitRoster;
        [SerializeField] private ShopDockView _shopDock;
        [SerializeField] private CommandDockView _commandDock;
        [SerializeField] private InspectCardView _inspectCard;
        [SerializeField] private StatusMessageView _statusView;

        [SerializeField] private Image[] _objectiveRows;
        [SerializeField] private Image[] _shopCards;

        public void SetupObjectives(Image mine, Image hire, Image work, Image mineCard, Image goblinCard, Image trollCard)
        {
            _objectiveRows = new[] { mine, hire, work };
            _shopCards = new[] { mineCard, goblinCard, trollCard };
        }

        private void RefreshObjectives(GameSnapshot snapshot)
        {
            bool mineBuilt = false, staffed = false;
            foreach (var building in snapshot.Buildings)
                if (building.Kind == BuildingKind.Mine) { mineBuilt = true; staffed |= building.WorkerCount > 0; }
            int step = !mineBuilt ? 0 : snapshot.Units.Count == 0 ? 1 : !staffed ? 2 : 3;
            if (_objectiveRows == null || _shopCards == null) return;
            for (int i = 0; i < _objectiveRows.Length; i++)
            {
                var row = _objectiveRows[i];
                if (row == null) continue;
                row.color = i == step ? ColonyPalette.Forest : ColonyPalette.Night;
                var marker = row.transform.Find("Index").GetComponent<TextMeshProUGUI>();
                marker.text = i < step ? "✓" : (i + 1).ToString("00");
                marker.color = i <= step ? ColonyPalette.Gold : ColonyPalette.MutedText;
            }
            for (int i = 0; i < _shopCards.Length; i++)
                if (_shopCards[i] != null)
                    _shopCards[i].color = (step == 0 && i == 0) || (step == 1 && i > 0)
                        ? ColonyPalette.Forest : ColonyPalette.Night;
        }

        private GameSession _session;
        private InteractionController _interaction;

        public void Init(
            GameSession session,
            InteractionController interaction,
            ResourceBarView resourceBar,
            UnitRosterView unitRoster,
            ShopDockView shopDock,
            CommandDockView commandDock,
            InspectCardView inspectCard,
            StatusMessageView statusView)
        {
            _session = session;
            _interaction = interaction;
            _resourceBar = resourceBar;
            _unitRoster = unitRoster;
            _shopDock = shopDock;
            _commandDock = commandDock;
            _inspectCard = inspectCard;
            _statusView = statusView;

            if (_shopDock != null) _shopDock.Bind(_session, _interaction);
            if (_unitRoster != null) _unitRoster.BindInteraction(_interaction);
            if (_commandDock != null) _commandDock.Bind(_session, _interaction);
            if (_inspectCard != null) _inspectCard.Bind(_session, _interaction);
            if (_statusView != null) _statusView.Bind(_interaction);

            _session.OnSnapshotChanged += OnSnapshotChanged;
            _interaction.OnInteractionChanged += OnInteractionChanged;

            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
            if (_interaction != null) _interaction.OnInteractionChanged -= OnInteractionChanged;
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            RefreshAll();
        }

        private void OnInteractionChanged()
        {
            RefreshAll();
        }

        public void RefreshAll()
        {
            if (_session == null || _interaction == null) return;

            var snap = _session.CurrentSnapshot;
            var selIds = _interaction.SelectedIds;
            RefreshObjectives(snap);

            if (_resourceBar != null)
                _resourceBar.UpdateView(snap);

            if (_unitRoster != null)
                _unitRoster.UpdateRoster(snap.Units, selIds);

            if (_shopDock != null)
                _shopDock.UpdateView(snap);

            if (_commandDock != null)
                _commandDock.UpdateView(selIds.Count, _interaction.CommandsOpen, _interaction.StackQuantity);

            if (_inspectCard != null)
                _inspectCard.UpdateView(snap, _interaction.InspectedBuildingId, _interaction.InspectedUnitId);

            if (_statusView != null)
                _statusView.UpdateView(_interaction.Mode, _interaction.Message);
        }
    }
}
