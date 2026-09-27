using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Buildings
{
    public class BuildingVisualsManager : MonoBehaviour
    {
        [SerializeField] private Transform _container;
        [SerializeField] private TilemapWorldView _worldView;
        [SerializeField] private GameContentCatalog _catalog;

        private GameSession _session;
        private InteractionController _interaction;
        private GameSnapshot _previousSnapshot;
        private readonly Dictionary<string, BuildingView> _views = new();
        private readonly Dictionary<string, BuildingView> _sceneViews = new();
        public IReadOnlyDictionary<string, BuildingView> Views => _views;

        public void SetContainer(Transform container) => _container = container;

        /// <param name="sceneViews">Scene-placed starting buildings by id; they become those buildings' views.</param>
        public void Init(GameSession session, InteractionController interaction, TilemapWorldView worldView,
            GameContentCatalog catalog, IReadOnlyDictionary<string, BuildingView> sceneViews = null)
        {
            _session = session;
            _interaction = interaction;
            _worldView = worldView;
            _catalog = catalog;

            if (_container == null)
            {
                var go = new GameObject("BuildingsContainer");
                go.transform.SetParent(transform);
                _container = go.transform;
            }

            if (sceneViews != null)
                foreach (var pair in sceneViews)
                    _sceneViews.Add(pair.Key, pair.Value);

            _session.OnSnapshotChanged += OnSnapshotChanged;
            _interaction.OnInteractionChanged += OnInteractionChanged;
            SyncBuildings(_session.CurrentSnapshot);
            _previousSnapshot = _session.CurrentSnapshot;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
            if (_interaction != null) _interaction.OnInteractionChanged -= OnInteractionChanged;
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            SyncBuildings(snapshot);
            ShowSales(_previousSnapshot, snapshot);
            ShowConstruction(_previousSnapshot, snapshot);
            _previousSnapshot = snapshot;
        }

        /// <summary>New buildings spring up, upgrades pulse gold, moved ones land; all with a ring on the ground.</summary>
        private void ShowConstruction(GameSnapshot previous, GameSnapshot current)
        {
            if (previous == null) return;
            var before = new Dictionary<string, BuildingSnapshot>();
            foreach (var building in previous.Buildings) before[building.Id] = building;
            foreach (var building in current.Buildings)
            {
                if (!_views.TryGetValue(building.Id, out var view) || view == null || view.Model == null) continue;
                var model = view.Model.transform;
                float radius = Mathf.Max(building.Width, building.Height) * .6f * _worldView.CellSize;
                if (!before.TryGetValue(building.Id, out var old))
                {
                    Juice.PopIn(model, .38f);
                    Ring(view.transform.position, new Color(.95f, .9f, .75f, .9f), radius);
                }
                else if (building.Level > old.Level)
                {
                    Juice.Punch(model, .2f, .5f);
                    Ring(view.transform.position, new Color(1f, .84f, .35f, 1f), radius * 1.2f);
                }
                else if (building.Cell != old.Cell)
                {
                    Juice.Punch(model, .12f, .35f);
                    Ring(view.transform.position, new Color(.95f, .9f, .75f, .9f), radius);
                }
            }
        }

        private void Ring(Vector3 position, Color color, float radius) =>
            WorldPing.Show(position + _worldView.GroundOffset(.03f), _worldView.GroundRotation, color, radius, .5f);

        public Sprite GetOutgoingProductSprite(string buildingId) =>
            buildingId != null && _views.TryGetValue(buildingId, out var view)
                ? view.OutgoingProductSprite : null;

        // Icon of a carried good; the source prefab's product sprite stands in when the catalog has none.
        public Sprite GetCargoSprite(ResourceKind resource, string sourceId) =>
            _catalog?.TryGetResource(resource)?.Icon ?? GetOutgoingProductSprite(sourceId);

        private void ShowSales(GameSnapshot previous, GameSnapshot current)
        {
            if (previous == null || current.SoldGoods <= previous.SoldGoods) return;
            var oldUnits = new Dictionary<string, UnitSnapshot>();
            foreach (var unit in previous.Units) oldUnits[unit.Id] = unit;

            foreach (var unit in current.Units)
            {
                if (!oldUnits.TryGetValue(unit.Id, out var before)) continue;
                var oldAssignment = before.Assignment;
                var assignment = unit.Assignment;
                if (oldAssignment.Kind != TrollStrategy.Domain.AssignmentKind.Haul ||
                    assignment.Kind != TrollStrategy.Domain.AssignmentKind.Haul ||
                    oldAssignment.Carried <= 0 || assignment.Carried != 0 ||
                    oldAssignment.DestinationId != assignment.DestinationId) continue;
                if (!_views.TryGetValue(assignment.DestinationId, out var market)) continue;
                var building = current.Buildings;
                BuildingSnapshot marketSnapshot = null;
                foreach (var item in building)
                    if (item.Id == assignment.DestinationId && item.Kind == BuildingKind.Market) marketSnapshot = item;
                if (marketSnapshot == null) continue;

                var resource = oldAssignment.CarriedResource;
                market.PlaySale(GetCargoSprite(resource, oldAssignment.SourceId),
                    oldAssignment.Carried * TrollStrategy.Domain.ColonySimulation.SalePrice(_catalog, resource, marketSnapshot.Level));
            }
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
                    var def = _catalog.GetBuilding(bSnap.Kind);
                    if (_sceneViews.Remove(bSnap.Id, out view))
                        view.transform.SetParent(_container, true);
                    else
                        view = Instantiate(ContentPrefabs.Building(def), _container);
                    view.name = $"Building_{bSnap.Id}";
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
                    // demolished: it shrinks away with a ring instead of vanishing between frames
                    if (kvp.Value != null)
                    {
                        Ring(kvp.Value.transform.position, new Color(.8f, .75f, .65f, .9f), .9f * _worldView.CellSize);
                        Juice.ShrinkAndDestroy(kvp.Value.gameObject, .28f);
                    }
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
