using System.Collections;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// A cluster of bombs (the Bombs are children of this object).
    /// Chain reaction: when one blows up, the others blow up one after the other.
    /// </summary>
    public sealed class BombGroup : MonoBehaviour
    {
        [Tooltip("When one bomb explodes, the others explode too.")]
        [SerializeField] private bool chainReaction = true;
        [Tooltip("Wait before each next bomb. Farther bombs wait longer.")]
        [SerializeField, Min(0f)] private float delayPerUnit = 0.12f;

        private bool _chaining;

        private void Awake()
        {
            foreach (var bomb in GetComponentsInChildren<Bomb>())
                bomb.Exploded += OnBombExploded;
        }

        private void OnBombExploded(Bomb source)
        {
            if (!chainReaction || _chaining) return;
            _chaining = true;

            Vector3 origin = source.transform.position;
            foreach (var other in GetComponentsInChildren<Bomb>())
            {
                if (other == source) continue;
                StartCoroutine(Detonate(other, Vector3.Distance(origin, other.transform.position) * delayPerUnit));
            }
        }

        private static IEnumerator Detonate(Bomb bomb, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (bomb != null) bomb.Detonate();
        }
    }
}
