using UnityEngine;

public class ToggleEmitParticleOnEnable : MonoBehaviour
{
    [SerializeField] private ParticleSystem _particles;

    private void OnEnable()
    {
        _particles.Play(true);
    }

    private void OnDisable()
    {
        _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
