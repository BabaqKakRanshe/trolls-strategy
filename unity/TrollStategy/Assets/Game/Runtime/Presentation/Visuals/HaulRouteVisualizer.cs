using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Visuals
{
    public class HaulRouteVisualizer : MonoBehaviour
    {
        [SerializeField] private TilemapWorldView _worldView;

        private GameSession _session;
        private bool _guidesVisible = true;
        private GroundGridView _grid;
        private GameObject _routesContainer;
        private readonly List<LineRenderer> _lines = new();
        private readonly List<LineRenderer> _arrows = new();
        private static Texture2D s_dashedTexture;

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

        private static Texture2D GetDashedTexture()
        {
            if (s_dashedTexture != null) return s_dashedTexture;
            s_dashedTexture = new Texture2D(16, 2, TextureFormat.RGBA32, false);
            s_dashedTexture.filterMode = FilterMode.Point;
            s_dashedTexture.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    s_dashedTexture.SetPixel(x, y, x < 10 ? Color.white : Color.clear);
                }
            }
            s_dashedTexture.Apply();
            return s_dashedTexture;
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
                {
                    routes.Add((u.Assignment.SourceId, u.Assignment.DestinationId));
                }
            }

            while (_lines.Count < routes.Count)
            {
                var go = new GameObject($"HaulRouteLine_{_lines.Count}");
                go.transform.SetParent(_routesContainer.transform, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.07f;
                lr.endWidth = 0.07f;
                lr.useWorldSpace = true;
                lr.textureMode = LineTextureMode.Tile;

                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.mainTexture = GetDashedTexture();
                lr.material = mat;

                Color routeColor = ColonyPalette.WithAlpha(ColonyPalette.Gold, 0.85f);
                lr.startColor = routeColor;
                lr.endColor = routeColor;
                lr.sortingOrder = 6;
                _lines.Add(lr);

                var arrowGo = new GameObject($"HaulRouteArrow_{_arrows.Count}");
                arrowGo.transform.SetParent(go.transform, false);
                var aLr = arrowGo.AddComponent<LineRenderer>();
                aLr.positionCount = 3;
                aLr.startWidth = 0.08f;
                aLr.endWidth = 0.08f;
                aLr.useWorldSpace = true;
                aLr.material = new Material(Shader.Find("Sprites/Default"));
                aLr.startColor = routeColor;
                aLr.endColor = routeColor;
                aLr.sortingOrder = 7;
                _arrows.Add(aLr);
            }

            int index = 0;
            foreach (var (srcId, dstId) in routes)
            {
                BuildingSnapshot srcB = null;
                BuildingSnapshot dstB = null;

                for (int b = 0; b < snapshot.Buildings.Count; b++)
                {
                    if (snapshot.Buildings[b].Id == srcId) srcB = snapshot.Buildings[b];
                    if (snapshot.Buildings[b].Id == dstId) dstB = snapshot.Buildings[b];
                }

                if (srcB != null && dstB != null)
                {
                    var p1 = _worldView.BuildingCenterWorld(srcB.Cell, srcB.Width, srcB.Height);
                    var p2 = _worldView.BuildingCenterWorld(dstB.Cell, dstB.Width, dstB.Height);

                    _lines[index].gameObject.SetActive(true);
                    _lines[index].SetPosition(0, p1 + _worldView.GroundOffset(0.22f));
                    _lines[index].SetPosition(1, p2 + _worldView.GroundOffset(0.22f));

                    Vector3 dir = (p2 - p1).normalized;
                    Vector3 normal = Vector3.Cross(dir, _worldView.GroundRotation * Vector3.back);
                    Vector3 arrowTip = p2 - dir * 0.4f;
                    Vector3 left = arrowTip - dir * 0.25f + normal * 0.15f;
                    Vector3 right = arrowTip - dir * 0.25f - normal * 0.15f;

                    _arrows[index].gameObject.SetActive(true);
                    _arrows[index].SetPosition(0, left + _worldView.GroundOffset(0.22f));
                    _arrows[index].SetPosition(1, arrowTip + _worldView.GroundOffset(0.22f));
                    _arrows[index].SetPosition(2, right + _worldView.GroundOffset(0.22f));

                    index++;
                }
            }

            for (int i = index; i < _lines.Count; i++)
            {
                _lines[i].gameObject.SetActive(false);
                if (i < _arrows.Count) _arrows[i].gameObject.SetActive(false);
            }

            if (_routesContainer != null)
                _routesContainer.SetActive(_guidesVisible);
        }
    }
}
