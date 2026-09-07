using System.Collections.Generic;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Map;

namespace TrollStrategy.Presentation.Visuals
{
    public class HaulRouteVisualizer : MonoBehaviour
    {
        [SerializeField] private LineRenderer _routeRendererPrefab;
        [SerializeField] private TilemapWorldView _worldView;

        private GameSession _session;
        private readonly List<LineRenderer> _lines = new();

        public void Init(GameSession session, TilemapWorldView worldView)
        {
            _session = session;
            _worldView = worldView;
            _session.OnSnapshotChanged += OnSnapshotChanged;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
        }

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
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
                go.transform.SetParent(transform);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.08f;
                lr.endWidth = 0.08f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = new Color(0.4f, 0.9f, 1f, 0.4f);
                lr.endColor = new Color(0.4f, 0.9f, 1f, 0.4f);
                lr.sortingOrder = 5;
                _lines.Add(lr);
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
                    index++;
                }
            }

            for (int i = index; i < _lines.Count; i++)
                _lines[i].gameObject.SetActive(false);
        }
    }
}
