using Unity.Cinemachine;
using UnityEngine;

public class InvokeImpulseOnEnable : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    private void OnEnable()
    {
        _impulseSource.GenerateImpulse();
    }
}
