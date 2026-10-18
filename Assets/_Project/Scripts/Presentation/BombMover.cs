using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Moves a bomb. None = stays still. PointToPoint = floats between point A and point B.
    /// Radial = circles around a center point. Needs the kinematic Rigidbody2D of the bomb prefab.
    /// </summary>
    public sealed class BombMover : MonoBehaviour
    {
        public enum MoveMode { None, PointToPoint, Radial }

        [SerializeField] private MoveMode mode = MoveMode.None;

        [Header("Point to point (floats between A and B)")]
        [Tooltip("Point A. Empty = where the bomb starts.")]
        [SerializeField] private Transform pointA;
        [Tooltip("Point B. Empty = point A + Travel.")]
        [SerializeField] private Transform pointB;
        [Tooltip("Used when Point B is empty. World units from A.")]
        [SerializeField] private Vector2 travel = new Vector2(6f, 0f);
        [Tooltip("Units per second. Slows down at each end, so it floats.")]
        [SerializeField, Min(0.01f)] private float speed = 1.5f;
        [Tooltip("Wait at each end.")]
        [SerializeField, Min(0f)] private float pauseSeconds = 0.5f;

        [Header("Radial (circles around a center)")]
        [Tooltip("The point (a GameObject in the maze) the bomb circles. Empty = a center is placed so that the bomb starts at Start Angle.")]
        [SerializeField] private Transform center;
        [Tooltip("Distance from the center, in world units.")]
        [SerializeField, Min(0.1f)] private float radius = 3f;
        [Tooltip("Degrees per second. Negative = the other way.")]
        [SerializeField] private float angularSpeed = 60f;
        [Tooltip("Only without a Center: where on the circle the bomb starts (0 = right, 90 = up).")]
        [SerializeField] private float startAngle;

        private Rigidbody2D _body;
        private Vector2 _a, _b, _fixedCenter;
        private float _t;           // point to point: 0 at A, 1 at B
        private int _direction = 1;
        private float _waitUntil;
        private float _angle;       // radial, degrees

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            Vector2 start = transform.position;

            _a = pointA != null ? (Vector2)pointA.position : start;
            _b = pointB != null ? (Vector2)pointB.position : _a + travel;

            if (center != null)
            {
                Vector2 offset = start - (Vector2)center.position;
                _angle = offset.sqrMagnitude > 0.0001f ? Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg : startAngle;
            }
            else
            {
                _angle = startAngle;
                float rad = startAngle * Mathf.Deg2Rad;
                _fixedCenter = start - new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            }
        }

        private void FixedUpdate()
        {
            switch (mode)
            {
                case MoveMode.PointToPoint: MovePointToPoint(); break;
                case MoveMode.Radial: MoveRadial(); break;
            }
        }

        private void MovePointToPoint()
        {
            if (Time.time < _waitUntil) return;

            float length = Mathf.Max(0.01f, (_b - _a).magnitude);
            _t += _direction * speed * Time.fixedDeltaTime / length;

            if (_t >= 1f || _t <= 0f)
            {
                _t = Mathf.Clamp01(_t);
                _direction = -_direction;
                _waitUntil = Time.time + pauseSeconds;
            }

            MoveTo(Vector2.Lerp(_a, _b, Mathf.SmoothStep(0f, 1f, _t)));
        }

        private void MoveRadial()
        {
            _angle += angularSpeed * Time.fixedDeltaTime;
            Vector2 around = center != null ? (Vector2)center.position : _fixedCenter;
            float rad = _angle * Mathf.Deg2Rad;
            MoveTo(around + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius);
        }

        private void MoveTo(Vector2 position)
        {
            if (_body != null) _body.MovePosition(position);
            else transform.position = position;
        }
    }
}
