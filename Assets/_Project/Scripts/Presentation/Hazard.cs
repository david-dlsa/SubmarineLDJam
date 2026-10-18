using SubmarineLDJam.Domain.Levels;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Put on a collider that hurts the submarine. Pick what kind it is.
    /// Color and icon come from the HazardStyle asset (per kind). The hazard blinks slowly.
    /// </summary>
    public sealed class Hazard : MonoBehaviour
    {
        [field: SerializeField] public CollisionKind Kind { get; private set; } = CollisionKind.Obstacle;

        private SpriteRenderer _target; // the renderer that is tinted and blinks
        private Color _color;
        private float _phase;

        private void Awake()
        {
            var body = GetComponent<SpriteRenderer>();
            if (body == null) return;

            var style = HazardStyle.Current;
            var look = style.For(Kind);
            _color = look.color;
            _target = body;

            if (look.sprite != null)
            {
                // The icon sits on a child, at the same world size as the object, so the collider does not change.
                var size = body.bounds.size;
                var iconSize = look.sprite.bounds.size;
                var parentScale = transform.lossyScale;

                var icon = new GameObject("Icon") { layer = gameObject.layer };
                icon.transform.SetParent(transform, false);
                icon.transform.localScale = new Vector3(
                    size.x / iconSize.x / Mathf.Max(0.0001f, parentScale.x),
                    size.y / iconSize.y / Mathf.Max(0.0001f, parentScale.y), 1f);

                _target = icon.AddComponent<SpriteRenderer>();
                _target.sprite = look.sprite;
                _target.sortingLayerID = body.sortingLayerID;
                _target.sortingOrder = body.sortingOrder + 1;
                body.enabled = false;
            }

            _target.color = _color;
            _phase = transform.position.x * 1.7f + transform.position.y * 2.3f; // so hazards do not blink in step
        }

        private void Update()
        {
            if (_target == null) return;

            var style = HazardStyle.Current;
            var color = _color;
            if (style.blinkHz > 0f)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * style.blinkHz * Mathf.PI * 2f + _phase);
                color.a = _color.a * Mathf.Lerp(style.blinkMinAlpha, 1f, wave);
            }
            _target.color = color;
        }
    }
}
