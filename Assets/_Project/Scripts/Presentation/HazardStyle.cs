using System;
using SubmarineLDJam.Domain.Levels;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// How hazards look, per kind. One asset for the whole game: Assets/_Project/Resources/HazardStyle.
    /// Every <see cref="Hazard"/> reads it when it appears, so changing a color or sprite here changes every maze.
    /// No asset = every kind is red.
    /// </summary>
    [CreateAssetMenu(menuName = "Submarine/Hazard Style", fileName = "HazardStyle")]
    public sealed class HazardStyle : ScriptableObject
    {
        [Serializable]
        public struct Look
        {
            [Tooltip("Color of the hazard, on screen and on the radar.")]
            public Color color;
            [Tooltip("Icon / sprite. Empty = keep the sprite of the object. The icon is drawn at the same size as the object.")]
            public Sprite sprite;
        }

        [Header("Look per kind")]
        public Look wall = Red();
        public Look obstacle = Red();
        public Look enemy = Red();
        public Look bomb = Red();

        [Header("Slow blink (all hazards)")]
        [Tooltip("Blinks per second. 0 = no blink.")]
        [Min(0f)] public float blinkHz = 0.5f;
        [Tooltip("Lowest opacity of the blink. 1 = no blink.")]
        [Range(0f, 1f)] public float blinkMinAlpha = 0.35f;

        public Look For(CollisionKind kind)
        {
            switch (kind)
            {
                case CollisionKind.Obstacle: return obstacle;
                case CollisionKind.Enemy: return enemy;
                case CollisionKind.Bomb: return bomb;
                default: return wall;
            }
        }

        private static Look Red() => new Look { color = new Color(1f, 0.25f, 0.25f) };

        private static HazardStyle _current;

        /// <summary>The style in use: the Resources asset, or an all-red one made in memory.</summary>
        public static HazardStyle Current
        {
            get
            {
                if (_current != null) return _current;

                _current = Resources.Load<HazardStyle>("HazardStyle");
                if (_current == null) _current = CreateInstance<HazardStyle>();
                return _current;
            }
        }
    }
}
