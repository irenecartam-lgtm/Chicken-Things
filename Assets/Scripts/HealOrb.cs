using UnityEngine;

// Orbe de vida: una bolita de luz blanca que sale del enemigo al que has dado y vuela, haciendo una curva,
// hasta el pecho del jugador. Con la salsa de mayonesa cada impacto lanza uno: asi se ve que el daño que haces
// se convierte en vida. El orbe que completa un punto de vida (grande) lleva la curacion y la aplica al llegar.
public class HealOrb : MonoBehaviour
{
    private Transform target;
    private Vector3 targetOffset;
    private Vector3 start, control;
    private float t, duration;
    private bool big;
    private bool finished;
    private System.Action onArrive;
    private ParticleSystem trail;

    // from = donde nace (el punto de impacto). onArrive se ejecuta cuando llega al jugador (puede ser null).
    public static void Spawn(Vector3 from, Transform target, bool big, System.Action onArrive)
    {
        // Sin jugador al que llegar, la curacion se aplica directamente.
        if (target == null)
        {
            if (onArrive != null) onArrive();
            return;
        }

        GameObject go = new GameObject("OrbeVida");
        go.transform.position = from;
        go.AddComponent<HealOrb>().Init(from, target, big, onArrive);
    }

    private void Init(Vector3 from, Transform playerTarget, bool isBig, System.Action arrive)
    {
        target = playerTarget;
        big = isBig;
        onArrive = arrive;
        start = from;
        duration = Random.Range(0.4f, 0.55f);

        // Apunta al pecho: el centro del collider del jugador.
        Collider col = target.GetComponent<Collider>();
        Vector3 chest = col != null ? col.bounds.center : target.position + Vector3.up;
        targetOffset = chest - target.position;

        // Punto de control de la curva: sube y se va a un lado, para que no sea una linea recta.
        Vector3 toTarget = chest - from;
        Vector3 side = Vector3.Cross(Vector3.up, toTarget);
        side = side.sqrMagnitude < 0.01f ? Vector3.right : side.normalized;
        control = from + toTarget * 0.35f + Vector3.up * Random.Range(0.8f, 1.6f) + side * Random.Range(-1f, 1f);

        BuildVisual();
    }

    private void BuildVisual()
    {
        // La estela de particulas es lo que se ve: las mas recientes (junto al orbe) son las mas grandes.
        trail = SauceFX.NewSystem("Estela", transform, transform.position, SauceFX.SoftMaterial());

        ParticleSystem.MainModule m = trail.main;
        m.loop = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        m.startSpeed = 0f;
        m.startSize = big ? new ParticleSystem.MinMaxCurve(0.2f, 0.3f) : new ParticleSystem.MinMaxCurve(0.1f, 0.16f);
        m.startColor = Color.white;
        m.maxParticles = 60;

        ParticleSystem.EmissionModule e = trail.emission;
        e.rateOverTime = 90f;

        ParticleSystem.ShapeModule s = trail.shape;
        s.enabled = false;

        ParticleSystem.ColorOverLifetimeModule c = trail.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 1f, 0.9f, 0.8f),
            new Color(1f, 0.97f, 0.75f, 0.4f),
            new Color(1f, 0.95f, 0.7f, 0f));

        ParticleSystem.SizeOverLifetimeModule z = trail.sizeOverLifetime;
        z.enabled = true;
        z.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        trail.Play();

        // El orbe grande ademas ilumina un poco lo que tiene cerca.
        if (big)
        {
            Light l = gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 1f, 0.92f);
            l.range = 2.5f;
            l.intensity = 1.4f;
            l.shadows = LightShadows.None;
        }
    }

    private void Update()
    {
        if (finished) return;

        t += Time.deltaTime / duration;

        if (target == null)
        {
            Finish(false);
            return;
        }

        // Curva de Bezier cuadratica hacia donde esta ahora el jugador: arranca despacio y se acelera, como si le "absorbiera".
        Vector3 end = target.position + targetOffset;
        float k = Mathf.Clamp01(t);
        k *= k;

        Vector3 a = Vector3.Lerp(start, control, k);
        Vector3 b = Vector3.Lerp(control, end, k);
        transform.position = Vector3.Lerp(a, b, k);

        if (t >= 1f) Finish(true);
    }

    private void Finish(bool arrived)
    {
        finished = true;

        if (arrived)
        {
            if (onArrive != null) onArrive();
            HealEffect.Absorb(big);
        }

        // La estela se separa del orbe para que sus ultimas particulas se desvanezcan solas.
        if (trail != null)
        {
            trail.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            trail.transform.SetParent(null, true);
            Destroy(trail.gameObject, 1f);
            trail = null;
        }

        Destroy(gameObject);
    }
}
