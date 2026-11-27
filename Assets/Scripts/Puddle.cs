using UnityEngine;

// Charco o mancha que crece al aparecer y, cuando se le pide, aguanta un rato y se encoge hasta desaparecer.
// Se pone en un objeto "contenedor": lo que se escala es el contenedor entero (sus hijos conservan su forma).
public class Puddle : MonoBehaviour
{
    private float growTime = 0.3f;
    private float age;

    private bool fading;
    private float fadeDelay;
    private float fadeTime = 1f;
    private float fadeAge;

    // Empieza en tamano 0 y crece hasta el completo en "grow" segundos.
    public void Begin(float grow)
    {
        growTime = Mathf.Max(0f, grow);
        age = 0f;
        transform.localScale = growTime > 0f ? Vector3.zero : Vector3.one;
    }

    // Tras "delay" segundos se encoge durante "time" segundos y se destruye.
    public void FadeOut(float delay, float time)
    {
        if (fading) return;

        fading = true;
        fadeDelay = Mathf.Max(0f, delay);
        fadeTime = Mathf.Max(0.05f, time);
        fadeAge = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        float k = growTime > 0f ? Mathf.Clamp01(age / growTime) : 1f;
        k = 1f - (1f - k) * (1f - k);   // entra rapido y se asienta

        if (fading)
        {
            fadeAge += dt;
            if (fadeAge > fadeDelay)
            {
                float t = (fadeAge - fadeDelay) / fadeTime;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                k *= 1f - t;
            }
        }

        transform.localScale = Vector3.one * k;
    }
}
