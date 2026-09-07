using UnityEngine;
using TrollStrategy.Application;

namespace TrollStrategy.UI
{
    public class HudPresenter : MonoBehaviour
    {
        [SerializeField] private UnitRosterView _rosterView;
        [SerializeField] private ShopDockView _shopDock;
        [SerializeField] private CommandDockView _commandDock;

        private GameSession _session;
        private InteractionController _interaction;

        public void Init(GameSession session, InteractionController interaction, UnitRosterView rosterView, ShopDockView shopDock, CommandDockView commandDock)
        {
            _session = session;
            _interaction = interaction;
            _rosterView = rosterView;
            _shopDock = shopDock;
            _commandDock = commandDock;

            if (_rosterView != null) _rosterView.BindInteraction(_interaction);
            if (_shopDock != null) _shopDock.BindInteraction(_interaction);
            if (_commandDock != null) _commandDock.BindInteraction(_interaction);

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

            if (_rosterView != null)
                _rosterView.UpdateRoster(snap.Units, selIds);

            if (_shopDock != null)
                _shopDock.UpdateView(snap, _interaction.Mode, _interaction.Message);

            if (_commandDock != null)
                _commandDock.UpdateState(selIds.Count, _interaction.Mode, snap);
        }
    }
}

