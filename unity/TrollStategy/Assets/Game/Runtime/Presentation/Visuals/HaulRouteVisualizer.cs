using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private GameObject _gridContainer;
        private GameObject _routesContainer;
        private readonly List<LineRenderer> _lines = new();
        private readonly List<LineRenderer> _arrows = new();
        private static Texture2D s_dashedTexture;

        public bool GuidesVisible => _guidesVisible;

        private void Awake()
        {
            EnsureGridLines();
        }

        public void Init(GameSession session, TilemapWorldView worldView)
        {
            _session = session;
            _worldView = worldView;

            EnsureGridLines();

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
            if (_gridContainer != null)
                _gridContainer.SetActive(_guidesVisible);
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

        private void EnsureGridLines()
        {
            if (_gridContainer != null) return;

            _gridContainer = new GameObject("GridLines");
            _gridContainer.transform.SetParent(transform, false);

            int width = 14;
            int height = 14;
            float thickness = 0.045f;
            float halfT = thickness * 0.5f;

            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();

            // Crisp lime-tinted grid matching Phaser prototype (0xe2f4b0)
            Color gridColor = new Color(0.886f, 0.957f, 0.690f, 0.42f);

            // Vertical lines (x = 0 to 14)
            for (int x = 0; x <= width; x++)
            {
                int baseIdx = vertices.Count;
                vertices.Add(new Vector3(x - halfT, 0f, 0f));
                vertices.Add(new Vector3(x + halfT, 0f, 0f));
                vertices.Add(new Vector3(x + halfT, height, 0f));
                vertices.Add(new Vector3(x - halfT, height, 0f));

                for (int i = 0; i < 4; i++) colors.Add(gridColor);

                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 3);
            }

            // Horizontal lines (y = 0 to 14)
            for (int y = 0; y <= height; y++)
            {
                int baseIdx = vertices.Count;
                vertices.Add(new Vector3(0f, y - halfT, 0f));
                vertices.Add(new Vector3(width, y - halfT, 0f));
                vertices.Add(new Vector3(width, y + halfT, 0f));
                vertices.Add(new Vector3(0f, y + halfT, 0f));

                for (int i = 0; i < 4; i++) colors.Add(gridColor);

                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 3);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();

            var mf = _gridContainer.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = _gridContainer.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Sprites/Default"));
            mr.sortingOrder = 4;

            _gridContainer.SetActive(_guidesVisible);
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

                Color routeColor = new Color(1f, 0.84f, 0.44f, 0.85f); // #ffd66f
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
                    _lines[index].SetPosition(0, p1);
                    _lines[index].SetPosition(1, p2);

                    Vector3 dir = (p2 - p1).normalized;
                    Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
                    Vector3 arrowTip = p2 - dir * 0.4f;
                    Vector3 left = arrowTip - dir * 0.25f + normal * 0.15f;
                    Vector3 right = arrowTip - dir * 0.25f - normal * 0.15f;

                    _arrows[index].gameObject.SetActive(true);
                    _arrows[index].SetPosition(0, left);
                    _arrows[index].SetPosition(1, arrowTip);
                    _arrows[index].SetPosition(2, right);

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
