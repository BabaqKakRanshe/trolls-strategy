using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.Presentation.Island
{
    /// <summary>What one 5x5 land block of the colony island shows.</summary>
    public enum LandBlockState
    {
        /// <summary>Not bought: an empty slot with clouds below the island's edge.</summary>
        Hidden = 0,
        /// <summary>Just bought: the land comes up out of the clouds; the view turns it Wild when it docks.</summary>
        Rising = 1,
        /// <summary>Docked, not cleared: forest, stones or meadow; nothing is built here.</summary>
        Wild = 2,
        /// <summary>Cleared lawn: buildable.</summary>
        Cleared = 3
    }

    /// <summary>
    /// The colony island that grows by 5x5 blocks. The view only shows states: which blocks are bought, wild or
    /// cleared comes from the game (see <see cref="Apply"/>); nothing here knows prices or rules.
    /// <para>Rules (the same as the kit's build_isle.apply_stage):</para>
    /// <list type="bullet">
    /// <item>a bought block shows its Land; an empty slot shows its Cover clouds;</item>
    /// <item>the edge ledge of a side shows when the block is bought and the block across that side is not;</item>
    /// <item>an outer corner shows when both of its sides and the diagonal block are empty;</item>
    /// <item>a side wall shows only under a noticeably shallower bought neighbour, and on a rising block or towards
    /// one — every other wall is hidden under the neighbour's lawn or behind the ledge;</item>
    /// <item>WildGround and Wild show while the block is Rising or Wild, Decor when it is Cleared.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IslandView : MonoBehaviour
    {
        public const int South = 0, East = 1, North = 2, West = 3;
        private static readonly Vector2Int[] SideStep =
            { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        // corners SE, NE, NW, SW: the two sides that meet there
        private static readonly int[] CornerSideA = { South, East, North, West };
        private static readonly int[] CornerSideB = { East, North, West, South };

        [SerializeField] private int _blocksPerSide = 8;
        [SerializeField] private float _blockSize = 5f;
        [SerializeField] private RectInt _startBlocks = new RectInt(2, 2, 4, 4);
        [Tooltip("A wall shows under a bought neighbour only when that neighbour's pillar is this much shallower, m.")]
        [SerializeField] private float _wallStep = 2f;
        [SerializeField] private float _riseDepth = 8f;
        [SerializeField] private float _riseSeconds = 2.2f;
        [Tooltip("How far the cover clouds drift away from a rising block, m.")]
        [SerializeField] private float _coverDrift = 3.5f;
        [Tooltip("Edit-mode preview of a Rising block: share of the rise already done.")]
        [SerializeField, Range(0f, 1f)] private float _previewRise = .6f;
        [SerializeField] private IslandBlockView[] _blocks = Array.Empty<IslandBlockView>();

        // saved with the scene: an edit-time stage preview stays what Play Mode and the camera rig start from
        [SerializeField, HideInInspector] private LandBlockState[] _states;
        private float[] _rise;                       // 0..1 while a block rises, -1 otherwise (runtime only)

        /// <summary>Raised when a rising block has docked (the view has switched it to Wild).</summary>
        public event Action<int, int> RiseFinished;

        public int BlocksPerSide => _blocksPerSide;
        public float BlockSize => _blockSize;
        public RectInt StartBlocks => _startBlocks;
        public int Count => _blocksPerSide * _blocksPerSide;
        private float Half => _blocksPerSide * _blockSize * .5f;

        public void Configure(int blocksPerSide, float blockSize, RectInt startBlocks, float wallStep, float riseDepth,
            float riseSeconds, IslandBlockView[] blocks)
        {
            _blocksPerSide = blocksPerSide;
            _blockSize = blockSize;
            _startBlocks = startBlocks;
            _wallStep = wallStep;
            _riseDepth = riseDepth;
            _riseSeconds = riseSeconds;
            _blocks = blocks ?? Array.Empty<IslandBlockView>();
            _states = null;
            _rise = null;
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < _blocksPerSide && y < _blocksPerSide;

        /// <summary>
        /// Grid slots whose land cannot show where the game sells it: no block view, a view of another slot, or a block
        /// moved off the island's origin (the kit bakes every block's land in island space, so each block root stays at
        /// the origin, unturned and unscaled, and the land lies over its slot).
        /// </summary>
        public List<Vector2Int> BrokenBlocks()
        {
            var broken = new List<Vector2Int>();
            for (int y = 0; y < _blocksPerSide; y++)
            for (int x = 0; x < _blocksPerSide; x++)
            {
                var block = Block(x, y);
                var root = block != null ? block.transform : null;
                if (block == null || block.X != x || block.Y != y || root.localPosition != Vector3.zero ||
                    root.localRotation != Quaternion.identity || root.localScale != Vector3.one)
                    broken.Add(new Vector2Int(x, y));
            }
            return broken;
        }

        private void Awake()
        {
            if (!UnityEngine.Application.isPlaying) return;
            var broken = BrokenBlocks();
            if (broken.Count > 0)
                Debug.LogError($"[Isle] {name}: {broken.Count} of {Count} blocks are missing or moved (first {broken[0]}); " +
                               "bought land there will not show over its slot. The scene's island must be the prefab as " +
                               "built: TrollStrategy > Isle > Reset Island In Scene To Prefab.", this);
        }

        public IslandBlockView Block(int x, int y) =>
            Inside(x, y) && _blocks != null && y * _blocksPerSide + x < _blocks.Length ? _blocks[y * _blocksPerSide + x] : null;

        public LandBlockState State(int x, int y)
        {
            EnsureState();
            return Inside(x, y) ? _states[y * _blocksPerSide + x] : LandBlockState.Hidden;
        }

        /// <summary>Middle of a block in the island's local space (x east, z north, y up; the lawn is at 0).</summary>
        public Vector3 BlockCenter(int x, int y) =>
            new Vector3((x + .5f) * _blockSize - Half, 0f, (y + .5f) * _blockSize - Half);

        /// <summary>Block under a point in the island's local space; false outside the grid.</summary>
        public bool BlockAt(Vector3 local, out int x, out int y)
        {
            x = Mathf.FloorToInt((local.x + Half) / _blockSize);
            y = Mathf.FloorToInt((local.z + Half) / _blockSize);
            return Inside(x, y);
        }

        /// <summary>Local-space bounds of the docked blocks (Wild or Cleared); the start zone when there are none.</summary>
        public Bounds OwnedBounds()
        {
            EnsureState();
            bool any = false;
            var bounds = new Bounds();
            for (int y = 0; y < _blocksPerSide; y++)
            for (int x = 0; x < _blocksPerSide; x++)
            {
                var s = _states[y * _blocksPerSide + x];
                if (s != LandBlockState.Wild && s != LandBlockState.Cleared) continue;
                var block = new Bounds(BlockCenter(x, y), new Vector3(_blockSize, 0f, _blockSize));
                if (!any) bounds = block;
                else bounds.Encapsulate(block);
                any = true;
            }
            if (!any)
            {
                var min = BlockCenter(_startBlocks.xMin, _startBlocks.yMin);
                var max = BlockCenter(_startBlocks.xMax - 1, _startBlocks.yMax - 1);
                bounds.SetMinMax(min - new Vector3(_blockSize, 0f, _blockSize) * .5f,
                    max + new Vector3(_blockSize, 0f, _blockSize) * .5f);
            }
            return bounds;
        }

        /// <summary>The first state of a game: the start zone cleared, every other slot empty.</summary>
        public void ShowStart()
        {
            var states = new LandBlockState[Count];
            for (int y = 0; y < _blocksPerSide; y++)
            for (int x = 0; x < _blocksPerSide; x++)
                states[y * _blocksPerSide + x] = _startBlocks.Contains(new Vector2Int(x, y))
                    ? LandBlockState.Cleared
                    : LandBlockState.Hidden;
            Apply(states, false);
        }

        /// <summary>
        /// Sets every block (index = y * BlocksPerSide + x). A block that turns Rising with <paramref name="animate"/>
        /// in Play Mode comes up over RiseSeconds and then turns Wild by itself (<see cref="RiseFinished"/>); without
        /// it (loading a game) it starts part way up. In edit mode a Rising block stays part way up (8 m x 0.4 below
        /// the lawn, like the kit's mid stage).
        /// Wild for a block that is still coming up does not cut the rise short: the game may already count it as
        /// docked land, the view lets it finish.
        /// </summary>
        public void Apply(IReadOnlyList<LandBlockState> states, bool animate = true)
        {
            EnsureState();
            int n = Mathf.Min(Count, states?.Count ?? 0);
            for (int i = 0; i < n; i++) Set(i, states[i], animate);
            Refresh();
        }

        public void SetState(int x, int y, LandBlockState state, bool animate = true)
        {
            if (!Inside(x, y)) return;
            EnsureState();
            Set(y * _blocksPerSide + x, state, animate);
            Refresh();
        }

        /// <summary>Clearing a wild block: hides that share (0..1) of its trees, bushes and stones, in a fixed order.</summary>
        public void SetClearProgress(int x, int y, float progress)
        {
            var block = Block(x, y);
            if (block == null || block.Wild == null) return;
            int count = block.Wild.childCount;
            int gone = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * count), 0, count);
            for (int i = 0; i < count; i++) block.Wild.GetChild(i).gameObject.SetActive(i >= gone);
        }

        /// <summary>World position of the wild object that clearing removes next (for a dust puff), or null.</summary>
        public Vector3? NextWildObject(int x, int y)
        {
            var block = Block(x, y);
            if (block == null || block.Wild == null) return null;
            for (int i = 0; i < block.Wild.childCount; i++)
            {
                var child = block.Wild.GetChild(i);
                if (child.gameObject.activeSelf) return child.position;
            }
            return null;
        }

        private void Set(int i, LandBlockState state, bool animate)
        {
            var previous = _states[i];
            if (previous == LandBlockState.Rising && state == LandBlockState.Wild && _rise[i] >= 0f &&
                UnityEngine.Application.isPlaying)
                return;                                  // still coming up: Update turns it Wild when it docks
            _states[i] = state;
            if (state == LandBlockState.Rising)
            {
                if (previous != LandBlockState.Rising || _rise[i] < 0f)
                    _rise[i] = animate && UnityEngine.Application.isPlaying ? 0f : _previewRise;
            }
            else
            {
                _rise[i] = -1f;
            }
            var block = _blocks != null && i < _blocks.Length ? _blocks[i] : null;
            if (block == null) return;
            PoseRise(block, _rise[i]);
            if (state != LandBlockState.Wild && state != LandBlockState.Rising) SetClearProgress(block.X, block.Y, 0f);
            else if (previous != LandBlockState.Wild && previous != LandBlockState.Rising) SetClearProgress(block.X, block.Y, 0f);
        }

        private void Update()
        {
            EnsureState();
            bool changed = false;
            for (int i = 0; i < _rise.Length; i++)
            {
                if (_rise[i] < 0f || _states[i] != LandBlockState.Rising) continue;
                _rise[i] = Mathf.Min(1f, _rise[i] + Time.deltaTime / Mathf.Max(.05f, _riseSeconds));
                var block = _blocks != null && i < _blocks.Length ? _blocks[i] : null;
                if (block != null) PoseRise(block, _rise[i]);
                if (_rise[i] < 1f) continue;
                _rise[i] = -1f;
                _states[i] = LandBlockState.Wild;
                changed = true;
                if (block != null) RiseFinished?.Invoke(block.X, block.Y);
            }
            if (changed) Refresh();
        }

        /// <summary>Land below the lawn level by the rest of the rise; cover clouds drift out and shrink.</summary>
        private void PoseRise(IslandBlockView block, float t)
        {
            // the overshoot is for the animation; a paused pose (edit-time preview) is linear, as the kit poses it
            float k = t < 0f ? 1f : UnityEngine.Application.isPlaying ? EaseOutBack(t) : t;
            if (block.Land != null) block.Land.localPosition = new Vector3(0f, -_riseDepth * (1f - k), 0f);
            if (block.Cover == null) return;
            var center = BlockCenter(block.X, block.Y);
            float drift = t < 0f ? 0f : Mathf.Clamp01(t);
            for (int c = 0; c < block.Cover.childCount; c++)
            {
                var puff = block.Cover.GetChild(c);
                if (!block.CoverHome(c, out var home, out var scale)) continue;
                var away = home - center;
                away.y = 0f;
                away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.right;
                puff.localPosition = home + away * (_coverDrift * drift) + Vector3.down * (.6f * drift);
                puff.localScale = scale * Mathf.Lerp(1f, .05f, drift * drift);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.2f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        private void EnsureState()
        {
            int n = Count;
            if (_states == null || _states.Length != n)
            {
                _states = new LandBlockState[n];
                for (int i = 0; i < n; i++)
                {
                    int x = i % _blocksPerSide, y = i / _blocksPerSide;
                    _states[i] = _startBlocks.Contains(new Vector2Int(x, y)) ? LandBlockState.Cleared : LandBlockState.Hidden;
                }
                _rise = null;
            }
            if (_rise != null && _rise.Length == n) return;
            // a block saved as Rising (edit-time preview) is part way up; in Play Mode it finishes the rise
            _rise = new float[n];
            for (int i = 0; i < n; i++) _rise[i] = _states[i] == LandBlockState.Rising ? _previewRise : -1f;
        }

        private bool Owned(int x, int y) => Inside(x, y) && _states[y * _blocksPerSide + x] != LandBlockState.Hidden;
        private bool Rising(int x, int y) => Inside(x, y) && _states[y * _blocksPerSide + x] == LandBlockState.Rising;

        private void Refresh()
        {
            for (int y = 0; y < _blocksPerSide; y++)
            for (int x = 0; x < _blocksPerSide; x++)
            {
                var block = Block(x, y);
                if (block == null) continue;
                int i = y * _blocksPerSide + x;
                var state = _states[i];
                bool owned = state != LandBlockState.Hidden;
                bool rising = state == LandBlockState.Rising;
                if (block.Land != null) block.Land.gameObject.SetActive(owned);
                for (int side = 0; side < 4; side++)
                {
                    int nx = x + SideStep[side].x, ny = y + SideStep[side].y;
                    bool neighbour = Owned(nx, ny);
                    Show(block.Ledge(side), owned && !neighbour);
                    bool shallower = neighbour && Block(nx, ny) != null && Block(nx, ny).Bottom > block.Bottom + _wallStep;
                    Show(block.Wall(side), owned && (rising || (neighbour && (Rising(nx, ny) || shallower))));
                }
                for (int corner = 0; corner < 4; corner++)
                {
                    var a = SideStep[CornerSideA[corner]];
                    var b = SideStep[CornerSideB[corner]];
                    Show(block.Corner(corner), owned && !Owned(x + a.x, y + a.y) && !Owned(x + b.x, y + b.y) &&
                                               !Owned(x + a.x + b.x, y + a.y + b.y));
                }
                bool wild = state == LandBlockState.Rising || state == LandBlockState.Wild;
                Show(block.WildGround, wild);
                if (block.Wild != null) block.Wild.gameObject.SetActive(wild);
                Show(block.Decor, state == LandBlockState.Cleared);
                if (block.Cover != null)
                    block.Cover.gameObject.SetActive(state == LandBlockState.Hidden || (rising && _rise[i] < 1f));
            }
        }

        private static void Show(GameObject piece, bool visible)
        {
            if (piece != null && piece.activeSelf != visible) piece.SetActive(visible);
        }

        // ---------------------------------------------------------------- Play Mode checks before the game drives it

        [ContextMenu("Debug: Raise an Empty Block Next to the Land")]
        private void DebugRaiseNeighbour()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[Isle] Play Mode only");
                return;
            }
            EnsureState();
            for (int y = 0; y < _blocksPerSide; y++)
            for (int x = 0; x < _blocksPerSide; x++)
            {
                if (Owned(x, y)) continue;
                for (int side = 0; side < 4; side++)
                {
                    if (!Owned(x + SideStep[side].x, y + SideStep[side].y)) continue;
                    SetState(x, y, LandBlockState.Rising);
                    Debug.Log($"[Isle] block {x},{y} rises");
                    return;
                }
            }
        }

        [ContextMenu("Debug: Clear Every Wild Block")]
        private void DebugClearWild()
        {
            EnsureState();
            for (int i = 0; i < _states.Length; i++)
                if (_states[i] == LandBlockState.Wild) Set(i, LandBlockState.Cleared, false);
            Refresh();
        }
    }
}
