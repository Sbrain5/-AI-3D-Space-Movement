using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(ParticleSystem))]
public class ScaleParticles : MonoBehaviour
{
    private ParticleSystem ps;
    private ParticleSystem.MainModule mainModule;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        mainModule = ps.main;
    }

    private void Update()
    {
        if (ps == null)
            return;

        float scaleValue = transform.lossyScale.magnitude;

        mainModule.startSize = scaleValue;
    }
}