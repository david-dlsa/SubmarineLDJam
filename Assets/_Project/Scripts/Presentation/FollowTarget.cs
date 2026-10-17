using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Debug helper: camera follows the submarine (for the F1 world view).</summary>
    public sealed class FollowTarget : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float z = -10f;

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = new Vector3(target.position.x, target.position.y, z);
        }
    }
}
