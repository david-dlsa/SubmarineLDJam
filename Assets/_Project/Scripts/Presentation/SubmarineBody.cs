using SubmarineLDJam.Domain.Common;
using SubmarineLDJam.Domain.Levels;
using SubmarineLDJam.Domain.Navigation;
using SubmarineLDJam.Domain.Runs;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Unity physics for the submarine. Domain says "impulse", this pushes the Rigidbody2D.
    /// Physics says "hit" or "arrived", this tells the domain.
    /// Physics is off while there is no run (for example in the stage select).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SubmarineBody : RunView
    {
        [Tooltip("Impulse strength when force is at max level.")]
        [SerializeField] private float maxImpulse = 8f;
        [Tooltip("How fast the submarine slows down by itself.")]
        [SerializeField] private float damping = 1.5f;
        [Tooltip("OFF (default): only the movement module turns the submarine. ON: hits and physics can spin it.")]
        [SerializeField] private bool allowEnvironmentRotation = false;
        [Tooltip("Only used when environment rotation is ON. Slows the spin.")]
        [SerializeField] private float angularDamping = 2f;

        private Rigidbody2D _body;
        private RadarView _radar;
        private Impulse _pendingImpulse;
        private bool _hasPending;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.linearDamping = damping;
            _body.angularDamping = angularDamping;
            _body.freezeRotation = !allowEnvironmentRotation;
            _body.simulated = false; // OnBind turns it on when a run starts
        }

        protected override void OnBind()
        {
            _body.simulated = true;
            _hasPending = false;
            if (_radar == null) _radar = FindFirstObjectByType<RadarView>(FindObjectsInactive.Include);
        }

        private void FixedUpdate()
        {
            // The force waits until the radar finished turning to the new heading.
            if (!IsBound || !_hasPending) return;
            if (_radar != null && _radar.IsTurning) return;

            _hasPending = false;
            Push(_pendingImpulse);
        }

        private void Push(Impulse impulse)
        {
            var direction = new Vector2(impulse.Heading.X, impulse.Heading.Y);
            _body.AddForce(direction * (impulse.Force.Normalized * maxImpulse), ForceMode2D.Impulse);
        }

        protected override void OnUnbind()
        {
            if (_body == null) return;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.simulated = false;
            _hasPending = false;
        }

        /// <summary>Move to a place, stand still, face up. Used when a stage starts.</summary>
        public void PlaceAt(Vector2 position)
        {
            if (_body == null) _body = GetComponent<Rigidbody2D>();

            transform.SetPositionAndRotation(new Vector3(position.x, position.y, transform.position.z), Quaternion.identity);
            _body.position = position;
            _body.rotation = 0f;
            _hasPending = false;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        protected override void OnEvent(IDomainEvent domainEvent)
        {
            switch (domainEvent)
            {
                case HeadingLocked locked:
                    // Sprite faces up. Domain heading is clockwise, Unity rotation is counter-clockwise.
                    _body.rotation = -locked.Heading.Degrees;
                    break;

                case ImpulseFired fired:
                    _pendingImpulse = fired.Impulse;
                    _hasPending = true;
                    break;

                case RunCompleted _:
                case RunFailed _:
                    _hasPending = false;
                    _body.linearVelocity = Vector2.zero;
                    break;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsBound) return;

            // No Hazard component = treated as wall.
            var kind = collision.collider.TryGetComponent<Hazard>(out var hazard) ? hazard.Kind : CollisionKind.Wall;
            Service.RegisterCollision(kind);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsBound && other.TryGetComponent<DestinationZone>(out _))
                Service.ReachDestination();
        }
    }
}
