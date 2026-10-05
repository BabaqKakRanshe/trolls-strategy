using System;
using System.Collections.Generic;
using TrollStrategy.Application;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// The same bot on two sessions in lockstep. The live session runs on someone else's clock (the game scene's
    /// frames, with the HUD and the views attached); the shadow is a plain session moved to exactly the colony time
    /// of each live look. Before and after every look both colonies must read the same
    /// (<see cref="SnapshotDigest"/>): then the headless runs measure what the game itself does with the same moves.
    /// The first difference ends the check and says where it is.
    /// </summary>
    public sealed class BotParity : IDisposable
    {
        private readonly GameSession _live, _shadow;
        private readonly BotPlay _livePlay, _shadowPlay;
        private bool _inLook, _looked, _over;

        /// <param name="shadow">A new session of the same catalog and layout; it is brought to the live clock.</param>
        public BotParity(GameSession live, GameSession shadow, BotProfile profile, int stopAfterLevel = 0)
        {
            _live = live ?? throw new ArgumentNullException(nameof(live));
            _shadow = shadow ?? throw new ArgumentNullException(nameof(shadow));
            // the scene may have run a few frames before the bot sat down
            if (CatchUp("до первого взгляда"))
            {
                _livePlay = new CampaignBot(live, profile) { StopAfterLevel = stopAfterLevel }.Start();
                _shadowPlay = new CampaignBot(shadow, profile) { StopAfterLevel = stopAfterLevel }.Start();
            }
            else _over = true;
            _live.OnCommandResolved += NoteForeign;
        }

        /// <summary>Looks both bots took with the colonies still the same.</summary>
        public int Looks { get; private set; }
        /// <summary>Where the colonies parted, in words; null while they read the same.</summary>
        public string Difference { get; private set; }
        /// <summary>Commands the live session took between the bot's looks: something in the scene sent them.</summary>
        public List<string> ForeignCommands { get; } = new();
        public bool IsOver => _over;
        /// <summary>The live colony time the next look waits for.</summary>
        public int NextLookMs => _looked ? _livePlay.NextLookMs : _live.ActiveTimeMs;

        /// <summary>
        /// Call after every frame of the live clock: looks once the live colony has reached the next look. False once
        /// the game is over or the colonies parted.
        /// </summary>
        public bool Tick()
        {
            if (_over) return false;
            if (_live.ActiveTimeMs < NextLookMs) return true;
            _looked = true;
            if (!CatchUp("до хода")) return End();
            bool liveGoesOn;
            _inLook = true;
            try { liveGoesOn = _livePlay.Look(); }
            finally { _inLook = false; }
            bool shadowGoesOn = _shadowPlay.Look();
            if (!Same("после хода")) return End();
            Looks++;
            if (liveGoesOn != shadowGoesOn)
            {
                Difference = $"Взгляд {Looks}: {(liveGoesOn ? "в игре бот играет дальше, без сцены закончил" : "в игре бот закончил, без сцены играет дальше")}";
                return End();
            }
            return liveGoesOn || End();
        }

        /// <summary>Both runs as they stand: the live one played in the game, the shadow one without the scene.</summary>
        public (BotRun Live, BotRun Shadow) Finish() =>
            _livePlay == null ? (null, null) : (_livePlay.Finish(), _shadowPlay.Finish());

        public void Dispose() => _live.OnCommandResolved -= NoteForeign;

        private bool End()
        {
            _over = true;
            return false;
        }

        // The shadow goes to the live colony time and both must read the same.
        private bool CatchUp(string when)
        {
            int at = _live.ActiveTimeMs;
            if (_shadow.ActiveTimeMs < at) _shadow.Advance((at - _shadow.ActiveTimeMs) / 1000f);
            if (_shadow.ActiveTimeMs != at)
            {
                Difference = $"Взгляд {Looks + 1}, {when}: в игре колония на {at} мс, без сцены — на {_shadow.ActiveTimeMs} мс";
                return false;
            }
            return Same(when);
        }

        private bool Same(string when)
        {
            string difference = SnapshotDigest.FirstDifference(SnapshotDigest.Lines(_live),
                SnapshotDigest.Lines(_shadow), "в игре", "без сцены");
            if (difference == null) return true;
            Difference = $"Взгляд {Looks + 1}, {_live.ActiveTimeMs / 60000f:0.##} мин колонии, {when}:\n{difference}";
            return false;
        }

        private void NoteForeign(IGameCommand command, CommandResult result)
        {
            if (_inLook) return;
            ForeignCommands.Add($"{_live.ActiveTimeMs / 60000f:0.##} мин: {command.GetType().Name} — " +
                                (result.Ok ? "принята" : $"отказ: {result.Error}"));
        }
    }
}
