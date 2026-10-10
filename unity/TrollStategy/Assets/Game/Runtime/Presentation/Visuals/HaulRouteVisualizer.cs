using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Visuals
{
    /// <summary>
    /// The haul routes as trodden trails on the lawn: sand-coloured stepping stones from the edge of the plot the goods come
    /// from to the edge of the plot they go to, a white step running down each toward the destination.
    /// </summary>
    public class HaulRouteVisualizer : MonoBehaviour
    {
        // a stone every StoneStep m, StoneSize m long, swaying StoneSway m to either side, StoneTilt degrees off the
        // trail at most; the trail starts PlotMargin m out of each plot and lies TrailLift m over the map plane
        private const float StoneStep = .36f;
        private const float StoneSize = .2f;
        private const float StoneSway = .07f;
        private const float StoneTilt = 15f;
        private const float PlotMargin = .15f;
        private const float TrailLift = .22f;
        // one stone in LitEvery is lit, the lit one moving a stone on every LitSeconds
        private const int LitEvery = 8;
        private const float LitSeconds = .45f;
        private const int SortingOrder = 6;

        [SerializeField] private TilemapWorldView _worldView;

        private GameSession _session;
        private bool _guidesVisible = true;
        private GroundGridView _grid;
        private GameObject _routesContainer;
        private readonly List<SpriteRenderer> _stones = new();
        // each laid stone's place along its trail, from the source
        private readonly List<int> _stoneSteps = new();
        // each trail on the map plane, from the source plot's edge to the destination's, world
        private readonly List<(Vector3 From, Vector3 To)> _trails = new();
        private readonly List<(Vector3 From, Vector3 To)> _nextTrails = new();
        private int _laid;
        private int _lit = -1;

        public bool GuidesVisible => _guidesVisible;

        public void Init(GameSession session, InteractionController interaction, TilemapWorldView worldView, Camera camera)
        {
            _session = session;
            _worldView = worldView;

            EnsureGrid(interaction, camera);

            if (_routesContainer == null)
            {
                _routesContainer = new GameObject("RoutesContainer");
                _routesContainer.transform.SetParent(transform, false);
            }

            _session.OnSnapshotChanged += OnSnapshotChanged;
            OnSnapshotChanged(_session.CurrentSnapshot);
        }

        private void OnDestroy()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
        }

        private void Update()
        {
            if (!_guidesVisible || _laid == 0) return;
            int lit = Mathf.FloorToInt(Time.time / LitSeconds) % LitEvery;
            if (lit != _lit) Light(lit);
        }

        public void ToggleGuides()
        {
            SetGuidesVisible(!_guidesVisible);
        }

        public void SetGuidesVisible(bool visible)
        {
            _guidesVisible = visible;
            if (_grid != null)
                _grid.gameObject.SetActive(_guidesVisible);
            if (_routesContainer != null)
                _routesContainer.SetActive(_guidesVisible);
        }

        private void EnsureGrid(InteractionController interaction, Camera camera)
        {
            if (_grid == null)
            {
                _grid = new GameObject("GroundGrid").AddComponent<GroundGridView>();
                _grid.transform.SetParent(_worldView.Grid.transform, false);
            }
            _grid.Init(_session, interaction, _worldView, camera);
            _grid.gameObject.SetActive(_guidesVisible);
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            if (snapshot == null || _worldView == null) return;

            var routes = new HashSet<(string source, string dest)>();
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                var u = snapshot.Units[i];
                if (u.Assignment != null && u.Assignment.Kind == AssignmentKind.Haul)
                    routes.Add((u.Assignment.SourceId, u.Assignment.DestinationId));
            }

            _nextTrails.Clear();
            foreach (var (srcId, dstId) in routes)
            {
                BuildingSnapshot source = null;
                BuildingSnapshot destination = null;
                for (int b = 0; b < snapshot.Buildings.Count; b++)
                {
                    if (snapshot.Buildings[b].Id == srcId) source = snapshot.Buildings[b];
                    if (snapshot.Buildings[b].Id == dstId) destination = snapshot.Buildings[b];
                }
                if (source == null || destination == null) continue;

                var start = _worldView.BuildingCenterWorld(source.Cell, source.Width, source.Height);
                var end = _worldView.BuildingCenterWorld(destination.Cell, destination.Width, destination.Height);
                float from = PlotExit(source, start, end);
                float to = 1f - PlotExit(destination, end, start);
                if (to - from <= 0f) continue;
                _nextTrails.Add((Vector3.LerpUnclamped(start, end, from), Vector3.LerpUnclamped(start, end, to)));
            }

            if (!SameTrails()) Lay();

            if (_routesContainer != null)
                _routesContainer.SetActive(_guidesVisible);
        }

        private bool SameTrails()
        {
            if (_nextTrails.Count != _trails.Count) return false;
            for (int i = 0; i < _trails.Count; i++)
                if ((_trails[i].From - _nextTrails[i].From).sqrMagnitude > 1e-6f ||
                    (_trails[i].To - _nextTrails[i].To).sqrMagnitude > 1e-6f)
                    return false;
            return true;
        }

        private void Lay()
        {
            _trails.Clear();
            _trails.AddRange(_nextTrails);
            _laid = 0;
            _stoneSteps.Clear();
            foreach (var (from, to) in _trails)
            {
                var mapFrom = _worldView.WorldToMap(from);
                var mapTo = _worldView.WorldToMap(to);
                var along = mapTo - mapFrom;
                float length = along.magnitude;
                float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
                var side = new Vector3(-along.y, along.x, 0f) / Mathf.Max(length, 1e-4f);
                int step = 0;
                for (float s = StoneStep * .3f; s < length - StoneStep * .3f; s += StoneStep, step++)
                {
                    // repeatable wobble: every stone of a trail sits where it sat before
                    float sway = step % 2 == 0 ? StoneSway : -StoneSway;
                    float size = StoneSize * (.9f + .2f * Mathf.PerlinNoise(step * .7f, 3.1f));
                    float tilt = (step * 37 % 30) / 15f * StoneTilt - StoneTilt;
                    var mapPoint = mapFrom + along * (s / length) + side * sway;
                    var stone = Stone(_laid++);
                    stone.transform.SetPositionAndRotation(
                        _worldView.MapToWorld(mapPoint) + _worldView.GroundOffset(TrailLift),
                        _worldView.GroundRotation * Quaternion.Euler(0f, 0f, angle + tilt));
                    stone.transform.localScale = new Vector3(size, size, 1f);
                    stone.gameObject.SetActive(true);
                    _stoneSteps.Add(step);
                }
            }
            for (int i = _laid; i < _stones.Count; i++) _stones[i].gameObject.SetActive(false);
            Light(Mathf.Max(0, _lit));
        }

        private SpriteRenderer Stone(int index)
        {
            while (_stones.Count <= index)
            {
                var go = new GameObject($"TrailStone_{_stones.Count}");
                go.transform.SetParent(_routesContainer.transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = FeelSprites.TrailStone;
                renderer.sortingOrder = SortingOrder;
                _stones.Add(renderer);
            }
            return _stones[index];
        }

        private void Light(int lit)
        {
            _lit = lit;
            var sand = ColonyPalette.WithAlpha(ColonyPalette.Cream, .85f);
            for (int i = 0; i < _laid; i++)
                _stones[i].color = _stoneSteps[i] % LitEvery == lit ? Color.white : sand;
        }

        // the share of a→b that lies over the building's plot (and PlotMargin of lawn round it)
        private float PlotExit(BuildingSnapshot building, Vector3 a, Vector3 b)
        {
            var d = _worldView.WorldToMap(b) - _worldView.WorldToMap(a);
            float cell = _worldView.CellSize;
            float tx = Mathf.Abs(d.x) > 1e-4f ? (building.Width * cell * .5f + PlotMargin) / Mathf.Abs(d.x) : float.MaxValue;
            float ty = Mathf.Abs(d.y) > 1e-4f ? (building.Height * cell * .5f + PlotMargin) / Mathf.Abs(d.y) : float.MaxValue;
            return Mathf.Clamp01(Mathf.Min(tx, ty));
        }
    }
}
