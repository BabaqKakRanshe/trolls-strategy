using System;
using TrollStrategy.Content;

namespace TrollStrategy.Domain
{
    /// <summary>What the colony owns of one square block of land.</summary>
    public enum LandBlock
    {
        /// <summary>Not bought: nothing stands or walks here.</summary>
        Unowned = 0,
        /// <summary>Bought, still forest, stones or meadow: nothing is built here until it is cleared.</summary>
        Wild = 1,
        /// <summary>Cleared: buildings, creatures and routes may use it.</summary>
        Cleared = 2
    }

    /// <summary>
    /// The colony's land: the grid split into square blocks of <see cref="BlockSize"/> cells, bought one by one
    /// next to the land already owned and then cleared over time. Block (x, y) holds cells x*size .. x*size+size-1
    /// on both axes; its index is y * BlocksPerSide + x.
    /// </summary>
    [Serializable]
    public sealed class LandState
    {
        private readonly LandBlock[] _blocks;
        // clearing timers in milliseconds of colony time: what is left and what the clearing took at the start
        private readonly int[] _clearLeftMs;
        private readonly int[] _clearTotalMs;

        public LandState(int blocksPerSide, int blockSize)
        {
            if (blocksPerSide < 1) throw new ArgumentOutOfRangeException(nameof(blocksPerSide));
            if (blockSize < 1) throw new ArgumentOutOfRangeException(nameof(blockSize));
            BlocksPerSide = blocksPerSide;
            BlockSize = blockSize;
            _blocks = new LandBlock[blocksPerSide * blocksPerSide];
            _clearLeftMs = new int[_blocks.Length];
            _clearTotalMs = new int[_blocks.Length];
        }

        public int BlocksPerSide { get; }
        public int BlockSize { get; }
        public int Count => _blocks.Length;
        /// <summary>Blocks bought so far (the start zone is not counted); the next price grows with it.</summary>
        public int Purchases { get; set; }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < BlocksPerSide && y < BlocksPerSide;

        public LandBlock Block(int x, int y) => Inside(x, y) ? _blocks[y * BlocksPerSide + x] : LandBlock.Unowned;

        public bool IsOwned(int x, int y) => Block(x, y) != LandBlock.Unowned;

        /// <summary>A wild block whose clearing has started and not finished.</summary>
        public bool IsClearing(int x, int y) => Inside(x, y) && _clearLeftMs[y * BlocksPerSide + x] > 0;

        public float ClearSecondsLeft(int x, int y) => Inside(x, y) ? _clearLeftMs[y * BlocksPerSide + x] / 1000f : 0f;

        /// <summary>Milliseconds of colony time the block's clearing still needs; 0 when it is not being cleared.</summary>
        public int ClearLeftMs(int x, int y) => Inside(x, y) ? _clearLeftMs[y * BlocksPerSide + x] : 0;

        /// <summary>Milliseconds the block's clearing takes in all; 0 when it is not being cleared.</summary>
        public int ClearTotalMs(int x, int y) => Inside(x, y) ? _clearTotalMs[y * BlocksPerSide + x] : 0;

        /// <summary>Share of the clearing done, 0..1; 0 for a block that is not being cleared.</summary>
        public float ClearProgress(int x, int y)
        {
            if (!IsClearing(x, y)) return 0f;
            int i = y * BlocksPerSide + x;
            return _clearTotalMs[i] > 0 ? 1f - _clearLeftMs[i] / (float)_clearTotalMs[i] : 1f;
        }

        /// <summary>Whether the cell lies on cleared land; cells outside the blocks never do.</summary>
        public bool IsCleared(Cell cell)
        {
            if (cell.X < 0 || cell.Y < 0) return false;
            return Block(cell.X / BlockSize, cell.Y / BlockSize) == LandBlock.Cleared;
        }

        public void Set(int x, int y, LandBlock block)
        {
            if (!Inside(x, y)) throw new ArgumentOutOfRangeException($"({x}, {y})");
            int i = y * BlocksPerSide + x;
            _blocks[i] = block;
            _clearLeftMs[i] = 0;
            _clearTotalMs[i] = 0;
        }

        /// <summary>Puts a block back as a saved game had it, its clearing timer included (the save checks the values).</summary>
        public void Restore(int x, int y, LandBlock block, int clearLeftMs, int clearTotalMs)
        {
            if (!Inside(x, y)) throw new ArgumentOutOfRangeException($"({x}, {y})");
            int i = y * BlocksPerSide + x;
            _blocks[i] = block;
            _clearLeftMs[i] = clearLeftMs;
            _clearTotalMs[i] = clearTotalMs;
        }

        internal void StartClearing(int x, int y, int milliseconds)
        {
            int i = y * BlocksPerSide + x;
            _clearLeftMs[i] = milliseconds;
            _clearTotalMs[i] = milliseconds;
        }

        /// <summary>Advances every clearing; returns how many blocks turned Cleared.</summary>
        internal int Advance(int milliseconds)
        {
            int finished = 0;
            for (int i = 0; i < _blocks.Length; i++)
            {
                if (_clearLeftMs[i] <= 0) continue;
                _clearLeftMs[i] = Math.Max(0, _clearLeftMs[i] - milliseconds);
                if (_clearLeftMs[i] > 0) continue;
                _clearTotalMs[i] = 0;
                _blocks[i] = LandBlock.Cleared;
                finished++;
            }
            return finished;
        }

        public LandState Clone()
        {
            var clone = new LandState(BlocksPerSide, BlockSize) { Purchases = Purchases };
            Array.Copy(_blocks, clone._blocks, _blocks.Length);
            Array.Copy(_clearLeftMs, clone._clearLeftMs, _clearLeftMs.Length);
            Array.Copy(_clearTotalMs, clone._clearTotalMs, _clearTotalMs.Length);
            return clone;
        }
    }

    /// <summary>Buying and clearing land, and where land limits building and walking (EconomyConfig: Land*).</summary>
    public static class LandRules
    {
        /// <summary>
        /// The land a new colony starts with: the start blocks cleared, the rest not bought; null when the economy
        /// has no land (then land limits nothing).
        /// </summary>
        public static LandState CreateStart(EconomyConfig economy)
        {
            if (economy == null || !economy.LandEnabled) return null;
            int size = economy.LandBlockSize;
            int side = (Math.Max(economy.GridWidth, economy.GridHeight) + size - 1) / size;
            var land = new LandState(side, size);
            var start = economy.StartLand;
            for (int y = start.y; y < start.y + start.height; y++)
            for (int x = start.x; x < start.x + start.width; x++)
                if (land.Inside(x, y)) land.Set(x, y, LandBlock.Cleared);
            return land;
        }

        /// <summary>Middle cell of the start land; the middle of the grid when the economy has no land.</summary>
        public static Cell StartCenter(EconomyConfig economy)
        {
            if (economy == null) return new Cell(0, 0);
            if (!economy.LandEnabled) return new Cell(economy.GridWidth / 2, economy.GridHeight / 2);
            var start = economy.StartLand;
            int size = economy.LandBlockSize;
            return new Cell((start.x * 2 + start.width) * size / 2, (start.y * 2 + start.height) * size / 2);
        }

        /// <summary>Whether land lets this cell be used: always without land, otherwise only on cleared land.</summary>
        public static bool IsOpen(GameState state, Cell cell) => state.Land == null || state.Land.IsCleared(cell);

        public static int NextPrice(LandState land, EconomyConfig economy) =>
            economy.LandPriceBase + economy.LandPriceStep * (land?.Purchases ?? 0);

        /// <summary>Every rule for buying the block except the price.</summary>
        public static CommandResult ValidatePlace(GameState state, int x, int y)
        {
            var land = state.Land;
            if (land == null) return CommandResult.Fail("Земля здесь не продаётся");
            if (!land.Inside(x, y)) return CommandResult.Fail("За краем острова земли нет");
            if (land.IsOwned(x, y)) return CommandResult.Fail("Эта земля уже ваша");
            if (!land.IsOwned(x + 1, y) && !land.IsOwned(x - 1, y) && !land.IsOwned(x, y + 1) && !land.IsOwned(x, y - 1))
                return CommandResult.Fail("Покупать можно только рядом со своей землёй");
            return CommandResult.Success();
        }

        public static CommandResult ValidateBuy(GameState state, int x, int y, GameContentCatalog catalog)
        {
            var place = ValidatePlace(state, x, y);
            if (!place.Ok) return place;
            int price = NextPrice(state.Land, catalog.Economy);
            return state.Gold < price ? CommandResult.Fail($"Не хватает золота: нужно {price}") : CommandResult.Success();
        }

        /// <summary>The block becomes the colony's wild land at once; rising out of the clouds is only the view.</summary>
        public static CommandResult Buy(GameState state, int x, int y, GameContentCatalog catalog)
        {
            var validation = ValidateBuy(state, x, y, catalog);
            if (!validation.Ok) return validation;
            state.Gold -= NextPrice(state.Land, catalog.Economy);
            state.Land.Set(x, y, LandBlock.Wild);
            state.Land.Purchases++;
            return CommandResult.Success();
        }

        public static CommandResult ValidateClear(GameState state, int x, int y, GameContentCatalog catalog)
        {
            var land = state.Land;
            if (land == null || !land.IsOwned(x, y)) return CommandResult.Fail("Сначала купите эту землю");
            if (land.Block(x, y) == LandBlock.Cleared) return CommandResult.Fail("Эта земля уже расчищена");
            if (land.IsClearing(x, y)) return CommandResult.Fail("Эту землю уже расчищают");
            int price = catalog.Economy.LandClearGold;
            return state.Gold < price ? CommandResult.Fail($"Не хватает золота: нужно {price}") : CommandResult.Success();
        }

        /// <summary>Pays for the clearing and starts its timer; with no clearing time the block is cleared at once.</summary>
        public static CommandResult Clear(GameState state, int x, int y, GameContentCatalog catalog)
        {
            var validation = ValidateClear(state, x, y, catalog);
            if (!validation.Ok) return validation;
            var economy = catalog.Economy;
            state.Gold -= economy.LandClearGold;
            int milliseconds = (int)Math.Round(economy.LandClearSeconds * 1000.0);
            if (milliseconds > 0)
            {
                state.Land.StartClearing(x, y, milliseconds);
                return CommandResult.Success();
            }
            state.Land.Set(x, y, LandBlock.Cleared);
            state.LayoutVersion++;
            return CommandResult.Success();
        }

        /// <summary>Runs the clearing timers; a finished block is cleared and routes are planned again.</summary>
        public static void Tick(GameState state, float deltaSeconds)
        {
            if (state.Land == null || deltaSeconds <= 0f) return;
            if (state.Land.Advance((int)Math.Round(deltaSeconds * 1000.0)) > 0) state.LayoutVersion++;
        }
    }
}
