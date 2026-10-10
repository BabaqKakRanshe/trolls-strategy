using System;
using System.IO;
using UnityEngine;
using TrollStrategy.Application;
using TrollStrategy.Presentation;

namespace TrollStrategy.Bootstrap
{
    /// <summary>
    /// Runs the colony's autosave (<see cref="SaveGames"/>): once a frame after the colony's step it saves what is
    /// due, and it saves at once whenever the game may be about to end — focus lost (a browser tab too), the OS
    /// pausing the app, quitting, the scene being left for a new colony or a load. A component of its own, so the
    /// battle scene switching the colony off leaves it on; the composer holds timed saves while that scene is open.
    /// It also gives every save its picture of the island (<see cref="IslandThumbnail"/>), taken on the frame of the
    /// save while the colony is on screen; in a battle, on quit and while the scene goes, the last picture taken.
    /// </summary>
    public sealed class SaveKeeper : MonoBehaviour
    {
        private SaveGames _saves;
        private Func<bool> _hold;
        private Func<Camera> _camera;
        private readonly IslandThumbnail _thumbnail = new();
        private byte[] _lastPicture;
        private double _pictureMs;
        private bool _leaving;

        public void Init(SaveGames saves, Func<bool> hold, Func<Camera> camera)
        {
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _hold = hold;
            _camera = camera;
            _saves.HoldAutosave = hold;
            _saves.Thumbnail = Picture;
            _saves.Saved += Log;
        }

        // The first picture into a new texture costs the render pipeline its setup (a quarter of a second in a
        // player): it is taken while the scene loads, so no autosave pays it, and it stands in until the next one.
        private void Start()
        {
            if (_saves != null) Picture();
        }

        private void LateUpdate() => Run(() => _saves?.AutosaveIfDue());

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Flush("focus");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush("pause");
        }

        private void OnApplicationQuit()
        {
            _leaving = true;
            Flush("quit");
        }

        private void OnDestroy()
        {
            _leaving = true;
            Flush("leave");
            _thumbnail.Dispose();
            if (_saves == null) return;
            _saves.Saved -= Log;
            _saves.Thumbnail = null;
            _saves.Dispose();
            _saves = null;
        }

        private void Flush(string reason) => Run(() => _saves?.AutosaveNow(reason));

        private byte[] Picture()
        {
            _pictureMs = 0;
            if (_leaving || (_hold?.Invoke() ?? false)) return _lastPicture;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var picture = _thumbnail.Capture(_camera?.Invoke());
                if (picture != null) _lastPicture = picture;
                _pictureMs = watch.Elapsed.TotalMilliseconds;
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                Debug.LogWarning($"[Saves] no picture of the island: {exception.Message}");
            }
            return _lastPicture;
        }

        private static void Run(Func<SaveResult> save)
        {
            try { save(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void Log(SaveResult result)
        {
            string what = result.Autosave ? $"autosave ({result.Reason})" : "save";
            if (result.Ok)
                Debug.Log($"[Saves] {what} {result.SlotId}: {result.Milliseconds:F1} ms, picture " +
                          (result.Slot?.Thumbnail != null
                              ? $"{result.Slot.Thumbnail.Length} bytes ({(_pictureMs > 0 ? $"{_pictureMs:F1} ms" : "kept")})"
                              : "none"));
            else Debug.LogWarning($"[Saves] {what} {result.SlotId} failed: {result.Error} {result.Detail}");
        }

        /// <summary>
        /// The save folder: Application.persistentDataPath/Saves (IndexedDB on WebGL), the editor's games in
        /// Saves-Editor beside it so they never mix with a player's; -saveDir &lt;folder&gt; (or ?saveDir= on WebGL)
        /// puts them elsewhere, for checks that must not touch the player's saves.
        /// </summary>
        public static string Folder()
        {
            string custom = Argument("saveDir");
            if (!string.IsNullOrEmpty(custom)) return custom;
            return Path.Combine(UnityEngine.Application.persistentDataPath,
                UnityEngine.Application.isEditor ? "Saves-Editor" : "Saves");
        }

        /// <summary>
        /// A launch option: "-name value" on the command line, or "name=value" in the web page's address on WebGL;
        /// null when it is not given.
        /// </summary>
        public static string Argument(string name)
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (string.Equals(args[i], "-" + name, StringComparison.OrdinalIgnoreCase))
                        return args[i + 1];
                string url = UnityEngine.Application.absoluteURL;
                int query = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
                if (query < 0) return null;
                int hash = url.IndexOf('#', query);
                string search = hash < 0 ? url.Substring(query + 1) : url.Substring(query + 1, hash - query - 1);
                foreach (string pair in search.Split('&'))
                {
                    int equals = pair.IndexOf('=');
                    if (equals > 0 && string.Equals(pair.Substring(0, equals), name, StringComparison.OrdinalIgnoreCase))
                        return Uri.UnescapeDataString(pair.Substring(equals + 1));
                }
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                Debug.LogWarning($"[Saves] reading the launch option {name}: {exception.Message}");
            }
            return null;
        }
    }
}
