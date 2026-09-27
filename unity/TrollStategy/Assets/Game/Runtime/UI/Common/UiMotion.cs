using System;
using System.Runtime.CompilerServices;
using TrollStrategy.Presentation.Feel;
using UnityEngine;
using UnityEngine.UIElements;

namespace TrollStrategy.UI
{
    /// <summary>
    /// Short reactions for HUD elements (punch, pop-in, nudge, colour flash) on unscaled time. Each writes
    /// an inline style while it runs and clears it at the end, so USS stays the resting look. A newer
    /// reaction on the same property replaces the running one. Elements outside a panel are left alone.
    /// </summary>
    public static class UiMotion
    {
        private const long FrameMs = 16;

        private enum Channel
        {
            Scale,
            Translate,
            Color
        }

        private sealed class Motion
        {
            public readonly IVisualElementScheduledItem[] Running = new IVisualElementScheduledItem[3];
            public Color RestColor;
            public bool Flashing;
        }

        private static readonly ConditionalWeakTable<VisualElement, Motion> s_motions = new();

        /// <summary>Uniform spring: a counter changed, something was confirmed.</summary>
        public static void Punch(VisualElement element, float strength = .12f, float duration = .28f) =>
            Run(element, Channel.Scale, duration,
                t => element.style.scale = new Scale(Vector3.one * (1f + strength * Ease.Spring(t))),
                () => element.style.scale = StyleKeyword.Null);

        /// <summary>Grows from nothing with a small overshoot: a panel appeared.</summary>
        public static void PopIn(VisualElement element, float duration = .3f) =>
            Run(element, Channel.Scale, duration,
                t => element.style.scale = new Scale(Vector3.one * Mathf.Max(.001f, Ease.OutBack(t, 2.2f))),
                () => element.style.scale = StyleKeyword.Null);

        /// <summary>Side-to-side wobble: "no". Do not use on elements centred with a USS translate.</summary>
        public static void Nudge(VisualElement element, float amplitude = 8f, float duration = .35f) =>
            Run(element, Channel.Translate, duration,
                t => element.style.translate = new Translate(amplitude * Ease.Spring(t, 2.5f), 0f),
                () => element.style.translate = StyleKeyword.Null);

        /// <summary>Tints the element's text and fades back to its styled colour.</summary>
        public static void Flash(VisualElement element, Color color, float duration = .3f)
        {
            if (element?.panel == null) return;
            var motion = s_motions.GetOrCreateValue(element);
            if (!motion.Flashing) motion.RestColor = element.resolvedStyle.color;
            motion.Flashing = true;
            var rest = motion.RestColor;
            Run(element, Channel.Color, duration,
                t => element.style.color = Color.Lerp(color, rest, Ease.OutCubic(t)),
                () =>
                {
                    motion.Flashing = false;
                    element.style.color = StyleKeyword.Null;
                });
        }

        private static void Run(VisualElement element, Channel channel, float duration, Action<float> apply,
            Action finish)
        {
            if (element?.panel == null) return;
            var motion = s_motions.GetOrCreateValue(element);
            int slot = (int)channel;
            motion.Running[slot]?.Pause();

            float start = Time.unscaledTime;
            float length = Mathf.Max(.01f, duration);
            IVisualElementScheduledItem item = null;
            item = element.schedule.Execute(() =>
            {
                float t = (Time.unscaledTime - start) / length;
                if (t < 1f)
                {
                    apply(t);
                    return;
                }
                item.Pause();
                if (motion.Running[slot] == item) motion.Running[slot] = null;
                finish();
            }).Every(FrameMs);
            motion.Running[slot] = item;
            apply(0f);
        }
    }
}
