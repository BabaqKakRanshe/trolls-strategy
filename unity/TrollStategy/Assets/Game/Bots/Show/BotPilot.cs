using System;
using System.Threading;
using TrollStrategy.Application;
using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Lets a bot's look take as long as its hands do. The planners decide in one go and expect every command's
    /// answer at once; at the HUD a command takes seconds of moving and clicking while the colony runs on. So the look
    /// runs on its own thread and stops at every command: the main thread performs it (through the HUD), answers, and
    /// the look goes on with the colony as it stands then. Only one side runs at a time, so the session is never
    /// touched from two threads: the main thread waits while the look thinks, and the look waits while the main thread
    /// plays frames.
    /// </summary>
    public sealed class BotPilot : IDisposable
    {
        private const int ThinkTimeoutMs = 30000;

        private readonly BotPlay _play;
        private readonly SemaphoreSlim _toBrain = new(0, 1), _toMain = new(0, 1);
        private readonly Thread _thread;
        private IGameCommand _pending;
        private string _pendingIntent;
        private CommandResult _answer;
        private Exception _error;
        private volatile bool _stopping;
        private bool _looking, _goesOn = true;

        private sealed class Stopped : Exception
        {
        }

        public BotPilot(BotPlay play)
        {
            _play = play ?? throw new ArgumentNullException(nameof(play));
            _play.SendThrough(Send);
            _thread = new Thread(Loop, 4 * 1024 * 1024) { IsBackground = true, Name = "Bot look" };
            _thread.Start();
        }

        public BotPlay Play => _play;
        /// <summary>A look is under way: it waits for <see cref="Pending"/> to be answered.</summary>
        public bool Looking => _looking;
        /// <summary>The command the look waits on; null between looks.</summary>
        public IGameCommand Pending => _pending;
        /// <summary>What the bot wants the pending command for, in words; null when the planner gave no reason.</summary>
        public string PendingIntent => _pendingIntent;
        /// <summary>False once a look ended the game (the chain claimed, a stall or the time limit).</summary>
        public bool GoesOn => _goesOn;

        /// <summary>
        /// Starts a look. Returns when the look asks for its first command (<see cref="Pending"/>) or is over.
        /// </summary>
        public void BeginLook()
        {
            if (_looking) throw new InvalidOperationException("Взгляд уже идёт");
            if (!_goesOn) return;
            _looking = true;
            Hand(_toBrain);
        }

        /// <summary>The pending command's answer. Returns when the look asks for the next one or is over.</summary>
        public void Answer(CommandResult result)
        {
            if (_pending == null) throw new InvalidOperationException("Бот ничего не ждёт");
            _answer = result;
            _pending = null;
            _pendingIntent = null;
            Hand(_toBrain);
        }

        public void Dispose()
        {
            if (_stopping) return;
            _stopping = true;
            // the look thread wakes up, sees the stop and leaves; a look in the middle of a command throws out of it
            if (_toBrain.CurrentCount == 0) _toBrain.Release();
            _thread.Join(2000);
        }

        // hands the turn to the look thread and waits until it hands it back
        private void Hand(SemaphoreSlim turn)
        {
            turn.Release();
            if (!_toMain.Wait(ThinkTimeoutMs))
                throw new TimeoutException($"Бот думает дольше {ThinkTimeoutMs / 1000} с");
            if (_error != null)
            {
                var error = _error;
                _error = null;
                _looking = false;
                _goesOn = false;
                throw new InvalidOperationException($"Бот упал во время взгляда: {error.Message}", error);
            }
        }

        // the look thread: one look per turn; between looks and commands it sleeps on the semaphore
        private void Loop()
        {
            while (true)
            {
                _toBrain.Wait();
                if (_stopping) return;
                try
                {
                    _goesOn = _play.Look();
                }
                catch (Stopped)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _error = exception;
                }
                _looking = false;
                if (_stopping) return;
                _toMain.Release();
            }
        }

        // a command from the look: the main thread performs it while this thread waits
        private CommandResult Send(IGameCommand command, string intent)
        {
            _pending = command;
            _pendingIntent = intent;
            _toMain.Release();
            _toBrain.Wait();
            if (_stopping) throw new Stopped();
            return _answer;
        }
    }
}
