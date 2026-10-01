using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Map;
using TrollStrategy.Presentation.Visuals;
using TrollStrategy.Presentation.WorldUi;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TrollStrategy.Presentation.Island
{
    /// <summary>
    /// The colony's land on the island. Reads the game snapshot and tells <see cref="IslandView"/> what each block
    /// shows: not owned — Hidden, owned and wild — Wild, cleared — Cleared. A block bought since the last snapshot
    /// is handed over once as Rising: the view brings it up out of the clouds and turns it Wild itself, and the
    /// camera slides over to it. While a block is cleared its trees and stones go one by one and a bar over it fills.
    /// In the land mode (<see cref="InteractionModeType.ManagingLand"/>) the blocks for sale get kit corners and a
    /// price, and a click on the map picks a block. Decides nothing: prices and rules come from the snapshot.
    /// </summary>
    public sealed class LandPresenter : MonoBehaviour
    {
        // the kit's corner model lies in the FBX's Y-up; in the map plane (local -Z up, +Y north) it is turned like
        // the SelectionRim corners of the building prefabs (BuildingKitDetailsMigration.KitRotation)
        private static readonly Quaternion KitRotation = new Quaternion(0f, 0.7071068f, -0.7071068f, 0f);

        [Tooltip("The kit's corner piece (Vitaria/Models/Colony/FX_Select_Corner) that frames a block for sale.")]
        [SerializeField] private GameObject _corner;
        [SerializeField, Min(.1f)] private float _cornerScale = 1.6f;
        [Tooltip("How far the corners sit inside the block's edge, cells.")]
        [SerializeField, Min(0f)] private float _cornerInset = .2f;
        [Tooltip("Height of the price over the block, m.")]
        [SerializeField] private float _priceHeight = .6f;
        [Tooltip("The clearing bar and the offer over wild land stand over the middle of the block, this far above " +
                 "its tallest tree or stone, m: like a building's label, nothing on the block covers them.")]
        [SerializeField, Min(0f)] private float _wildGap = .3f;

        private GameSession _session;
        private InteractionController _interaction;
        private TilemapWorldView _worldView;
        private Camera _camera;
        private IslandView _island;
        private IslandCameraRig _rig;
        private LandSnapshot _land;
        private bool[] _owned;
        private int _modeFrame = -1;
        private InteractionModeType _lastMode;
        private readonly Dictionary<int, Marker> _markers = new();
        private readonly Dictionary<int, ClearBar> _bars = new();
        // height of each wild block's tallest object over the lawn, m, measured while all of it still stands
        private readonly Dictionary<int, float> _wildTops = new();

        private sealed class Marker
        {
            public Transform Root;
            public Transform Corners;
            public WorldPanel Price;
            public Label Label;
        }

        private sealed class ClearBar
        {
            public WorldPanel Panel;
            public Label Label;
            public VisualElement Fill;
        }

        public IslandView Island => _island;

        public void Init(GameSession session, InteractionController interaction, TilemapWorldView worldView,
            Camera camera, IslandView island)
        {
            Unsubscribe();
            _session = session;
            _interaction = interaction;
            _worldView = worldView;
            _camera = camera;
            _island = island;
            _rig = camera != null ? camera.GetComponent<IslandCameraRig>() : null;
            var land = session?.CurrentSnapshot.Land;
            if (_island != null && land != null && _island.BlocksPerSide != land.BlocksPerSide)
            {
                Debug.LogWarning($"{nameof(LandPresenter)}: the island has {_island.BlocksPerSide} blocks per side, " +
                                 $"the game's land {land.BlocksPerSide}; the island stays as it is", this);
                _island = null;
            }
            if (_session == null) return;
            _session.OnSnapshotChanged += OnSnapshotChanged;
            if (_interaction != null) _interaction.OnInteractionChanged += OnInteractionChanged;
            if (_island != null) _island.RiseFinished += OnRiseFinished;
            _owned = null;
            OnSnapshotChanged(_session.CurrentSnapshot);
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_session != null) _session.OnSnapshotChanged -= OnSnapshotChanged;
            if (_interaction != null) _interaction.OnInteractionChanged -= OnInteractionChanged;
            if (_island != null) _island.RiseFinished -= OnRiseFinished;
        }

        // ------------------------------------------------------------------------------------------ island

        private void OnSnapshotChanged(GameSnapshot snapshot)
        {
            _land = snapshot.Land;
            if (_land == null) return;
            ShowIsland();
            ShowClearing();
            ShowMarkers(snapshot);
        }

        private void OnInteractionChanged()
        {
            if (_session != null) ShowMarkers(_session.CurrentSnapshot);
        }

        private void ShowIsland()
        {
            int count = _land.BlocksPerSide * _land.BlocksPerSide;
            bool first = _owned == null || _owned.Length != count;
            if (first)
            {
                _owned = new bool[count];
                var states = new LandBlockState[count];
                for (int i = 0; i < count; i++)
                {
                    var block = _land.Blocks[i];
                    _owned[i] = block.Owned;
                    states[i] = Desired(block);
                }
                // a game that starts (or loads) shows its land in place, nothing rises
                if (_island != null) _island.Apply(states, false);
                return;
            }
            int boughtCount = 0, boughtIndex = -1;
            for (int i = 0; i < count; i++)
            {
                var block = _land.Blocks[i];
                bool bought = block.Owned && !_owned[i];
                _owned[i] = block.Owned;
                if (_island == null) continue;
                if (bought)
                {
                    // handed over once: the view lifts it and turns it Wild when it docks (RiseFinished)
                    _island.SetState(block.X, block.Y, LandBlockState.Rising);
                    boughtCount++;
                    boughtIndex = i;
                    continue;
                }
                var shown = _island.State(block.X, block.Y);
                var desired = Desired(block);
                if (shown == desired || shown == LandBlockState.Rising && desired == LandBlockState.Wild) continue;
                if (shown == LandBlockState.Wild && desired == LandBlockState.Cleared) GameAudio.Play(Sfx.Build);
                _island.SetState(block.X, block.Y, desired);
            }
            // one purchase: the camera slides over to it, the zoom stays (a cheat buying everything leaves it be)
            if (boughtCount == 1 && _rig != null)
            {
                var bought = _land.Blocks[boughtIndex];
                _rig.Frame(_island.transform.TransformPoint(_island.BlockCenter(bought.X, bought.Y)), _rig.Side);
            }
        }

        private static LandBlockState Desired(LandBlockSnapshot block) =>
            !block.Owned ? LandBlockState.Hidden : block.Cleared ? LandBlockState.Cleared : LandBlockState.Wild;

        private void OnRiseFinished(int x, int y)
        {
            GameAudio.Play(Sfx.Land);
            // the bought block has docked: now it can be framed for clearing
            if (_session != null) ShowMarkers(_session.CurrentSnapshot);
        }

        /// <summary>Trees and stones of a block being cleared go one by one; a bar over it counts the seconds.</summary>
        private void ShowClearing()
        {
            var clearing = new HashSet<int>();
            foreach (var block in _land.Blocks)
            {
                if (!block.Clearing) continue;
                int index = block.Y * _land.BlocksPerSide + block.X;
                clearing.Add(index);
                if (_island != null) _island.SetClearProgress(block.X, block.Y, block.ClearProgress);
                var bar = Bar(index, block.X, block.Y);
                if (bar == null) continue;
                bar.Panel.gameObject.SetActive(true);
                bar.Fill.style.width = Length.Percent(Mathf.Clamp01(block.ClearProgress) * 100f);
                bar.Label.text = $"Расчистка, {Mathf.CeilToInt(block.ClearSecondsLeft)} с";
            }
            foreach (var pair in _bars)
                if (!clearing.Contains(pair.Key) && pair.Value.Panel.gameObject.activeSelf)
                    pair.Value.Panel.gameObject.SetActive(false);
        }

        private ClearBar Bar(int index, int x, int y)
        {
            if (_bars.TryGetValue(index, out var bar)) return bar;
            if (_worldView == null) return null;
            var panel = WorldPanel.Create($"LandClearBar_{x}_{y}", transform, 60, Pivot.BottomCenter, "land-bar");
            panel.transform.position = OverWild(x, y);
            bar = new ClearBar { Panel = panel, Label = panel.AddLabel("world-label land-bar__label") };
            var track = Part(panel.Content, "land-bar__track");
            bar.Fill = Part(track, "land-bar__fill");
            _bars[index] = bar;
            return bar;
        }

        // ------------------------------------------------------------------------------------------ land mode

        /// <summary>Corners and a price on every block for sale, and on the picked block, while the land mode is on.</summary>
        private void ShowMarkers(GameSnapshot snapshot)
        {
            var land = snapshot.Land;
            var mode = _interaction != null ? _interaction.Mode : InteractionMode.Neutral;
            bool on = land != null && mode.Type == InteractionModeType.ManagingLand;
            var wanted = new HashSet<int>();
            if (on)
            {
                foreach (var block in land.Blocks)
                {
                    bool picked = mode.LandOffer != LandOffer.None && block.X == mode.LandX && block.Y == mode.LandY;
                    // a bought block still rising has no corners yet: it gets them once it has docked
                    bool rising = _island != null && _island.State(block.X, block.Y) == LandBlockState.Rising;
                    if (!block.CanBuy && !picked) continue;
                    if (rising) continue;
                    int index = block.Y * land.BlocksPerSide + block.X;
                    wanted.Add(index);
                    var marker = MarkerAt(index, block.X, block.Y);
                    marker.Root.gameObject.SetActive(true);
                    marker.Corners.localScale = Vector3.one * (picked ? 1.12f : 1f);
                    if (marker.Label == null) continue;
                    bool forSale = !block.Owned;
                    marker.Price.gameObject.SetActive(forSale || picked);
                    // over wild land the offer stands above the block's trees, where the clearing bar will be
                    marker.Price.transform.position = forSale
                        ? marker.Root.position + _worldView.GroundOffset(_priceHeight)
                        : OverWild(block.X, block.Y);
                    marker.Label.text = forSale ? Gold(land.NextPrice) : "Расчистить?";
                    marker.Label.EnableInClassList("land-price--short", forSale && snapshot.Gold < land.NextPrice);
                    marker.Label.EnableInClassList("land-price--picked", picked);
                }
            }
            foreach (var pair in _markers)
                if (!wanted.Contains(pair.Key) && pair.Value.Root.gameObject.activeSelf)
                    pair.Value.Root.gameObject.SetActive(false);
        }

        private static string Gold(int gold) => gold + " зол.";

        private Marker MarkerAt(int index, int x, int y)
        {
            if (_markers.TryGetValue(index, out var marker)) return marker;
            var root = new GameObject($"LandMarker_{x}_{y}").transform;
            root.SetParent(transform, false);
            root.position = BlockWorld(x, y);
            marker = new Marker { Root = root, Corners = new GameObject("Corners").transform };
            marker.Corners.SetParent(root, false);
            if (_worldView != null)
            {
                int size = _land.BlockSize;
                float cell = _worldView.CellSize;
                float half = size * cell * .5f - _cornerInset * cell;
                // SW, SE, NE, NW around the block's middle in map space; each corner turned a quarter more
                var offsets = new[] { new Vector2(-half, -half), new Vector2(half, -half), new Vector2(half, half), new Vector2(-half, half) };
                var center = _worldView.WorldToMap(root.position);
                for (int k = 0; k < 4 && _corner != null; k++)
                {
                    var corner = Instantiate(_corner, marker.Corners);
                    corner.name = "Corner" + k;
                    foreach (var collider in corner.GetComponentsInChildren<Collider>(true)) Destroy(collider);
                    corner.transform.position = _worldView.MapToWorld(center + (Vector3)offsets[k]) +
                                                _worldView.GroundOffset(.05f);
                    corner.transform.rotation = _worldView.GroundRotation *
                                                Quaternion.AngleAxis(90f * k, Vector3.forward) * KitRotation;
                    corner.transform.localScale = Vector3.one * _cornerScale;
                }
                marker.Price = WorldPanel.Create("Price", root, 60, Pivot.BottomCenter);
                marker.Price.transform.position = root.position + _worldView.GroundOffset(_priceHeight);
                marker.Label = marker.Price.AddLabel("world-label land-price");
            }
            _markers[index] = marker;
            return marker;
        }

        private void Update()
        {
            if (_interaction == null || _worldView == null || _camera == null || _land == null) return;
            var mode = _interaction.Mode.Type;
            if (mode != _lastMode)
            {
                _lastMode = mode;
                _modeFrame = Time.frameCount;
            }
            // the click that switched the mode on (the HUD button) is not a pick; a tap never starts on the HUD
            if (mode != InteractionModeType.ManagingLand || Time.frameCount <= _modeFrame) return;
            Vector2 pointer;
            if (MapPointer.UsesTouch)
            {
                if (!MapPointer.Tapped(out pointer)) return;
            }
            else
            {
                if (Mouse.current == null || !Mouse.current.leftButton.wasReleasedThisFrame ||
                    UIInputUtils.IsPointerOverUI())
                    return;
                pointer = Mouse.current.position.ReadValue();
            }
            if (!WorldProjection.TryGroundPoint(_camera, pointer, _worldView, out var world)) return;
            var cell = _worldView.WorldToCell(world);
            int size = _land.BlockSize;
            _interaction.ChooseLandBlock(Mathf.FloorToInt(cell.X / (float)size), Mathf.FloorToInt(cell.Y / (float)size));
        }

        private void LateUpdate()
        {
            if (_camera == null || !_camera.isActiveAndEnabled) return;
            foreach (var marker in _markers.Values)
                if (marker.Price != null && marker.Root.gameObject.activeSelf) marker.Price.Face(_camera);
            foreach (var bar in _bars.Values)
                if (bar.Panel.gameObject.activeSelf) bar.Panel.Face(_camera);
        }

        /// <summary>Middle of block (x, y) on the lawn, world space (the game's grid decides where it is).</summary>
        private Vector3 BlockWorld(int x, int y)
        {
            int size = _land != null ? _land.BlockSize : 5;
            if (_worldView != null)
                return _worldView.BuildingCenterWorld(new Cell(x * size, y * size), size, size);
            return _island != null ? _island.transform.TransformPoint(_island.BlockCenter(x, y)) : Vector3.zero;
        }

        /// <summary>
        /// Over the middle of block (x, y) and <see cref="_wildGap"/> above its tallest tree or stone, world space — as a
        /// building's label stands over its model, so from the colony camera nothing on the block covers it. Measured
        /// once, while the whole wild land stands, so the bar does not sink as the clearing takes the trees away.
        /// </summary>
        private Vector3 OverWild(int x, int y)
        {
            int index = y * _land.BlocksPerSide + x;
            if (!_wildTops.TryGetValue(index, out float top))
            {
                float? measured = WildTop(x, y);
                top = measured ?? 0f;
                if (measured.HasValue) _wildTops[index] = top;
            }
            return BlockWorld(x, y) + _worldView.GroundOffset(top + _wildGap);
        }

        /// <summary>
        /// Height over the lawn of the tallest wild object the block shows, m, as it stands once docked (a block still
        /// coming up out of the clouds counts from where it will stop); null while it shows none.
        /// </summary>
        private float? WildTop(int x, int y)
        {
            var block = _island != null ? _island.Block(x, y) : null;
            if (block == null || block.Wild == null || block.Land == null) return null;
            var up = _worldView.GroundOffset(1f);
            // measured from the block's land: docked, its origin lies on the lawn; rising, it carries the trees down
            float? top = null;
            foreach (var renderer in block.Wild.GetComponentsInChildren<Renderer>(false))
            {
                var bounds = renderer.bounds;
                var extents = bounds.extents;
                float height = Vector3.Dot(bounds.center - block.Land.position, up) +
                               Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y + Mathf.Abs(up.z) * extents.z;
                top = top.HasValue ? Mathf.Max(top.Value, height) : height;
            }
            return top;
        }

        private static VisualElement Part(VisualElement parent, string className)
        {
            var part = new VisualElement { pickingMode = PickingMode.Ignore };
            part.AddToClassList(className);
            parent.Add(part);
            return part;
        }
    }
}
