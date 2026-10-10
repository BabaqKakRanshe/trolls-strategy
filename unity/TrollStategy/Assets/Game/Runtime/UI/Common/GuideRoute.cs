using System.Collections.Generic;
using UnityEngine;

namespace TrollStrategy.UI
{
    /// <summary>
    /// The arrow the tutorial pointer draws while the player picks where the goods go: a dotted track from the source
    /// up and over to the pointer, a notched head there, and tokens of the goods riding the track toward the head, as
    /// the goods will. Shapes only, in the layer's coordinates; <see cref="GuideOverlay"/> paints them in the look of
    /// the tutorial's touch.
    /// </summary>
    public static class GuideRoute
    {
        /// <summary>Nearer to the source than this, the pointer is on the source itself: no arrow.</summary>
        public const float MinLength = 56f;
        /// <summary>The head's corners: its tip (the pointer), a barb, the notch the track runs into, the other barb.</summary>
        public const int HeadCorners = 4;
        // a long head: a short wide one reads as pointing wherever its sharpest corner happens to stand
        public const float HeadLength = 42f;
        public const float HeadHalfWidth = 16f;
        public const float NotchDepth = 9f;
        /// <summary>How round each of the head's corners is: the tip the sharpest, so the eye finds it.</summary>
        public static IReadOnlyList<float> HeadRounding { get; } = new[] { 1.5f, 3.5f, 3f, 3.5f };
        /// <summary>The track's dots, this far apart from a little way out of the source to the head's notch.</summary>
        public const float TrackSpacing = 14f;
        public const float TrackRadius = 3.4f;
        private const float TrackStart = 10f;
        /// <summary>A token of the goods at full size, how far apart the tokens ride and how fast, in the layer's pixels.</summary>
        public const float TokenRadius = 16f;
        public const float TokenSpacing = 120f;
        public const float TokenSpeed = 45f;
        // a token grows over this much of the road out of the source and shrinks over this much into the head
        private const float TokenGrow = 40f;
        private const float TokenShrink = 30f;
        private const int Samples = 48;
        // the curve rises by this share of its length, up to a limit
        private const float Lift = .35f;
        private const float LiftMax = 140f;

        // the curve as points and the length along it up to each; the arrow is traced on the main thread only
        private static readonly Vector2[] Points = new Vector2[Samples + 1];
        private static readonly float[] Lengths = new float[Samples + 1];

        /// <summary>A token of the goods on its way.</summary>
        public readonly struct Token
        {
            public Token(Vector2 centre, float radius, int turn)
            {
                Centre = centre;
                Radius = radius;
                Turn = turn;
            }

            public Vector2 Centre { get; }
            public float Radius { get; }
            /// <summary>Which in turn of the goods it carries: one more than the token ahead, kept all the way.</summary>
            public int Turn { get; }
        }

        /// <summary>
        /// The arrow from <paramref name="from"/> to <paramref name="to"/> at <paramref name="time"/> seconds: the
        /// track's dots and the tokens from the source on, put into <paramref name="track"/> and
        /// <paramref name="tokens"/>, and the head's <see cref="HeadCorners"/> corners, put into <paramref name="head"/>.
        /// False, with no dots and no tokens, when the pointer is too near the source for an arrow.
        /// </summary>
        public static bool Trace(Vector2 from, Vector2 to, float time, List<Vector2> track, List<Token> tokens,
            Vector2[] head)
        {
            track.Clear();
            tokens.Clear();
            float chord = Vector2.Distance(from, to);
            if (chord < MinLength) return false;
            var control = (from + to) / 2f + new Vector2(0f, -Mathf.Min(chord * Lift, LiftMax));
            for (int i = 0; i <= Samples; i++)
            {
                Points[i] = Bezier(from, control, to, i / (float)Samples);
                Lengths[i] = i == 0 ? 0f : Lengths[i - 1] + Vector2.Distance(Points[i - 1], Points[i]);
            }
            float length = Lengths[Samples];

            // the head lies along the curve's last stretch: its barbs' back on the curve, its tip at the pointer
            var back = At(length - HeadLength);
            var axis = (to - back).normalized;
            var side = new Vector2(-axis.y, axis.x) * HeadHalfWidth;
            head[0] = to;
            head[1] = back + side;
            head[2] = back + axis * NotchDepth;
            head[3] = back - side;

            float notchAt = length - HeadLength + NotchDepth;
            for (float at = TrackStart; at < notchAt; at += TrackSpacing) track.Add(At(at));

            // the tokens set out one after another; the newest is the one nearest the source
            float travelled = time * TokenSpeed;
            int newest = Mathf.FloorToInt(travelled / TokenSpacing);
            float nearest = travelled - newest * TokenSpacing;
            for (int k = 0; nearest + k * TokenSpacing < notchAt; k++)
            {
                float at = nearest + k * TokenSpacing;
                float size = Mathf.SmoothStep(0f, 1f, at / TokenGrow) * Mathf.SmoothStep(0f, 1f, (notchAt - at) / TokenShrink);
                if (size > .05f) tokens.Add(new Token(At(at), TokenRadius * size, newest - k));
            }
            return true;
        }

        // the point that far along the curve
        private static Vector2 At(float along)
        {
            int i = 1;
            while (i < Samples && Lengths[i] < along) i++;
            float span = Lengths[i] - Lengths[i - 1];
            return Vector2.Lerp(Points[i - 1], Points[i], span > 0f ? Mathf.Clamp01((along - Lengths[i - 1]) / span) : 0f);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
