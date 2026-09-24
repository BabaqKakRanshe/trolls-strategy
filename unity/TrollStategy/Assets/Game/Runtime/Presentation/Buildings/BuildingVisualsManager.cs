using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Buildings
{
    public class BuildingVisualsManager : MonoBehaviour
    {
        [SerializeField] private BuildingView _buildingPrefab;
        [SerializeField] private Transform _container;
        [SerializeField] private TilemapWorldView _worldView;
        [SerializeField] private GameContentCatalog _catalog;

        private GameSession _session;
        private InteractionController _interaction;
        private readonly Dictionary<string, BuildingView> _views = new();
        public IReadOnlyDictionary<string, BuildingView> Views => _views;

        public void SetContainer(Transform container) => _container = container;

        public void Init(GameSession session, InteractionController interaction, TilemapWorldView worldView, GameContentCatalog catalog, BuildingView prefab)
        {
            _session = session;
            _interaction = interaction;
            _worldView = worldView;
            _catalog = catalog;
            _buildingPrefab = prefab;

            if (_container == null)
            {
                var go = new GameObject("BuildingsContainer");
                go.transform.SetParent(transform);
                _container = go.transform;
            }

            _session.OnSnapshotChanged += OnSnapshotChanged;
            _interaction.OnInteractionChanged += OnInteractionChanged;
            SyncBuildings(_session.CurrentSnapshot);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
            if (_interaction != null) _interaction.OnInteractionChanged -= OnInteractionChanged;
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            SyncBuildings(snapshot);
        }

        private void OnInteractionChanged()
        {
            SyncHighlights();
        }

        private void SyncBuildings(GameSnapshot snapshot)
        {
            var targets = _interaction != null ? new HashSet<string>(_interaction.GetTargetBuildingIds()) : new HashSet<string>();
            var activeIds = new HashSet<string>();

            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                var bSnap = snapshot.Buildings[i];
                activeIds.Add(bSnap.Id);

                var targetPos = _worldView.BuildingCenterWorld(bSnap.Cell, bSnap.Width, bSnap.Height);

                if (!_views.TryGetValue(bSnap.Id, out var view))
                {
                    Transform existing = _container.Find($"Building_{bSnap.Id}");
                    if (existing != null && existing.TryGetComponent<BuildingView>(out var ev))
                    {
                        view = ev;
                    }
                    else
                    {
                        view = Instantiate(_buildingPrefab, _container);
                        view.name = $"Building_{bSnap.Id}";
                    }
                    var def = _catalog.GetBuilding(bSnap.Kind);
                    view.transform.position = targetPos;
                    view.transform.rotation = _worldView.GroundRotation;
                    view.Setup(bSnap, def.Sprite, OnBuildingClicked, _worldView);
                    _views.Add(bSnap.Id, view);
                }
                else
                {
                    bool isHighlighted = targets.Contains(bSnap.Id) || (_interaction != null && bSnap.Id == _interaction.InspectedBuildingId);
                    view.transform.position = targetPos;
                    view.transform.rotation = _worldView.GroundRotation;
                    view.UpdateVisuals(bSnap, isHighlighted);
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

        private void SyncHighlights()
        {
            var targets = _interaction != null ? new HashSet<string>(_interaction.GetTargetBuildingIds()) : new HashSet<string>();
            string inspectedId = _interaction != null ? _interaction.InspectedBuildingId : null;
            var snap = _session.CurrentSnapshot;

            for (int i = 0; i < snap.Buildings.Count; i++)
            {
                var bSnap = snap.Buildings[i];
                if (_views.TryGetValue(bSnap.Id, out var view))
                {
                    bool isHighlighted = targets.Contains(bSnap.Id) || bSnap.Id == inspectedId;
                    view.UpdateVisuals(bSnap, isHighlighted);
                }
            }
        }

        private void OnBuildingClicked(string buildingId)
        {
            _interaction?.ChooseBuilding(buildingId);
        }
    }
}
