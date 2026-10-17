using System;

namespace SubmarineLDJam.Domain.Navigation
{
    /// <summary>Direction in degrees. 0 = up, grows clockwise (like a compass).</summary>
    public readonly struct Heading
    {
        public float Degrees { get; }

        public Heading(float degrees) => Degrees = Normalize(degrees);

        /// <summary>Unit vector X (right is positive).</summary>
        public float X => (float)Math.Sin(Degrees * Math.PI / 180.0);

        /// <summary>Unit vector Y (up is positive).</summary>
        public float Y => (float)Math.Cos(Degrees * Math.PI / 180.0);

        public static float Normalize(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }
}
