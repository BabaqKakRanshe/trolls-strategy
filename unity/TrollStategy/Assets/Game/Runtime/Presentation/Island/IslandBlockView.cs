using System;
using UnityEngine;

namespace TrollStrategy.Presentation.Island
{
    /// <summary>
    /// Pieces of one block of the island prefab; IslandEnvironmentBuilder fills them from the Vitaria kit export
    /// (Models/Isle/Blocks/Isle_Block_x_y.fbx and Layout/isle_layout.json).
    /// <list type="bullet">
    /// <item>Land — everything that rises with the block: the lawn (Top), a wall per side (Wall_S/E/N/W), the wild
    /// edge ledge per side (Ledge_*), the rounded outer corners (Corner_SE/NE/NW/SW), the small wild ground
    /// (WildGround), the flat clover of a cleared block (Decor) and the trees/bushes/stones (Wild).</item>
    /// <item>Cover — the cloud puffs over the empty slot; they stay put and drift away when the block rises.</item>
    /// </list>
    /// Sides are S, E, N, W (0..3); corners are SE, NE, NW, SW (0..3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IslandBlockView : MonoBehaviour
    {
        [SerializeField] private int _x;
        [SerializeField] private int _y;
        [SerializeField] private float _bottom;
        [SerializeField] private string _biome;
        [SerializeField] private Transform _land;
        [SerializeField] private GameObject[] _walls = new GameObject[4];
        [SerializeField] private GameObject[] _ledges = new GameObject[4];
        [SerializeField] private GameObject[] _corners = new GameObject[4];
        [SerializeField] private GameObject _wildGround;
        [SerializeField] private GameObject _decor;
        [SerializeField] private Transform _wild;
        [SerializeField] private Transform _cover;
        // where the cover puffs rest over the empty slot: the rise moves them, a saved scene must not shift them
        [SerializeField] private Vector3[] _coverPositions = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] _coverScales = Array.Empty<Vector3>();

        public int X => _x;
        public int Y => _y;
        /// <summary>Depth of the block's rock pillar below the lawn, metres (negative).</summary>
        public float Bottom => _bottom;
        /// <summary>forest, rocks or meadow; empty for the starting blocks.</summary>
        public string Biome => _biome;
        public Transform Land => _land;
        public Transform Wild => _wild;
        public Transform Cover => _cover;
        public GameObject WildGround => _wildGround;
        public GameObject Decor => _decor;
        public GameObject Wall(int side) => Pick(_walls, side);
        public GameObject Ledge(int side) => Pick(_ledges, side);
        public GameObject Corner(int corner) => Pick(_corners, corner);

        public void Setup(int x, int y, float bottom, string biome, Transform land, GameObject[] walls,
            GameObject[] ledges, GameObject[] corners, GameObject wildGround, GameObject decor, Transform wild,
            Transform cover)
        {
            _x = x;
            _y = y;
            _bottom = bottom;
            _biome = biome;
            _land = land;
            _walls = walls;
            _ledges = ledges;
            _corners = corners;
            _wildGround = wildGround;
            _decor = decor;
            _wild = wild;
            _cover = cover;
            int n = cover != null ? cover.childCount : 0;
            _coverPositions = new Vector3[n];
            _coverScales = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                _coverPositions[i] = cover.GetChild(i).localPosition;
                _coverScales[i] = cover.GetChild(i).localScale;
            }
        }

        /// <summary>Resting place of cover puff <paramref name="index"/>; false if the puff was added later.</summary>
        public bool CoverHome(int index, out Vector3 position, out Vector3 scale)
        {
            bool known = _coverPositions != null && _coverScales != null && index >= 0 &&
                         index < _coverPositions.Length && index < _coverScales.Length;
            position = known ? _coverPositions[index] : Vector3.zero;
            scale = known ? _coverScales[index] : Vector3.one;
            return known;
        }

        private static GameObject Pick(GameObject[] pieces, int index) =>
            pieces != null && index >= 0 && index < pieces.Length ? pieces[index] : null;
    }
}
