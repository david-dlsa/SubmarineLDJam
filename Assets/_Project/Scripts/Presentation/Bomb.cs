using System;
using SubmarineLDJam.Domain.Levels;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// A bomb. Sends a slow red wave on the radar up to its range. It explodes when the submarine
    /// touches it (Contact) or comes inside its range (Range). Then it disappears, a strong wave shows
    /// where it was, the submarine loses oxygen and is pushed away.
    /// Needs a Collider2D (made a trigger here) and a Hazard of kind Bomb.
    /// </summary>
    [RequireComponent(typeof(Hazard), typeof(Collider2D))]
    public sealed class Bomb : RunView
    {
        [Header("Trigger")]
        [Tooltip("Explodes when the submarine touches it.")]
        [SerializeField] private bool triggerOnContact = true;
        [Tooltip("Explodes when the submarine comes inside the range.")]
        [SerializeField] private bool triggerOnRange = true;
        [Tooltip("Range, in world units. The red wave on the radar grows up to this size.")]
        [SerializeField, Min(0.1f)] private float triggerRange = 3f;
        [Tooltip("Range trigger only works while the submarine is moving (it 'hears' movement).")]
        [SerializeField] private bool needsMovement;
        [Tooltip("Speed the submarine needs to count as moving.")]
        [SerializeField, Min(0f)] private float movingSpeed = 0.3f;

        [Header("Damage to the submarine (same as an obstacle hit)")]
        [Tooltip("Oxygen lost at once.")]
        [SerializeField, Min(0f)] private float oxygenLoss = 15f;
        [Tooltip("Oxygen falls this many times faster after the hit...")]
        [SerializeField, Min(1f)] private float drainMultiplier = 2f;
        [Tooltip("...for this many seconds.")]
        [SerializeField, Min(0f)] private float drainBoostSeconds = 5f;

        [Header("Push")]
        [Tooltip("Impulse that pushes the submarine away from the bomb. A normal movement is about 8. 0 = no push.")]
        [SerializeField, Min(0f)] private float knockbackForce = 6f;

        [Header("Explosion")]
        [Tooltip("Size of the strong red wave.")]
        [SerializeField, Min(0.1f)] private float explosionRadius = 4f;
        [SerializeField, Min(0.05f)] private float explosionSeconds = 0.7f;
        [Tooltip("Another bomb's explosion (chain) only hurts if the submarine is this close.")]
        [SerializeField, Min(0.1f)] private float blastRadius = 3.5f;
        [SerializeField] private Color explosionColor = new Color(1f, 0.2f, 0.15f, 1f);

        [Header("Slow wave on the radar")]
        [SerializeField] private Color pulseColor = new Color(1f, 0.25f, 0.2f, 0.8f);
        [SerializeField, Min(0.2f)] private float pulseSeconds = 2.5f;
        [SerializeField, Min(0f)] private float pulsePauseSeconds = 0.5f;

        /// <summary>Raised when this bomb blows up, before it is destroyed.</summary>
        public event Action<Bomb> Exploded;

        private SubmarineBody _sub;
        private Rigidbody2D _subBody;
        private bool _exploded;

        private void Awake()
        {
            foreach (var collider in GetComponents<Collider2D>()) collider.isTrigger = true;
        }

        protected override void OnBind()
        {
            _sub = FindFirstObjectByType<SubmarineBody>();
            _subBody = _sub != null ? _sub.GetComponent<Rigidbody2D>() : null;
        }

        private void Start()
        {
            // A wave that repeats for as long as the bomb exists.
            RadialPulse.Spawn(transform.parent, transform.position, gameObject.layer, transform, pulseColor,
                0.3f, triggerRange, pulseSeconds, loop: true, pauseSeconds: pulsePauseSeconds);
        }

        private void Update()
        {
            if (!IsBound || _exploded || !triggerOnRange || _sub == null) return;

            Vector2 toSub = (Vector2)_sub.transform.position - (Vector2)transform.position;
            if (toSub.sqrMagnitude > triggerRange * triggerRange) return;
            if (needsMovement && (_subBody == null || _subBody.linearVelocity.magnitude < movingSpeed)) return;

            Explode(true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!triggerOnContact || !IsBound || other.GetComponentInParent<SubmarineBody>() == null) return;
            Explode(true);
        }

        /// <summary>Blow up now. Used by the bomb group for chain reactions.</summary>
        public void Detonate() => Explode(false);

        private void Explode(bool hitsSubmarine)
        {
            if (_exploded) return;
            _exploded = true;

            Vector2 position = transform.position;

            if (IsBound && _sub != null)
            {
                Vector2 away = (Vector2)_sub.transform.position - position;
                float distance = away.magnitude;
                away = distance > 0.01f ? away / distance : Vector2.down;

                if (hitsSubmarine || distance <= blastRadius)
                {
                    _sub.Knock(away * knockbackForce);
                    Service.RegisterCollision(CollisionKind.Bomb,
                        new CollisionPenalty(oxygenLoss, drainMultiplier, drainBoostSeconds), true);
                }
            }

            // Strong wave + flash. They live under the maze, so they go away with it.
            RadialPulse.Spawn(transform.parent, position, gameObject.layer, null, explosionColor,
                0.2f, explosionRadius, explosionSeconds, thick: true);
            RadialPulse.Spawn(transform.parent, position, gameObject.layer, null, explosionColor,
                0.2f, explosionRadius * 0.6f, explosionSeconds * 0.6f, filled: true);

            Exploded?.Invoke(this);
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, triggerRange);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
