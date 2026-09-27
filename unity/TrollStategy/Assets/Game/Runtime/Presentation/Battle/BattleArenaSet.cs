using System;
using TrollStrategy.Domain;
using UnityEngine;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>Roles of an arena's effect meshes (the layout's "fx"); each role may have several variants.</summary>
    public enum ArenaFx { Dust, Smoke, DebrisStone, DebrisEarth, DebrisWood, Rock, Spark }

    /// <summary>
    /// Art of a battle arena, carried by the root of a mission's environment prefab: cell tiles,
    /// obstacles, effect meshes and the camera framing and lighting the environment was dressed for.
    /// Without it the board keeps its primitive blockout and flat effects. Filled by
    /// TrollStrategy > Arena > Build Arena Prefabs.
    /// </summary>
    public sealed class BattleArenaSet : MonoBehaviour
    {
        [Header("Cells")]
        [SerializeField] private GameObject[] _neutralTiles = Array.Empty<GameObject>();
        [SerializeField] private GameObject _playerTile;
        [SerializeField] private GameObject _enemyTile;
        [SerializeField] private GameObject[] _obstacles = Array.Empty<GameObject>();

        [Header("Effects")]
        [Tooltip("Opaque palette meshes; effects make them vanish by scale, never by alpha.")]
        [SerializeField] private GameObject[] _dust = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _smoke = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _debrisStone = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _debrisEarth = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _debrisWood = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _rock = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _spark = Array.Empty<GameObject>();

        [Header("Camera")]
        [SerializeField, Range(20f, 85f)] private float _pitch = 50.2f;
        [SerializeField, Range(10f, 70f)] private float _fieldOfView = 32.2f;
        [Tooltip("Distance to the focus point at 16:9; narrower screens move the camera further back.")]
        [SerializeField, Min(1f)] private float _distance = 27f;
        [Tooltip("Focus point shift from the board middle along depth; negative is towards the camera.")]
        [SerializeField] private float _focusOffset = -1.6f;
        [Tooltip("Largest share of the frame width the board may take.")]
        [SerializeField, Range(.3f, 1f)] private float _boardWidthShare = .69f;
        [SerializeField] private Color _background = new(.33f, .45f, .6f, 1f);

        [Header("Lighting")]
        [Tooltip("World direction the sunlight travels in.")]
        [SerializeField] private Vector3 _sunDirection = new(-.36888f, -.76575f, .52683f);
        [SerializeField] private Color _sunColor = new(1f, .95f, .86f, 1f);
        [SerializeField, Min(0f)] private float _sunIntensity = 1.3f;
        [SerializeField] private Color _ambientSky = new(.64f, .72f, .82f, 1f);
        [SerializeField] private Color _ambientEquator = new(.52f, .58f, .5f, 1f);
        [SerializeField] private Color _ambientGround = new(.28f, .26f, .24f, 1f);
        [Tooltip("How far past the camera focus shadows must reach; during the battle the pipeline's shadow " +
                 "distance is raised to the camera distance plus this.")]
        [SerializeField, Min(0f)] private float _shadowReach = 30f;

        public float Pitch => _pitch;
        public float FieldOfView => _fieldOfView;
        public float Distance => _distance;
        public float FocusOffset => _focusOffset;
        public float BoardWidthShare => _boardWidthShare;
        public Color Background => _background;
        public Vector3 SunDirection => _sunDirection;
        public Color SunColor => _sunColor;
        public float SunIntensity => _sunIntensity;
        public Color AmbientSky => _ambientSky;
        public Color AmbientEquator => _ambientEquator;
        public Color AmbientGround => _ambientGround;
        public float ShadowReach => _shadowReach;

        /// <summary>Tile for a cell: deployment zones have their own, other cells get a stable variant.</summary>
        public GameObject TileFor(Cell cell, bool playerZone, bool enemyZone)
        {
            if (playerZone && _playerTile != null) return _playerTile;
            if (enemyZone && _enemyTile != null) return _enemyTile;
            return Pick(_neutralTiles, cell, 11);
        }

        public GameObject ObstacleFor(Cell cell) => Pick(_obstacles, cell, 29);

        /// <summary>Variant <paramref name="variant"/> (wrapped) of an effect mesh, or null when the role has none.</summary>
        public GameObject Effect(ArenaFx role, int variant)
        {
            var pool = Effects(role);
            if (pool == null || pool.Length == 0) return null;
            int start = (variant % pool.Length + pool.Length) % pool.Length;
            for (int i = 0; i < pool.Length; i++)
            {
                var item = pool[(start + i) % pool.Length];
                if (item != null) return item;
            }
            return null;
        }

        public bool HasEffect(ArenaFx role) => Effect(role, 0) != null;

        public void ConfigureEffects(GameObject[] dust, GameObject[] smoke, GameObject[] debrisStone,
            GameObject[] debrisEarth, GameObject[] debrisWood, GameObject[] rock, GameObject[] spark)
        {
            _dust = dust ?? Array.Empty<GameObject>();
            _smoke = smoke ?? Array.Empty<GameObject>();
            _debrisStone = debrisStone ?? Array.Empty<GameObject>();
            _debrisEarth = debrisEarth ?? Array.Empty<GameObject>();
            _debrisWood = debrisWood ?? Array.Empty<GameObject>();
            _rock = rock ?? Array.Empty<GameObject>();
            _spark = spark ?? Array.Empty<GameObject>();
        }

        private GameObject[] Effects(ArenaFx role) => role switch
        {
            ArenaFx.Dust => _dust,
            ArenaFx.Smoke => _smoke,
            ArenaFx.DebrisStone => _debrisStone,
            ArenaFx.DebrisEarth => _debrisEarth,
            ArenaFx.DebrisWood => _debrisWood,
            ArenaFx.Rock => _rock,
            ArenaFx.Spark => _spark,
            _ => null
        };

        /// <summary>Stable turn of a cell's tile in 60° steps, so neighbours do not repeat one pattern.</summary>
        public static int TurnFor(Cell cell) => Hash(cell, 3) % 6;

        public void Configure(GameObject[] neutralTiles, GameObject playerTile, GameObject enemyTile,
            GameObject[] obstacles, float pitch, float fieldOfView, float distance, float focusOffset,
            float boardWidthShare, Color background)
        {
            _neutralTiles = neutralTiles ?? Array.Empty<GameObject>();
            _playerTile = playerTile;
            _enemyTile = enemyTile;
            _obstacles = obstacles ?? Array.Empty<GameObject>();
            _pitch = pitch;
            _fieldOfView = fieldOfView;
            _distance = distance;
            _focusOffset = focusOffset;
            _boardWidthShare = boardWidthShare;
            _background = background;
        }

        /// <summary>Sun and trilight ambient the arena was lit with in the kit.</summary>
        public void ConfigureLighting(Vector3 sunDirection, Color sunColor, float sunIntensity,
            Color ambientSky, Color ambientEquator, Color ambientGround)
        {
            _sunDirection = sunDirection;
            _sunColor = sunColor;
            _sunIntensity = sunIntensity;
            _ambientSky = ambientSky;
            _ambientEquator = ambientEquator;
            _ambientGround = ambientGround;
        }

        private static GameObject Pick(GameObject[] pool, Cell cell, int salt)
        {
            if (pool == null || pool.Length == 0) return null;
            int start = Hash(cell, salt) % pool.Length;
            for (int i = 0; i < pool.Length; i++)
            {
                var item = pool[(start + i) % pool.Length];
                if (item != null) return item;
            }
            return null;
        }

        private static int Hash(Cell cell, int salt)
        {
            unchecked
            {
                uint h = (uint)(cell.X * 374761393 + cell.Y * 668265263 + salt * 1274126177);
                h = (h ^ (h >> 13)) * 1103515245u;
                h ^= h >> 16;
                return (int)(h & 0x7fffffffu);
            }
        }
    }
}
