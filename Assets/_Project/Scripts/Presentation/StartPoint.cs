using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>Put one in each maze prefab. The submarine starts here.</summary>
    public sealed class StartPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.6f);
        }
    }
}
