using System;
using System.Collections.Generic;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Audio;
using TrollStrategy.Presentation.Feel;
using TrollStrategy.Presentation.Units;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrollStrategy.Presentation.Battle
{
    public sealed class BattleCellView : MonoBehaviour
    {
        public Cell Cell { get; private set; }
        public void Init(Cell cell) => Cell = cell;
    }

    /// <summary>
    /// 3D view of a mission board; never decides gameplay validity. With an arena environment
    /// (a mission prefab carrying <see cref="BattleArenaSet"/>) cells get the arena's tiles and
    /// obstacles; without one the board stays a replaceable primitive blockout. Fighters are the units'
    /// sprites; the replay shows each report event as a readable beat (swing, flight, impact, death).
    /// </summary>
    public sealed class BattleBoardView : MonoBehaviour
    {
        public const float HexAcrossFlats = 2f;
        private const float Radius = HexAcrossFlats / 1.7320508f;
        private const float RowStep = Radius * 1.5f;

        private static readonly Color DealtColor = new(1f, .86f, .35f, 1f);
        private static readonly Color TakenColor = new(1f, .45f, .38f, 1f);
        private static readonly Color WeakColor = new(.82f, .82f, .8f, 1f);
        private static readonly Color HoverValid = new(1f, 1f, .92f, .55f);
        private static readonly Color HoverSwap = new(1f, .84f, .35f, .6f);
        private static readonly Color HoverInvalid = new(1f, .32f, .28f, .55f);

        private readonly List<Material> _materials = new();
        private readonly List<Mesh> _meshes = new();
        private readonly Dictionary<string, BattleFighterView> _allies = new(StringComparer.Ordinal);
        private readonly List<BattleFighterView> _enemies = new();
        private readonly Dictionary<string, BattleFighterView> _fighters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _health = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _stepMs = new(StringComparer.Ordinal);
        private readonly List<BattleFighterView> _views = new();
        private readonly List<Beat> _beats = new();
        private readonly List<Beat> _due = new();
        private string _selectedAlly;
        private BattleBoard _board;
        private BattleMissionDefinition _mission;
        private GameContentCatalog _catalog;
        private Camera _camera;
        private Vector3 _origin;
        private Bounds _bounds;
        private Material _outlineMaterial;
        private BattleArenaSet _arena;
        private BattleEffects _effects;
        private SpriteRenderer _hover;
        private Cell? _hoverCell;
        private float _hoverPop;
        private float _clock;
        private int _numberSide;

        public event Action<Cell> CellClicked;
        /// <summary>A blow killed a fighter; the scene freezes the replay for a beat.</summary>
        public event Action KillLanded;
        public Bounds WorldBounds => _bounds;
        /// <summary>Arena art of the mission environment, or null for the blockout.</summary>
        public BattleArenaSet Arena => _arena;
        /// <summary>Middle of the cell centres, where the mission environment is placed.</summary>
        public Vector3 BoardCenter { get; private set; }
        /// <summary>Board extent over hex corners: x across columns, y along rows (world Z).</summary>
        public Vector2 BoardSize { get; private set; }
        /// <summary>Highlight the cell under the pointer (preparation with a fighter selected).</summary>
        public bool PlacementHover { get; set; }
        /// <summary>Swings, projectiles or deaths from the report are still on their way.</summary>
        public bool HasPendingBeats => _beats.Count > 0;
        public int AlivePlayers => CountAlive(false);
        public int AliveEnemies => CountAlive(true);

        private int CountAlive(bool enemy)
        {
            int count = 0;
            foreach (var fighter in _fighters.Values)
                if (fighter != null && fighter.IsEnemy == enemy && !fighter.IsDead) count++;
            return count;
        }

        public void Init(BattleBoard board, BattleMissionDefinition mission, Camera battleCamera, Vector3 origin,
            GameContentCatalog catalog)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _mission = mission ?? throw new ArgumentNullException(nameof(mission));
            _camera = battleCamera ?? throw new ArgumentNullException(nameof(battleCamera));
            _catalog = catalog;
            _origin = origin;
            _effects = new GameObject("BattleEffects", typeof(BattleEffects)).GetComponent<BattleEffects>();
            _effects.transform.SetParent(transform, false);
            _effects.Init(battleCamera);
            Build();
            _effects.UseArena(_arena);
        }

        public Vector3 CellWorldPosition(Cell cell) =>
            _origin + new Vector3((cell.X + ((cell.Y & 1) == 1 ? .5f : 0f)) * HexAcrossFlats,
                0f, cell.Y * RowStep);

        /// <summary>Shows the preparation line-up: new fighters drop in, moved ones hop, removed ones vanish.</summary>
        public void ShowPlayerPlacements(IReadOnlyList<BattlePlacement> placements,
            IReadOnlyDictionary<string, UnitKind> kinds)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            if (placements != null)
                foreach (var placement in placements)
                {
                    if (!kinds.TryGetValue(placement.UnitId, out var kind)) continue;
                    keep.Add(placement.UnitId);
                    var ground = CellWorldPosition(placement.Cell);
                    if (_allies.TryGetValue(placement.UnitId, out var view) && view != null)
                    {
                        if (view.Cell == placement.Cell) continue;
                        view.SetCell(placement.Cell, ground, .22f);
                        _effects.Landing(ground, .2f);          // when the hop comes down
                    }
                    else
                    {
                        view = CreateFighter($"Ally_{placement.UnitId}", placement.UnitId, kind, false, placement.Cell);
                        view.PopIn();
                        _allies[placement.UnitId] = view;
                        _effects.Landing(ground, .08f);
                    }
                    _effects.GroundPing(ground, new Color(.55f, .8f, 1f, .9f), 1.05f);
                    GameAudio.Play(Sfx.Land);
                }

            var gone = new List<string>();
            foreach (var pair in _allies)
                if (!keep.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (var id in gone)
            {
                if (_allies[id] != null) _allies[id].Remove();
                _allies.Remove(id);
            }
        }

        /// <summary>Marks the roster's selected fighter on the board.</summary>
        public void ShowSelection(string unitId)
        {
            _selectedAlly = unitId;
            foreach (var pair in _allies)
                if (pair.Value != null) pair.Value.SetSelected(pair.Key == unitId);
        }

        /// <summary>Instant answer to a cell click: a ring where it landed, red when it was refused.</summary>
        public void PulseCell(Cell cell, bool accepted)
        {
            var ground = CellWorldPosition(cell);
            _effects.GroundPing(ground, accepted ? new Color(1f, 1f, .9f, .9f) : new Color(1f, .3f, .25f, 1f),
                accepted ? 1.1f : 1.25f, accepted ? .3f : .45f);
            _hoverPop = 1f;
        }

        public void BeginReplay(BattleReport report)
        {
            PlacementHover = false;
            _beats.Clear();
            _fighters.Clear();
            _health.Clear();
            _stepMs.Clear();
            var claimedEnemies = new HashSet<BattleFighterView>();
            foreach (var fighter in report.Fighters)
            {
                BattleFighterView view = null;
                if (fighter.IsPlayer) _allies.TryGetValue(fighter.Id, out view);
                else
                    foreach (var enemy in _enemies)
                        if (enemy != null && enemy.Cell == fighter.Cell && claimedEnemies.Add(enemy))
                        {
                            view = enemy;
                            break;
                        }
                if (view == null)
                {
                    view = CreateFighter(fighter.Id, fighter.Id, fighter.Kind, !fighter.IsPlayer, fighter.Cell);
                    view.PopIn();
                }
                view.name = fighter.Id;
                view.SetSelected(false);
                view.ResetHealth(fighter.Health);
                _fighters[fighter.Id] = view;
                _health[fighter.Id] = fighter.Health;
                _stepMs[fighter.Id] = fighter.StepIntervalMs;
            }

            // allies taken off the board and enemies the report does not know leave quietly
            foreach (var view in _views)
                if (view != null && !_fighters.ContainsValue(view)) view.Remove();
            _allies.Clear();
            _enemies.Clear();
        }

        public void ShowEvent(BattleEvent battleEvent)
        {
            if (!_fighters.TryGetValue(battleEvent.ActorId, out var actor) || actor == null) return;
            switch (battleEvent.Kind)
            {
                case BattleEventKind.Move:
                    _stepMs.TryGetValue(battleEvent.ActorId, out int stepMs);
                    actor.SetCell(battleEvent.Cell, CellWorldPosition(battleEvent.Cell),
                        Mathf.Clamp(stepMs * .0009f, .15f, .45f));
                    GameAudio.Play(Sfx.Step, .55f, .95f, .025f);
                    break;
                case BattleEventKind.Attack when battleEvent.TargetId != null &&
                                                 _fighters.TryGetValue(battleEvent.TargetId, out var target) && target != null:
                    bool melee = BattleBoard.HexDistance(actor.Cell, target.Cell) <= 1;
                    float release = actor.BeginAttack(target.Ground, melee);
                    GameAudio.Play(melee ? Sfx.Swing : Sfx.Throw, 1f, actor.Kind == UnitKind.Troll ? .85f : 1f);
                    _beats.Add(new Beat(_clock + release, melee ? BeatKind.Impact : BeatKind.Release,
                        battleEvent.ActorId, battleEvent.TargetId, battleEvent.Damage, false));
                    break;
                case BattleEventKind.Death:
                    // normally the killing blow lands first; this only catches a report the HP count missed
                    _beats.Add(new Beat(_clock + 1f, BeatKind.Death, battleEvent.ActorId, null, 0, false));
                    break;
            }
        }

        /// <summary>Advances fighters, pending blows and effects by replay (or preparation) time.</summary>
        public void Advance(float dt)
        {
            _clock += dt;
            // resolve due beats in time order; a throw released now may already have landed on a long frame
            while (true)
            {
                _due.Clear();
                foreach (var beat in _beats)
                    if (beat.At <= _clock) _due.Add(beat);
                if (_due.Count == 0) break;
                _beats.RemoveAll(beat => beat.At <= _clock);
                _due.Sort((a, b) => a.At.CompareTo(b.At));
                foreach (var beat in _due) Resolve(beat);
            }
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                if (_views[i] == null) _views.RemoveAt(i);
                else _views[i].Advance(dt);
            }
            _effects.Advance(dt);
            UpdateHover(dt);
        }

        private void Resolve(Beat beat)
        {
            _fighters.TryGetValue(beat.Actor, out var actor);
            BattleFighterView target = null;
            if (beat.Target != null) _fighters.TryGetValue(beat.Target, out target);
            switch (beat.Kind)
            {
                case BeatKind.Release when actor != null && target != null:
                    float distance = Vector3.Distance(actor.Chest, target.Chest);
                    float flight = Mathf.Clamp(distance * .075f, .16f, .42f);
                    _effects.Projectile(actor.Chest, target.Chest, flight, UnitView.GetFallbackOreSprite(), .3f);
                    _beats.Add(new Beat(beat.At + flight, BeatKind.Impact, beat.Actor, beat.Target, beat.Damage, true));
                    break;
                case BeatKind.Impact when target != null:
                    Land(actor, target, beat.Target, beat.Damage, beat.Ranged);
                    break;
                case BeatKind.Death when actor != null && !actor.IsDead:
                    Kill(actor);
                    break;
            }
        }

        private void Land(BattleFighterView actor, BattleFighterView target, string targetId, int damage, bool ranged)
        {
            if (target.IsDead) return;
            _health.TryGetValue(targetId, out int health);
            health = Mathf.Max(0, health - damage);
            _health[targetId] = health;
            bool kill = health == 0;
            bool weak = damage <= 1;
            bool heavy = !weak && damage >= Mathf.Max(5, target.MaxHp / 5);
            var from = actor != null ? actor.Ground : target.Ground + Vector3.left;

            target.ReceiveHit(damage, health, from, kill ? 0f : heavy ? .09f : .05f);
            if (actor != null && heavy) actor.Hold(.07f);
            _effects.Spark(target.Chest, weak ? new Color(.85f, .9f, 1f, 1f) : new Color(1f, .95f, .72f, 1f),
                heavy || kill ? 1.25f : weak ? .6f : .9f);
            // a thrown rock always shatters; a swing only throws up earth when it hits hard
            var away = target.Ground - from;
            if (ranged)
                _effects.Debris(target.Chest, target.Ground.y, away, ArenaFx.DebrisStone, 3, heavy || kill ? 6 : 4);
            else if (heavy || kill)
                _effects.Debris(target.Ground + Vector3.up * .15f, target.Ground.y, away, ArenaFx.DebrisEarth, 3, 6);
            if (heavy || kill) _effects.HitDust(target.Ground);

            var color = weak ? WeakColor : target.IsEnemy ? DealtColor : TakenColor;
            float size = kill ? 7.5f : heavy ? 6.4f : weak ? 4.2f : 5.4f;
            _numberSide = (_numberSide + 1) % 3;
            _effects.Number(target.AboveHead + Vector3.up * .15f, $"-{damage}", color, size, (_numberSide - 1) * .3f);

            GameAudio.Play(weak ? Sfx.Block : heavy ? Sfx.HitHeavy : Sfx.Hit, 1f, heavy ? .9f : 1f);
            if (kill) Kill(target);
            else if (heavy) CameraShake.Kick(_camera, .3f);
        }

        private void Kill(BattleFighterView fighter)
        {
            fighter.Die();
            // the report's own Death event only backs up a missed count; once dead it must not hold the verdict
            _beats.RemoveAll(beat => beat.Kind == BeatKind.Death && _fighters.TryGetValue(beat.Actor, out var view) &&
                                     view == fighter);
            _effects.GroundPing(fighter.Ground, fighter.IsEnemy ? new Color(1f, .4f, .3f, 1f) : new Color(.5f, .75f, 1f, 1f), 1.4f, .6f);
            _effects.DeathCloud(fighter.Ground, .1f);
            GameAudio.Play(Sfx.Death, 1f, fighter.Kind == UnitKind.Troll ? .8f : .95f);
            CameraShake.Kick(_camera, .55f);
            KillLanded?.Invoke();
        }

        private BattleFighterView CreateFighter(string name, string id, UnitKind kind, bool enemy, Cell cell)
        {
            UnitDefinition definition = null;
            if (_catalog != null)
            {
                try { definition = _catalog.GetUnit(kind); }
                catch (ArgumentException) { }
            }
            var view = new GameObject(name, typeof(BattleFighterView)).GetComponent<BattleFighterView>();
            view.transform.SetParent(transform, false);
            view.Init(id, kind, enemy, cell, CellWorldPosition(cell), ContentPrefabs.Unit(definition),
                definition != null ? definition.CombatHealth : 1, _camera);
            _views.Add(view);
            return view;
        }

        private void Build()
        {
            var enemyZone = new HashSet<Cell>(_mission.EnemyDeployment);
            var grass = NewMaterial(new Color32(146, 191, 80, 255));
            var stone = NewMaterial(new Color32(92, 107, 125, 255));
            var normal = NewMaterial(new Color32(157, 198, 98, 255));
            var blue = NewMaterial(new Color32(125, 177, 198, 255));
            var red = NewMaterial(new Color32(196, 143, 120, 255));
            var blocked = NewMaterial(new Color32(130, 137, 133, 255));
            _outlineMaterial = NewMaterial(new Color32(224, 234, 200, 255));

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int row = 0; row < _board.Height; row++)
            for (int column = 0; column < _board.Width; column++)
            {
                var cell = new Cell(column, row);
                var center = CellWorldPosition(cell);
                minX = Mathf.Min(minX, center.x);
                maxX = Mathf.Max(maxX, center.x);
                minZ = Mathf.Min(minZ, center.z);
                maxZ = Mathf.Max(maxZ, center.z);
            }

            var middle = new Vector3((minX + maxX) * .5f, _origin.y, (minZ + maxZ) * .5f);
            var size = new Vector3(maxX - minX + 2.8f, .65f, maxZ - minZ + 2.8f);
            BoardCenter = middle;
            BoardSize = new Vector2(maxX - minX + HexAcrossFlats, maxZ - minZ + 2f * Radius);
            _bounds = new Bounds(middle, new Vector3(size.x, 2f, size.z));

            if (_mission.EnvironmentPrefab != null)
            {
                var decoration = Instantiate(_mission.EnvironmentPrefab, middle, Quaternion.identity, transform);
                decoration.name = "MissionEnvironment";
                _arena = decoration.GetComponentInChildren<BattleArenaSet>(true);
            }
            if (_arena == null)
            {
                MakeCube("CliffBase", middle + Vector3.down * .83f, size + new Vector3(.32f, .45f, .32f), stone);
                MakeCube("GrassSurface", middle + Vector3.down * .42f, size, grass);
            }

            for (int row = 0; row < _board.Height; row++)
            for (int column = 0; column < _board.Width; column++)
            {
                var cell = new Cell(column, row);
                var material = _board.IsBlocked(cell) ? blocked :
                    _board.CanPlace(cell) ? blue : enemyZone.Contains(cell) ? red : normal;
                MakeCell(cell, material, _board.CanPlace(cell), enemyZone.Contains(cell));
                if (_board.IsBlocked(cell)) MakeObstacle(cell, stone);
            }

            _hover = new GameObject("CellHover", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            _hover.transform.SetParent(transform, false);
            _hover.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _hover.sprite = FeelSprites.Hex;
            _hover.sortingOrder = 3;
            _hover.enabled = false;

            for (int i = 0; i < _mission.Enemies.Count; i++)
            {
                var enemy = _mission.Enemies[i];
                var view = CreateFighter($"Enemy_{enemy.Cell.X}_{enemy.Cell.Y}", $"enemy-{i:D3}", enemy.Kind, true, enemy.Cell);
                view.PopIn(.25f + i * .09f);
                _enemies.Add(view);
            }
        }

        private void MakeCell(Cell cell, Material material, bool playerZone, bool enemyZone)
        {
            var go = new GameObject($"Hex_{cell.X}_{cell.Y}", typeof(MeshFilter), typeof(MeshRenderer),
                typeof(MeshCollider), typeof(BattleCellView));
            go.transform.SetParent(transform, false);
            go.transform.position = CellWorldPosition(cell) + Vector3.up * .025f;
            var mesh = new Mesh { name = "BattleHex" };
            var vertices = new Vector3[7];
            var triangles = new int[18];
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (30f + i * 60f);
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * Radius, 0f, Mathf.Sin(angle) * Radius);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % 6 + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            _meshes.Add(mesh);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            go.GetComponent<BattleCellView>().Init(cell);

            // Arena: the tile model shows the cell (its rim draws the grid); the flat hex only takes clicks.
            var tile = _arena != null ? _arena.TileFor(cell, playerZone, enemyZone) : null;
            if (tile != null)
            {
                var view = Instantiate(tile, go.transform);
                view.name = "Tile";
                view.transform.localPosition = Vector3.down * .025f;      // tile pivot sits on the grass
                view.transform.localRotation = Quaternion.Euler(0f, 60f * BattleArenaSet.TurnFor(cell), 0f);
                go.GetComponent<MeshRenderer>().enabled = false;
                return;
            }

            var line = new GameObject("Outline", typeof(LineRenderer));
            line.transform.SetParent(go.transform, false);
            var renderer = line.GetComponent<LineRenderer>();
            renderer.useWorldSpace = false;
            renderer.loop = true;
            renderer.positionCount = 6;
            renderer.widthMultiplier = .035f;
            renderer.sharedMaterial = _outlineMaterial;
            for (int i = 0; i < 6; i++)
                renderer.SetPosition(i, vertices[i + 1] + Vector3.up * .012f);
        }

        private void MakeObstacle(Cell cell, Material fallback)
        {
            var position = CellWorldPosition(cell);
            if (_mission.ObstaclePrefab != null)
            {
                var obstacle = Instantiate(_mission.ObstaclePrefab, position, Quaternion.identity, transform);
                obstacle.name = $"Obstacle_{cell.X}_{cell.Y}";
                return;
            }
            var arenaObstacle = _arena != null ? _arena.ObstacleFor(cell) : null;
            if (arenaObstacle != null)
            {
                var turn = Quaternion.Euler(0f, 60f * BattleArenaSet.TurnFor(cell) + 20f, 0f);
                var obstacle = Instantiate(arenaObstacle, position, turn, transform);
                obstacle.name = $"Obstacle_{cell.X}_{cell.Y}";
                return;
            }
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = $"Obstacle_{cell.X}_{cell.Y}";
            rock.transform.SetParent(transform, false);
            rock.transform.position = position + Vector3.up * .45f;
            rock.transform.localScale = new Vector3(1.35f, .9f, 1.25f);
            rock.GetComponent<MeshRenderer>().sharedMaterial = fallback;
            var collider = rock.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
        }

        private void MakeCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(transform, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = cube.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
        }

        private Material NewMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            _materials.Add(material);
            return material;
        }

        private void Update()
        {
            if (_camera == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            // only battle UI blocks the board: the hidden colony camera's 2D raycaster still hits colony units
            if (UIInputUtils.IsPointerOverInteractiveUI()) return;
            if (TryPointedCell(out var cell)) CellClicked?.Invoke(cell);
        }

        private bool TryPointedCell(out Cell cell)
        {
            cell = default;
            if (_camera == null || Mouse.current == null) return false;
            var ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var hits = Physics.RaycastAll(ray, 500f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var view = hits[i].collider.GetComponent<BattleCellView>();
                if (view == null) continue;
                cell = view.Cell;
                return true;
            }
            return false;
        }

        private void UpdateHover(float dt)
        {
            _hoverPop = Mathf.MoveTowards(_hoverPop, 0f, dt / .2f);
            Cell? pointed = null;
            if (PlacementHover && !UIInputUtils.IsPointerOverInteractiveUI() && TryPointedCell(out var cell) &&
                _board.Contains(cell))
                pointed = cell;
            if (pointed == null)
            {
                _hover.enabled = false;
                _hoverCell = null;
                return;
            }
            if (_hoverCell != pointed) _hoverPop = Mathf.Max(_hoverPop, .6f);
            _hoverCell = pointed;
            var target = pointed.Value;
            // another fighter's cell: a click picks that fighter up (gold); own cell or a free one: place (white)
            bool other = false;
            foreach (var pair in _allies)
                if (pair.Value != null && pair.Value.Cell == target && pair.Key != _selectedAlly) other = true;
            _hover.enabled = true;
            _hover.color = other ? HoverSwap : !_board.CanPlace(target) ? HoverInvalid : HoverValid;
            _hover.transform.position = CellWorldPosition(target) + Vector3.up * .06f;
            _hover.transform.localScale = Vector3.one * (2f * Radius * (.95f + _hoverPop * .08f));
        }

        private void OnDestroy()
        {
            foreach (var material in _materials)
                if (material != null) Destroy(material);
            foreach (var mesh in _meshes)
                if (mesh != null) Destroy(mesh);
        }

        private enum BeatKind { Release, Impact, Death }

        /// <summary>Something the report says happened, shown a moment later than its report time.</summary>
        private readonly struct Beat
        {
            public readonly float At;
            public readonly BeatKind Kind;
            public readonly string Actor;
            public readonly string Target;
            public readonly int Damage;
            /// <summary>The blow was thrown, not swung.</summary>
            public readonly bool Ranged;

            public Beat(float at, BeatKind kind, string actor, string target, int damage, bool ranged)
            {
                At = at;
                Kind = kind;
                Actor = actor;
                Target = target;
                Damage = damage;
                Ranged = ranged;
            }
        }
    }
}
