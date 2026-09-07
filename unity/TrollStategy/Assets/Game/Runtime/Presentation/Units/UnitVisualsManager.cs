using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;

namespace TrollStrategy.Presentation.Units
{
    public class UnitVisualsManager : MonoBehaviour
    {
        [SerializeField] private UnitView _unitPrefab;
        [SerializeField] private Transform _container;
        [SerializeField] private GameContentCatalog _catalog;

        private GameSession _session;
        private InteractionController _interaction;
        private readonly Dictionary<string, UnitView> _views = new();

        public IReadOnlyDictionary<string, UnitView> Views => _views;

        public void Init(GameSession session, InteractionController interaction, GameContentCatalog catalog, UnitView prefab)
        {
            _session = session;
            _interaction = interaction;
            _catalog = catalog;
            _unitPrefab = prefab;

            if (_container == null)
            {
                var go = new GameObject("UnitsContainer");
                go.transform.SetParent(transform);
                _container = go.transform;
            }

            _session.OnSnapshotChanged += OnSnapshotChanged;
            _interaction.OnInteractionChanged += OnInteractionChanged;
            SyncUnits(_session.CurrentSnapshot);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
            if (_interaction != null) _interaction.OnInteractionChanged -= OnInteractionChanged;
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            SyncUnits(snapshot);
        }

        private void OnInteractionChanged()
        {
            SyncSelection();
        }

        private void SyncUnits(GameSnapshot snapshot)
        {
            var selected = _interaction != null ? new HashSet<string>(_interaction.SelectedIds) : new HashSet<string>();
            var activeIds = new HashSet<string>();

            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                var uSnap = snapshot.Units[i];
                activeIds.Add(uSnap.Id);

                if (!_views.TryGetValue(uSnap.Id, out var view))
                {
                    view = Instantiate(_unitPrefab, _container);
                    var def = _catalog.GetUnit(uSnap.UnitKind);
                    view.Setup(uSnap, def, OnUnitClicked);
                    _views.Add(uSnap.Id, view);
                }
                else
                {
                    view.UpdateVisuals(uSnap, selected.Contains(uSnap.Id));
                }
            }

            var toRemove = new List<string>();
            foreach (var kvp in _views)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    Destroy(kvp.Value.gameObject);
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
                _views.Remove(toRemove[i]);
        }

        private void SyncSelection()
        {
            var selected = _interaction != null ? new HashSet<string>(_interaction.SelectedIds) : new HashSet<string>();
            var snap = _session.CurrentSnapshot;

            for (int i = 0; i < snap.Units.Count; i++)
            {
                var uSnap = snap.Units[i];
                if (_views.TryGetValue(uSnap.Id, out var view))
                {
                    view.UpdateVisuals(uSnap, selected.Contains(uSnap.Id));
                }
            }
        }

        private void OnUnitClicked(string unitId, bool additive)
        {
            _interaction?.ClickUnit(unitId, additive);
        }
    }
}
