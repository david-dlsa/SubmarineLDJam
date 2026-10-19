using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// A ring (or a filled disc) that grows from one radius to another and fades out.
    /// Made in code by the bombs. It is on the Sonar layer, so the radar sees it.
    /// Looping pulse: repeats until the object it follows is gone. One shot: removes itself at the end.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RadialPulse : MonoBehaviour
    {
        private static Sprite _thinRing, _thickRing, _disc;
        private static Material _material;

        private SpriteRenderer _renderer;
        private Transform _follow;
        private Color _color;
        private float _fromRadius, _toRadius, _seconds, _pause;
        private bool _loop, _filled;
        private float _startTime;
        private float _spriteDiameter;

        /// <param name="follow">Moves with this object. Loop pulses stop when it is destroyed.</param>
        public static RadialPulse Spawn(Transform parent, Vector3 position, int layer, Transform follow,
            Color color, float fromRadius, float toRadius, float seconds,
            bool loop = false, float pauseSeconds = 0f, bool filled = false, bool thick = false)
        {
            var go = new GameObject(filled ? "Flash" : "Pulse") { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var pulse = go.AddComponent<RadialPulse>();
            pulse._follow = follow;
            pulse._color = color;
            pulse._fromRadius = fromRadius;
            pulse._toRadius = toRadius;
            pulse._seconds = Mathf.Max(0.05f, seconds);
            pulse._pause = Mathf.Max(0f, pauseSeconds);
            pulse._loop = loop;
            pulse._filled = filled;
            pulse._startTime = Time.time;

            var sprite = filled ? (_disc ??= RadarSprites.Disc(256))
                : thick ? (_thickRing ??= RadarSprites.Ring(256, 0.1f))
                : (_thinRing ??= RadarSprites.Ring(256, 0.02f));

            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
            pulse._renderer = go.GetComponent<SpriteRenderer>();
            pulse._renderer.sprite = sprite;
            pulse._renderer.sharedMaterial = _material; // unlit, so the radar camera shows the exact color
            pulse._renderer.sortingOrder = 1;
            pulse._spriteDiameter = sprite.bounds.size.x;
            pulse.Apply(0f);
            return pulse;
        }

        private void Update()
        {
            if (_loop && _follow == null)
            {
                Destroy(gameObject);
                return;
            }

            if (_follow != null) transform.position = _follow.position;

            float t = (Time.time - _startTime) / _seconds;
            if (t >= 1f)
            {
                if (!_loop)
                {
                    Destroy(gameObject);
                    return;
                }

                if (t < 1f + _pause / _seconds)
                {
                    _renderer.enabled = false;
                    return;
                }

                _startTime = Time.time;
                t = 0f;
            }

            _renderer.enabled = true;
            Apply(t);
        }

        private void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t); // fast at first, slow at the end
            float radius = Mathf.Lerp(_fromRadius, _toRadius, eased);

            float parentScale = transform.parent != null ? Mathf.Max(0.0001f, transform.parent.lossyScale.x) : 1f;
            float scale = radius * 2f / _spriteDiameter / parentScale;
            transform.localScale = new Vector3(scale, scale, 1f);

            var color = _color;
            float fade = 1f - t;
            color.a = _color.a * (_filled ? fade * fade : fade);
            _renderer.color = color;
        }
    }
}
