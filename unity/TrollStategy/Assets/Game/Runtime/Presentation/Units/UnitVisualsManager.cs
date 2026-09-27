using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Buildings;

namespace TrollStrategy.Presentation.Units
{
    public class UnitVisualsManager : MonoBehaviour
    {
        [SerializeField] private Transform _container;
        [SerializeField] private GameContentCatalog _catalog;

        private GameSession _session;
        private InteractionController _interaction;
        private TilemapWorldView _worldView;
        private BuildingVisualsManager _buildingManager;
        private readonly Dictionary<string, UnitView> _views = new();
        private readonly HashSet<string> _highlighted = new();
        private bool _synced;

        public IReadOnlyDictionary<string, UnitView> Views => _views;

        public void Init(GameSession session, InteractionController interaction, GameContentCatalog catalog, TilemapWorldView worldView = null, BuildingVisualsManager buildingManager = null)
        {
            _session = session;
            _interaction = interaction;
            _catalog = catalog;
            _worldView = worldView;
            _buildingManager = buildingManager;

            if (_container == null)
            {
                var go = new GameObject("UnitsContainer");
                go.transform.SetParent(transform);
                _container = go.transform;
            }

            _session.OnSnapshotChanged += OnSnapshotChanged;
            _interaction.OnInteractionChanged += OnInteractionChanged;
            SyncUnits(_session.CurrentSnapshot);
            _synced = true;
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
                    var def = _catalog.GetUnit(uSnap.UnitKind);
                    view = Instantiate(ContentPrefabs.Unit(def), _container);
                    if (_worldView != null) view.transform.rotation = _worldView.GroundRotation;
                    view.Setup(uSnap, def, OnUnitClicked, _worldView);
                    bool isSelected = selected.Contains(uSnap.Id) ||
                                      (_interaction != null && uSnap.Id == _interaction.InspectedUnitId);
                    view.UpdateVisuals(uSnap, isSelected, CargoSprite(uSnap));
                    _views.Add(uSnap.Id, view);
                    // a hired creature springs up where it was placed
                    if (_synced) Juice.PopIn(view.transform, .35f);
                }
                else
                {
                    bool isSelected = selected.Contains(uSnap.Id) || (_interaction != null && uSnap.Id == _interaction.InspectedUnitId);
                    view.UpdateVisuals(uSnap, isSelected, CargoSprite(uSnap));
                }
            }

            var toRemove = new List<string>();
            foreach (var kvp in _views)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    if (kvp.Value != null) Juice.ShrinkAndDestroy(kvp.Value.gameObject, .22f);
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                _views.Remove(toRemove[i]);
                _highlighted.Remove(toRemove[i]);
            }
            AcknowledgeSelection();
        }

        /// <summary>A unit that just became selected springs, with one click for the whole pick.</summary>
        private void AcknowledgeSelection()
        {
            if (_interaction == null) return;
            var now = new HashSet<string>(_interaction.SelectedIds);
            if (_interaction.InspectedUnitId != null) now.Add(_interaction.InspectedUnitId);
            bool any = false;
            foreach (var id in now)
            {
                if (_highlighted.Contains(id) || !_views.TryGetValue(id, out var view) || view == null) continue;
                if (_synced) Juice.Punch(view.transform, .18f, .3f);
                any = true;
            }
            _highlighted.Clear();
            _highlighted.UnionWith(now);
            if (any && _synced) GameAudio.Play(Sfx.Select);
        }

        private void SyncSelection()
        {
            var selected = _interaction != null ? new HashSet<string>(_interaction.SelectedIds) : new HashSet<string>();
            string inspectedId = _interaction != null ? _interaction.InspectedUnitId : null;
            var snap = _session.CurrentSnapshot;

            for (int i = 0; i < snap.Units.Count; i++)
            {
                var uSnap = snap.Units[i];
                if (_views.TryGetValue(uSnap.Id, out var view))
                {
                    bool isSelected = selected.Contains(uSnap.Id) || uSnap.Id == inspectedId;
                    view.UpdateVisuals(uSnap, isSelected, CargoSprite(uSnap));
                }
            }
            AcknowledgeSelection();
        }

        private void OnUnitClicked(string unitId, bool additive)
        {
            _interaction?.ClickUnit(unitId, additive);
        }

        private Sprite CargoSprite(UnitSnapshot unit)
        {
            var assignment = unit.Assignment;
            return assignment.Kind == TrollStrategy.Domain.AssignmentKind.Haul
                ? _buildingManager?.GetCargoSprite(assignment.CarriedResource, assignment.SourceId) : null;
        }
    }
}
