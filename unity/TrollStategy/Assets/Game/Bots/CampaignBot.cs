using System;
using TrollStrategy.Application;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Plays a campaign session to the end of the authored quest chain the way a player at the HUD would:
    /// it looks at the snapshot every <see cref="BotProfile.ThinkSeconds"/> of colony time, sends commands,
    /// and lets the session's fixed simulation steps run in between. Nothing here decides an outcome: the
    /// session validates every command, so a run measures the game's rules and numbers as they ship.
    /// </summary>
    public sealed class CampaignBot
    {
        private readonly GameSession _session;
        private readonly BotProfile _profile;
        private readonly int _limitMs;
        private readonly int _stallMs;

        /// <param name="maxColonyMinutes">Stops with <see cref="BotOutcome.TimeLimit"/> after this much colony time.</param>
        /// <param name="stallMinutes">Stops with <see cref="BotOutcome.Stalled"/> when no goal moves this long.</param>
        public CampaignBot(GameSession session, BotProfile profile, float maxColonyMinutes = 240f,
            float stallMinutes = 30f)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (!session.IsCampaign) throw new ArgumentException("Бот играет кампанию: нужна сессия с заданиями");
            if (profile.ThinkSeconds <= 0f) throw new ArgumentException("ThinkSeconds должен быть больше нуля");
            _limitMs = (int)Math.Round(maxColonyMinutes * 60000f);
            _stallMs = (int)Math.Round(stallMinutes * 60000f);
        }

        /// <summary>
        /// Stops after this quest level is claimed; 0 plays the whole authored chain, a level past it plays on into
        /// the repeatable quests (the arena's milestones among them).
        /// </summary>
        public int StopAfterLevel { get; set; }

        /// <summary>Plays to the end on the session's own clock: a look, then the think and click time pass.</summary>
        public BotRun Run()
        {
            var play = Start();
            while (play.Look())
            {
                int now = _session.ActiveTimeMs;
                _session.Advance(play.WaitSeconds);
                if (_session.ActiveTimeMs == now)
                    throw new InvalidOperationException($"Колония не идёт: {SessionDigest.Describe(_session)}");
            }
            return play.Finish();
        }

        /// <summary>The game a look at a time, for a driver that runs the colony clock itself.</summary>
        public BotPlay Start()
        {
            int chain = _session.Catalog.Progression.Quests.Count;
            return new BotPlay(_session, _profile, StopAfterLevel > 0 ? StopAfterLevel : chain, _limitMs, _stallMs);
        }
    }
}
