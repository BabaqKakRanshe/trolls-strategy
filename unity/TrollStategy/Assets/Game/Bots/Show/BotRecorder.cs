using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// Writes the game view, HUD and the bot's cursor included, into an H.264 MP4 with the editor's own encoder: no
    /// package and no ffmpeg. Every frame is copied from the screen at the end of the frame and read back from the
    /// GPU without stalling; frames go into the file in order. The show bot runs the game on a fixed frame clock while
    /// it records (<c>Time.captureFramerate</c>), so the video plays at the game's own pace however fast the editor renders.
    /// </summary>
    public sealed class BotRecorder : IDisposable
    {
        private readonly MediaEncoder _encoder;
        private readonly RenderTexture _screen, _upright;
        private readonly Queue<AsyncGPUReadbackRequest> _pending = new();
        private readonly int _width, _height;
        private const int MaxInFlight = 3;
        private bool _disposed;

        public BotRecorder(string path, int width, int height, int framesPerSecond)
        {
            // H.264 wants even sides
            _width = width & ~1;
            _height = height & ~1;
            FramesPerSecond = framesPerSecond;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            _encoder = new MediaEncoder(path, new VideoTrackAttributes
            {
                frameRate = new MediaRational(framesPerSecond),
                width = (uint)_width,
                height = (uint)_height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.Low
            });
            _screen = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32) { name = "BotShow screen" };
            _upright = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGB32) { name = "BotShow frame" };
            FilePath = path;
        }

        public string FilePath { get; }
        public int FramesPerSecond { get; }
        /// <summary>Frames in the file so far.</summary>
        public int Frames { get; private set; }
        /// <summary>Frames the GPU could not hand back; they are left out.</summary>
        public int Dropped { get; private set; }
        /// <summary>Seconds of video so far.</summary>
        public double Seconds => (double)Frames / FramesPerSecond;
        /// <summary>Times a frame was asked for.</summary>
        public int Calls { get; private set; }
        /// <summary>
        /// Reads each frame back at once instead of asking the GPU for it later: slower, but an editor drawing without
        /// a frame cap outruns the GPU's hand-backs, and frames went missing.
        /// </summary>
        public bool Synchronous { get; set; }

        private Texture2D _frame;

        /// <summary>Takes the frame on the screen. Call at the end of a frame (after the UI has drawn).</summary>
        public void Capture()
        {
            if (_disposed) return;
            Calls++;
            if (_screen.width != Screen.width || _screen.height != Screen.height)
            {
                // the game view changed size: the frame is scaled into the video
                _screen.Release();
                _screen.width = Screen.width;
                _screen.height = Screen.height;
                _screen.Create();
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_screen);
            // the screen copy comes upside down where the GPU starts textures at the top; the encoder reads rows
            // from the bottom, as Texture2D does
            if (SystemInfo.graphicsUVStartsAtTop)
                Graphics.Blit(_screen, _upright, new Vector2(1f, -1f), new Vector2(0f, 1f));
            else
                Graphics.Blit(_screen, _upright);
            if (Synchronous)
            {
                var active = RenderTexture.active;
                RenderTexture.active = _upright;
                _frame ??= new Texture2D(_width, _height, TextureFormat.RGBA32, false) { name = "BotShow readback" };
                _frame.ReadPixels(new Rect(0, 0, _width, _height), 0, 0, false);
                RenderTexture.active = active;
                _encoder.AddFrame(_frame);
                Frames++;
                return;
            }
            _pending.Enqueue(AsyncGPUReadback.Request(_upright, 0, TextureFormat.RGBA32));
            Drain(false);
            // an editor drawing without a frame cap outruns the GPU's hand-backs: a few in flight, the rest waited for,
            // so no frame of the video is lost
            while (_pending.Count > MaxInFlight)
            {
                _pending.Peek().WaitForCompletion();
                Drain(false);
            }
        }

        /// <summary>The last frame taken as a PNG, for a finding's picture; null when there is none yet.</summary>
        public byte[] StillPng()
        {
            var request = AsyncGPUReadback.Request(_upright, 0, TextureFormat.RGBA32);
            request.WaitForCompletion();
            if (request.hasError) return null;
            return ImageConversion.EncodeNativeArrayToPNG(request.GetData<byte>(), UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB,
                (uint)_width, (uint)_height).ToArray();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Drain(true);
            _disposed = true;
            _encoder.Dispose();
            _screen.Release();
            _upright.Release();
            if (_frame != null) UnityEngine.Object.DestroyImmediate(_frame);
            UnityEngine.Object.DestroyImmediate(_screen);
            UnityEngine.Object.DestroyImmediate(_upright);
        }

        private void Drain(bool all)
        {
            while (_pending.Count > 0)
            {
                var request = _pending.Peek();
                if (!request.done)
                {
                    if (!all) return;
                    request.WaitForCompletion();
                }
                _pending.Dequeue();
                if (request.hasError)
                {
                    Dropped++;
                    continue;
                }
                _encoder.AddFrame(_width, _height, _width * 4, TextureFormat.RGBA32, request.GetData<byte>());
                Frames++;
            }
        }
    }
}
