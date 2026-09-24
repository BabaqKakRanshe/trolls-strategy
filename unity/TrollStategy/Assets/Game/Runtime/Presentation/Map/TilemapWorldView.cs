using UnityEngine;
using UnityEngine.Tilemaps;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Presentation.Map
{
    public class TilemapWorldView : MonoBehaviour
    {
        [SerializeField] private Grid _grid;
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField] private Tilemap _overlayTilemap;
        [SerializeField] private EconomyConfig _economy;

        public Grid Grid => _grid;
        public Tilemap GroundTilemap => _groundTilemap;
        public Tilemap OverlayTilemap => _overlayTilemap;
        public int GridWidth => _economy != null ? _economy.GridWidth : 0;
        public int GridHeight => _economy != null ? _economy.GridHeight : 0;
        public float CellSize => _economy != null ? _economy.CellSize : 1f;
        public Quaternion GroundRotation => _grid != null ? _grid.transform.rotation : Quaternion.identity;

        public Vector3 MapToWorld(Vector3 mapPosition) =>
            _grid != null ? _grid.transform.TransformPoint(mapPosition) : mapPosition;

        public Vector3 WorldToMap(Vector3 worldPosition) =>
            _grid != null ? _grid.transform.InverseTransformPoint(worldPosition) : worldPosition;

        public Vector3 GroundOffset(float height) => GroundRotation * (Vector3.back * height);

        public void Init(Grid grid, Tilemap ground, Tilemap overlay, EconomyConfig economy)
        {
            _grid = grid;
            _groundTilemap = ground;
            _overlayTilemap = overlay;
            _economy = economy;
        }

        public Vector3 CellToWorld(Cell cell)
        {
            return MapToWorld(new Vector3(cell.X * CellSize, cell.Y * CellSize, 0f));
        }

        public Vector3 BuildingCenterWorld(Cell cell, int width, int height)
        {
            return MapToWorld(new Vector3((cell.X + width * 0.5f) * CellSize, (cell.Y + height * 0.5f) * CellSize, 0f));
        }

        public Cell WorldToCell(Vector3 worldPos)
        {
            var mapPosition = WorldToMap(worldPos);
            int x = Mathf.FloorToInt(mapPosition.x / CellSize);
            int y = Mathf.FloorToInt(mapPosition.y / CellSize);
            return new Cell(x, y);
        }

        public bool IsInBounds(Cell cell)
        {
            if (_economy == null) return true;
            return cell.X >= 0 && cell.X < _economy.GridWidth && cell.Y >= 0 && cell.Y < _economy.GridHeight;
        }
    }
}
