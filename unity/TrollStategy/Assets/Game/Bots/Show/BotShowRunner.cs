using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TrollStrategy.Application;
using TrollStrategy.Bootstrap;
using TrollStrategy.Content;
using TrollStrategy.Domain;
using TrollStrategy.Presentation.Battle;
using TrollStrategy.Presentation.Island;
using TrollStrategy.UI;
using UnityEngine;

namespace TrollStrategy.Bots
{
    /// <summary>How a show run plays and what it keeps.</summary>
    public sealed class BotShowOptions
    {
        public BotProfile Profile;
        /// <summary>Stops after this quest level is claimed; 0 plays the whole chain.</summary>
        public int StopAfterLevel;
        /// <summary>Writes the video (and runs the game on a fixed frame clock); off only watches.</summary>
        public bool Record = true;
        public int FramesPerSecond = 30;
        /// <summary>The video plays this many times faster than the game: only every n-th frame goes into the file.</summary>
        public int VideoSpeed = 1;
        /// <summary>
        /// The editor draws as fast as it can: the game still steps 1/30 s a frame, so it runs several times faster than
        /// real time; only the HUD's own real-time motions keep their pace (and look slowed down on the video).
        /// </summary>
        public bool Uncapped;
        /// <summary>The hand skips what an impatient player skips: reward reveals, battle replays, the opening flight.</summary>
        public bool SkipAnimations;
        /// <summary>How fast the colony runs while the bot waits for its next look.</summary>
        public float IdleSpeed = 8f;
        /// <summary>How fast the hand moves: 1 is an unhurried player.</summary>
        public float HandSpeed = 1.25f;
        /// <summary>The run ends after this much video, whatever the bot does.</summary>
        public float MaxVideoMinutes = 150f;
        /// <summary>Where the video and the report go.</summary>
        public string Folder;
    }

    /// <summary>
    /// The show bot in the game scene: a bot's looks (<see cref="BotPilot"/>) performed at the HUD by a mouse and a
    /// keyboard of its own (<see cref="BotUiHands"/>), with the cursor, clicks and the bot's thoughts drawn over the
    /// game (<see cref="BotOverlay"/>) and everything recorded (<see cref="BotRecorder"/>). Between looks the colony
    /// runs fast. A move the HUD could not make is written down with a picture and then sent to the session directly,
    /// so one gap in the HUD does not end the game. Runs in Play Mode as coroutines on its overlay's document: an
    /// editor assembly cannot add MonoBehaviours of its own to the scene.
    /// </summary>
    internal sealed class BotShowRunner
    {
        private enum Stage
        {
            Opening,
            Waiting,
            Settling,
            Acting,
            Over
        }

        private BotShowOptions _options;
        private GameBootstrap _game;
        private GameSession _session;
        private BotPilot _pilot;
        private BotMouse _mouse;
        private BotOverlay _overlay;
        private BotHand _hand;
        private BotUiHands _ui;
        private BotRecorder _recorder;
        private Routine _routine;
        private Stage _stage;
        private IGameCommand _meant;
        private ShowEvent _event;
        private readonly List<(IGameCommand Command, CommandResult Result)> _sent = new();
        private readonly Dictionary<string, UnitKind> _kinds = new();
        private double _video, _clockMovedAt;
        private int _frameRate = -1, _vSync = -1;
        private int _clockMs;
        private int _nextLookMs, _problems, _pictures;
        private string _questId, _waiting;

        public BotShowLog Log { get; } = new();
        public bool Finished { get; private set; }
        /// <summary>Why the run stopped early; null when the bot played to its end.</summary>
        public string Failure { get; private set; }
        public BotRun Run => _pilot?.Play.Run;
        /// <summary>Seconds of video written (or of game played, without a recording).</summary>
        public double VideoSeconds => _recorder?.Seconds ?? _video;
        public int Problems => _problems;
        public string VideoFile => _recorder?.FilePath;
        public int Frames => _recorder?.Frames ?? 0;
        /// <summary>The quest the colony is on (or the last one, when the chain is done).</summary>
        public int QuestLevel => _session?.CurrentSnapshot.Progress.Level ?? 0;

        public void Begin(GameBootstrap game, BotShowOptions options)
        {
            _options = options;
            _game = game;
            _session = game.Session;
            Directory.CreateDirectory(options.Folder);
            var hud = UnityEngine.Object.FindAnyObjectByType<ColonyHud>();
            _overlay = new BotOverlay(hud != null && hud.Document != null ? hud.Document.panelSettings : null);
            _overlay.Host.StartCoroutine(Loop());
            _mouse = new BotMouse(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            _hand = new BotHand(_mouse, _overlay, options.Profile.Seed + 7) { Speed = options.HandSpeed };
            _ui = new BotUiHands(game, _hand) { SkipAnimations = options.SkipAnimations };
            _ui.SideMove += text => Log.Add(ShowEventKind.Note, VideoAt, _session.ActiveTimeMs, text);
            _hand.Interrupt = () => _meant != null ? _ui.Interruption(_meant) : null;
            _overlay.Pointer(_mouse.Position);
            var play = new CampaignBot(_session, options.Profile) { StopAfterLevel = options.StopAfterLevel }.Start();
            _pilot = new BotPilot(play);
            _session.OnCommandResolved += NoteSent;
            if (options.Record)
            {
                Time.captureFramerate = options.FramesPerSecond;
                // a fast editor would run the HUD's real-time motions in slow motion on the video: draw at the video's pace
                _frameRate = UnityEngine.Application.targetFrameRate;
                _vSync = QualitySettings.vSyncCount;
                QualitySettings.vSyncCount = 0;
                UnityEngine.Application.targetFrameRate = options.Uncapped ? -1 : options.FramesPerSecond;
                _recorder = new BotRecorder(Path.Combine(options.Folder, "video.mp4"), Screen.width, Screen.height,
                    options.FramesPerSecond) { Synchronous = options.Uncapped };
                _overlay.Host.StartCoroutine(Capture());
            }
            Debug.Log($"[Bot show] экран {Screen.width}×{Screen.height}, {(options.Record ? $"запись {options.FramesPerSecond} к/с" : "без записи")}");
            _overlay.SetTitle($"{options.Profile.Title}: играет мышкой");
            _overlay.SetThought("Начинаю игру", null);
            Log.Add(ShowEventKind.Note, 0, _session.ActiveTimeMs, $"{options.Profile.Title} начинает игру");
            _routine = new Routine(Opening());
            _stage = Stage.Opening;
        }

        // once a frame, after the scene's own updates
        private IEnumerator Loop()
        {
            while (_stage != Stage.Over)
            {
                yield return null;
                Frame();
            }
        }

        private void Frame()
        {
            if (_stage == Stage.Over || _options == null) return;
            float frame = _options.Record ? 1f / _options.FramesPerSecond : Time.unscaledDeltaTime;
            _video += frame;
            _hand.FrameSeconds = frame;
            _overlay.Tick(frame);
            _mouse.KeepCurrent();
            try
            {
                Tick();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                End($"сбой: {exception.Message}");
            }
            if (_stage != Stage.Over) Caption();
        }

        private void Tick()
        {
            if (_video > _options.MaxVideoMinutes * 60f)
            {
                End($"видео дошло до {_options.MaxVideoMinutes:0} мин");
                return;
            }
            switch (_stage)
            {
                case Stage.Opening:
                    _routine.Step();
                    if (!_routine.Done) return;
                    if (_routine.Error != null) Problem("начало игры", _routine.Error.Message);
                    _routine = null;
                    _nextLookMs = _session.ActiveTimeMs;
                    _stage = Stage.Waiting;
                    return;
                case Stage.Waiting:
                    if (_session.ActiveTimeMs < _nextLookMs)
                    {
                        Time.timeScale = _options.IdleSpeed;
                        if (_session.ActiveTimeMs != _clockMs)
                        {
                            _clockMs = _session.ActiveTimeMs;
                            _clockMovedAt = _video;
                        }
                        else if (_video - _clockMovedAt > 3)
                        {
                            // the colony's clock stands (a menu, a reward over the map): clear the screen as a player would
                            _clockMovedAt = _video;
                            Time.timeScale = 1f;
                            _routine = new Routine(_ui.Settle(null));
                            _stage = Stage.Settling;
                        }
                        return;
                    }
                    Time.timeScale = 1f;
                    _waiting = null;
                    _pilot.BeginLook();
                    Next();
                    return;
                case Stage.Acting:
                    _routine.Step();
                    if (_routine.Done) Resolve();
                    return;
                case Stage.Settling:
                    _routine.Step();
                    if (!_routine.Done) return;
                    if (_routine.Error != null) Problem("экран перед ожиданием", _routine.Error.Message);
                    _routine = null;
                    _stage = Stage.Waiting;
                    return;
            }
        }

        // after a look stops: the next command, or the look is over
        private void Next()
        {
            NoteQuest();
            if (_pilot.Pending != null)
            {
                StartCommand(_pilot.Pending, _pilot.PendingIntent);
                return;
            }
            if (!_pilot.GoesOn)
            {
                var run = _pilot.Play.Run;
                End(run.Outcome == BotOutcome.Completed ? null : $"бот остановился: {run.StopReason ?? run.Outcome.ToString()}");
                return;
            }
            _nextLookMs = _session.ActiveTimeMs + (int)Math.Round(_options.Profile.ThinkSeconds * 1000f);
            _waiting = BotNarrator.Waiting(_pilot.Play.WaitKind, _pilot.Play.WaitReason);
            var last = Log.Events.LastOrDefault();
            if (last == null || last.Kind != ShowEventKind.Wait || last.Text != _waiting)
                Log.Add(ShowEventKind.Wait, VideoAt, _session.ActiveTimeMs, _waiting);
            _overlay.SetThought(_waiting, null);
            _stage = Stage.Waiting;
        }

        private void StartCommand(IGameCommand command, string intent)
        {
            _meant = command;
            _sent.Clear();
            _kinds.Clear();
            foreach (var unit in _session.CurrentSnapshot.Units) _kinds[unit.Id] = unit.UnitKind;
            string text = BotNarrator.Describe(command, _session);
            // a reason that only repeats the move («Строю: Шахта», зачем: «Шахта») says nothing
            if (intent != null && text.Contains(intent)) intent = null;
            _event = Log.Add(ShowEventKind.Command, VideoAt, _session.ActiveTimeMs, text, intent);
            _overlay.SetThought(text, intent);
            _hand.ResetInterrupts();
            var gesture = _ui.Perform(command);
            _routine = new Routine(Sequence(_ui.Settle(command), gesture ?? Fail($"у рук нет жеста для {command.GetType().Name}")));
            _stage = Stage.Acting;
            Time.timeScale = 1f;
        }

        private void Resolve()
        {
            var error = _routine.Error;
            _routine = null;
            _event.End = VideoAt;
            // a look at the book sends nothing to the game: done when the hands are done
            if (_meant is BotPeek)
            {
                if (error != null) Problem(_event, error.Message, fallback: false);
                _meant = null;
                _event = null;
                _pilot.Answer(CommandResult.Success());
                Next();
                return;
            }
            var match = _sent.LastOrDefault(s => BotShowMatch.Same(_meant, s.Command, KindOf));
            // a move of the same sort the HUD made differently (another cell): the colony has it; it is not sent again
            var similar = match.Command == null
                ? _sent.LastOrDefault(s => s.Result.Ok && s.Command.GetType() == _meant.GetType())
                : default;
            CommandResult answer;
            if (match.Command != null)
            {
                answer = match.Result;
                if (error != null) Problem(_event, $"после хода: {error.Message}", fallback: false);
            }
            else if (similar.Command != null)
            {
                answer = similar.Result;
                Problem(_event, $"интерфейс сделал другой ход того же рода: {BotNarrator.Describe(similar.Command, _session)}",
                    fallback: false);
            }
            else
            {
                string why = error?.Message ?? "интерфейс не отправил этот ход";
                if (_sent.Count > 0) why += $" (отправлено: {string.Join(", ", _sent.Select(s => s.Command.GetType().Name))})";
                Problem(_event, why, fallback: true);
                ClearScreen();
                answer = _session.Dispatch(_meant);
            }
            if (!answer.Ok) _event.Refused = answer.Error;
            _meant = null;
            _event = null;
            _pilot.Answer(answer);
            Next();
        }

        // a failed gesture may leave a mode, a card or a dialog behind: the direct command and the next gesture need a clean screen
        private void ClearScreen()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<BattleSceneController>();
            if (controller != null && controller.isActiveAndEnabled && !(_meant is AcknowledgeBattleCommand))
                controller.gameObject.SendMessage("Close", SendMessageOptions.DontRequireReceiver);
            _game.Interaction.CancelOrClear();
            _game.Interaction.CancelOrClear();
            _game.Interaction.CloseInspect();
            var view = UnityEngine.Object.FindAnyObjectByType<ColonyHud>()?.View;
            if (view == null) return;
            if (view.Arena.IsOpen) view.Arena.Close();
            if (view.Menu.IsOpen) view.Menu.Close();
            if (view.Wiki?.IsOpen == true) view.Wiki.Close();
        }

        private void NoteSent(IGameCommand command, CommandResult result)
        {
            if (_stage == Stage.Acting) _sent.Add((command, result));
        }

        private UnitKind? KindOf(string id)
        {
            foreach (var unit in _session.CurrentSnapshot.Units)
                if (unit.Id == id) return unit.UnitKind;
            return _kinds.TryGetValue(id, out var kind) ? kind : null;
        }

        private void NoteQuest()
        {
            var quest = _session.CurrentSnapshot.Progress.Quest;
            if (quest == null || quest.Id == _questId) return;
            _questId = quest.Id;
            Log.Add(ShowEventKind.Quest, VideoAt, _session.ActiveTimeMs, $"{quest.Level}. «{quest.Title}»");
        }

        private void Caption()
        {
            var quest = _session.CurrentSnapshot.Progress.Quest;
            string where = quest != null ? $"задание {quest.Level}: «{quest.Title}»" : "все задания пройдены";
            _overlay.SetTitle($"{_options.Profile.Title} — {where} — колония {BotShowLog.Clock(_session.ActiveTimeMs / 1000.0)}");
            _overlay.SetBadge(_stage == Stage.Waiting && Time.timeScale > 1.01f ? $"×{Time.timeScale:0} пока жду" : null);
        }

        private void Problem(string what, string why) =>
            Problem(Log.Add(ShowEventKind.Note, VideoAt, _session.ActiveTimeMs, what), why, fallback: false);

        private void Problem(ShowEvent at, string why, bool fallback)
        {
            _problems++;
            at.Problem = why;
            if (fallback) at.Kind = ShowEventKind.Fallback;
            string name = $"problem-{++_pictures:000}.png";
            try
            {
                var png = _recorder?.StillPng();
                if (png != null) File.WriteAllBytes(Path.Combine(_options.Folder, name), png);
                else ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(_options.Folder, name)));
                at.Picture = name;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Bot show] кадр не записан: {exception.Message}");
            }
            Debug.LogWarning($"[Bot show] {BotShowLog.Clock(at.Start)} {at.Text}: {why}");
        }

        /// <summary>The start of a new colony as a player meets it: the welcome window, then the flight over the island.</summary>
        private IEnumerator Opening()
        {
            yield return _hand.Until(() => UnityEngine.Object.FindAnyObjectByType<ColonyHud>()?.View != null, 8f, "интерфейс колонии");
            var view = UnityEngine.Object.FindAnyObjectByType<ColonyHud>().View;
            yield return _hand.Wait(0.8f);
            if (view.Intro.IsOpen)
            {
                _overlay.SetThought("Окно приветствия", "отвечаю на вопросы и начинаю игру");
                // the hints question (if this build asks it): the bot keeps the tutorial pointer on, so the video shows it
                if (Property<bool>(view.Intro, "AsksHints"))
                    yield return _hand.Press(Property<UnityEngine.UIElements.Button>(view.Intro, "HintsOnButton"), "Показывать подсказки");
                if (view.Intro.AsksConsent) yield return _hand.Press(view.Intro.ConsentNoButton, "Не отправлять статистику");
                yield return _hand.Press(view.Intro.PlayButton, "Играть");
                yield return _hand.Until(() => !view.Intro.IsOpen, 2f, "окно закрылось");
            }
            var rig = _game.ColonyCamera != null ? _game.ColonyCamera.GetComponent<IslandCameraRig>() : null;
            if (rig != null && rig.IsPlayingIntro && _options.SkipAnimations)
            {
                // any key lands the flight
                yield return _hand.Key(UnityEngine.InputSystem.Key.Space, "Пробел");
            }
            if (rig != null && rig.IsPlayingIntro)
            {
                _overlay.SetThought("Пролёт над островом", null);
                yield return _hand.Until(() => !rig.IsPlayingIntro, 12f, "камера прилетела");
            }
            yield return _hand.Wait(0.5f);
            // the controls lesson waits for the camera to move and zoom; any new player looks around first anyway
            _overlay.SetThought("Осматриваю остров", "двигаю камеру и приближаю, как новый игрок");
            yield return _hand.LookAround();
        }

        private static T Property<T>(object owner, string name)
        {
            var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.GetValue(owner) is T value ? value : default;
        }

        private static IEnumerator Sequence(params IEnumerator[] steps)
        {
            foreach (var step in steps) yield return step;
        }

        private static IEnumerator Fail(string why)
        {
            throw new GestureFailed(why);
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        private IEnumerator Capture()
        {
            var end = new WaitForEndOfFrame();
            for (int frame = 0; _stage != Stage.Over; frame++)
            {
                yield return end;
                if (_stage != Stage.Over && frame % Math.Max(1, _options.VideoSpeed) == 0) _recorder?.Capture();
            }
        }

        // where the game's moment falls in the video, which may play faster than the game
        private double VideoAt => _video / Math.Max(1, _options.VideoSpeed);

        /// <summary>Ends the run: the video is closed, the mouse goes back to the player and the report is written.</summary>
        public void End(string failure)
        {
            if (_stage == Stage.Over) return;
            _stage = Stage.Over;
            Failure = failure;
            Time.timeScale = 1f;
            Time.captureFramerate = 0;
            if (_vSync >= 0)
            {
                QualitySettings.vSyncCount = _vSync;
                UnityEngine.Application.targetFrameRate = _frameRate;
            }
            if (_session != null) _session.OnCommandResolved -= NoteSent;
            // the run's account closes with the colony as it ends; the look thread waits meanwhile
            try { _pilot?.Play.Finish(); } catch (Exception exception) { Debug.LogException(exception); }
            if (failure != null) Log.Add(ShowEventKind.Note, VideoAt, _session?.ActiveTimeMs ?? 0, "Конец: " + failure);
            else Log.Add(ShowEventKind.Note, VideoAt, _session?.ActiveTimeMs ?? 0, "Конец: бот прошёл свою часть кампании");
            foreach (var e in Log.Events.Where(e => e.End < e.Start)) e.End = e.Start;
            if (_recorder != null)
                Debug.Log($"[Bot show] кадров в видео {_recorder.Frames}, снято {_recorder.Calls}, потеряно {_recorder.Dropped}");
            try { _recorder?.Dispose(); } catch (Exception exception) { Debug.LogException(exception); }
            try { _pilot?.Dispose(); } catch (Exception exception) { Debug.LogException(exception); }
            try { _mouse?.Dispose(); } catch (Exception exception) { Debug.LogException(exception); }
            _overlay?.Dispose();
            Finished = true;
        }

    }
}
