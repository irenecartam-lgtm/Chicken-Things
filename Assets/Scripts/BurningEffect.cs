using UnityEngine;

// Quemadura sobre un enemigo: le hace dano cada poco tiempo, lo tine de naranja
// que parpadea y le prende fuego (particulas). Se anade sola al enemigo con
// BurningEffect.Apply(...); cuando acaba el tiempo se quita sola.
// Es un componente aparte (y no una corrutina de EnemyHealth) porque EnemyHealth
// llama a StopAllCoroutines() en cada golpe y apagaria el fuego.
public class BurningEffect : MonoBehaviour
{
    private EnemyHealth health;
    private Renderer[] bodyRenderers;
    private MaterialPropertyBlock block;
    private GameObject fireObject;

    private float remaining;
    private float tickInterval = 0.5f;
    private float tickTimer = -1f;
    private int damagePerTick = 1;

    private static Material fireMaterial;

    // Prende (o vuelve a prender, reiniciando el tiempo) a un enemigo.
    public static void Apply(EnemyHealth target, float duration, int damagePerTick, float tickInterval)
    {
        if (target == null) return;

        BurningEffect effect = target.GetComponent<BurningEffect>();
        if (effect == null) effect = target.gameObject.AddComponent<BurningEffect>();

        effect.remaining = duration;
        effect.damagePerTick = damagePerTick;
        effect.tickInterval = Mathf.Max(0.05f, tickInterval);
        if (effect.tickTimer < 0f) effect.tickTimer = effect.tickInterval;
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        block = new MaterialPropertyBlock();

        // Solo las mallas del cuerpo: no se tocan los sprites (barra de vida) ni las particulas.
        System.Collections.Generic.List<Renderer> list = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);
        bodyRenderers = list.ToArray();

        BuildFire();
    }

    private void Update()
    {
        if (health == null) { Destroy(this); return; }

        float dt = Time.deltaTime;
        remaining -= dt;
        tickTimer -= dt;

        if (tickTimer <= 0f)
        {
            tickTimer += tickInterval;
            health.TakeBurnDamage(damagePerTick);
            if (health.CurrentHealth <= 0) return;   // murio: el enemigo se destruye en este frame
        }

        if (remaining <= 0f)
        {
            Extinguish();
            return;
        }

        // Parpadeo naranja <-> amarillo.
        float t = (Mathf.Sin(Time.time * 12f) + 1f) * 0.5f;
        Color c = Color.Lerp(new Color(1f, 0.25f, 0.05f), new Color(1f, 0.7f, 0.1f), t);
        block.SetColor("_Color", c);
        block.SetColor("_BaseColor", c);
        foreach (Renderer r in bodyRenderers)
            if (r != null) r.SetPropertyBlock(block);
    }

    private void Extinguish()
    {
        foreach (Renderer r in bodyRenderers)
            if (r != null) r.SetPropertyBlock(null);   // vuelve al color normal

        Destroy(this);   // OnDestroy se lleva el fuego
    }

    private void OnDestroy()
    {
        if (fireObject != null) Destroy(fireObject);
    }

    // ----------------------------------------------------------------- Fuego
    private void BuildFire()
    {
        // Tamano del fuego segun lo grande que sea el enemigo.
        Bounds b = default;
        bool has = false;
        foreach (Renderer r in bodyRenderers)
        {
            if (r == null) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        Vector3 center = has ? b.center : transform.position + Vector3.up * 0.5f;
        float radius = has ? Mathf.Max(0.2f, Mathf.Min(b.extents.x, b.extents.z)) : 0.4f;

        fireObject = new GameObject("Fuego");
        fireObject.transform.SetParent(transform, false);
        fireObject.transform.position = center;

        ParticleSystem ps = fireObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.55f, 0.1f, 0.9f), new Color(1f, 0.2f, 0.05f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;   // no depende de la escala del enemigo
        main.gravityModifier = -0.25f;                        // gravedad negativa: las llamas suben
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 30f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        // Del amarillo al rojo y se apaga (humo oscuro y transparente).
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0.1f), 0.6f),
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.7f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = g;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer pr = fireObject.GetComponent<ParticleSystemRenderer>();
        Material mat = GetFireMaterial();
        if (mat != null) pr.sharedMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
    }

    // Un unico material para todas las llamas: Sprites/Default siempre viene incluido
    // en el juego compilado y se ve igual en URP que en el pipeline normal.
    private static Material GetFireMaterial()
    {
        if (fireMaterial != null) return fireMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        const int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        // Circulo suave: opaco en el centro y transparente en el borde.
        Color32[] px = new Color32[res * res];
        float r = res * 0.5f;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                px[y * res + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        fireMaterial = new Material(shader) { mainTexture = tex };
        return fireMaterial;
    }
}
