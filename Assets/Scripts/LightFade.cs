using UnityEngine;

// Destello de luz: baja de intensidad hasta apagarse y destruye el objeto. Lo crea SauceFX.FlashLight.
[RequireComponent(typeof(Light))]
public class LightFade : MonoBehaviour
{
    public float duration = 0.25f;

    private Light lightComponent;
    private float startIntensity;
    private float age;

    private void Awake()
    {
        lightComponent = GetComponent<Light>();
        startIntensity = lightComponent.intensity;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = age / Mathf.Max(0.01f, duration);

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        lightComponent.intensity = startIntensity * (1f - t) * (1f - t);
    }
}
