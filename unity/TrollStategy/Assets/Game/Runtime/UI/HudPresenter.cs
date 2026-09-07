using UnityEngine;
using TrollStrategy.Application;

namespace TrollStrategy.UI
{
    public class HudPresenter : MonoBehaviour
    {
        [SerializeField] private ResourceBarView _resourceBar;
        [SerializeField] private ShopDockView _shopDock;
        [SerializeField] private CommandDockView _commandDock;
        [SerializeField] private InspectCardView _inspectCard;
        [SerializeField] private StatusMessageView _statusView;

        private GameSession _session;
        private InteractionController _interaction;

        public void Init(
            GameSession session,
            InteractionController interaction,
            ResourceBarView resourceBar,
            ShopDockView shopDock,
            CommandDockView commandDock,
            InspectCardView inspectCard,
            StatusMessageView statusView)
        {
            _session = session;
            _interaction = interaction;
            _resourceBar = resourceBar;
            _shopDock = shopDock;
            _commandDock = commandDock;
            _inspectCard = inspectCard;
            _statusView = statusView;

            if (_shopDock != null) _shopDock.Bind(_session, _interaction);
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

            if (_resourceBar != null)
                _resourceBar.UpdateView(snap);

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
