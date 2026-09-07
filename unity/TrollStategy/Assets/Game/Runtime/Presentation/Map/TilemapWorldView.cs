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

        public void Init(Grid grid, Tilemap ground, Tilemap overlay, EconomyConfig economy)
        {
            _grid = grid;
            _groundTilemap = ground;
            _overlayTilemap = overlay;
            _economy = economy;
        }

        public Vector3 CellToWorld(Cell cell)
        {
            float cs = _economy != null ? _economy.CellSize : 1f;
            return new Vector3(cell.X * cs, cell.Y * cs, 0f);
        }

        public Vector3 BuildingCenterWorld(Cell cell, int width, int height)
        {
            float cs = _economy != null ? _economy.CellSize : 1f;
            return new Vector3((cell.X + width * 0.5f) * cs, (cell.Y + height * 0.5f) * cs, 0f);
        }

        public Cell WorldToCell(Vector3 worldPos)
        {
            float cs = _economy != null ? _economy.CellSize : 1f;
            int x = Mathf.FloorToInt(worldPos.x / cs);
            int y = Mathf.FloorToInt(worldPos.y / cs);
            return new Cell(x, y);
        }

        public bool IsInBounds(Cell cell)
        {
            if (_economy == null) return true;
            return cell.X >= 0 && cell.X < _economy.GridWidth && cell.Y >= 0 && cell.Y < _economy.GridHeight;
        }
    }
}
