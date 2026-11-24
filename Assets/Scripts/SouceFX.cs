using UnityEngine;

// Efectos visuales compartidos por las salsas. Todo se crea por codigo (sin assets):
// particulas suaves, humo gris, explosion y unos pocos materiales que se reutilizan.
public static class SauceFX
{
    private static Material softMaterial;
    private static Material ringMaterial;
    private static Material cheeseMaterial;
    private static Mesh sphereMesh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        softMaterial = null;
        ringMaterial = null;
        cheeseMaterial = null;
        sphereMesh = null;
    }

    // ------------------------------------------------------------ Materiales

    // Circulo suave: opaco en el centro y transparente en el borde.
    public static Material SoftMaterial()
    {
        if (softMaterial == null) softMaterial = MakeParticleMaterial(MakeTexture(false));
        return softMaterial;
    }

    // Anillo fino, para la onda expansiva de las explosiones.
    public static Material RingMaterial()
    {
        if (ringMaterial == null) ringMaterial = MakeParticleMaterial(MakeTexture(true));
        return ringMaterial;
    }

    // Queso derretido: amarillo y con brillo. Se comparte entre todos los charcos.
    public static Material CheeseMaterial()
    {
        if (cheeseMaterial != null) return cheeseMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        cheeseMaterial = new Material(shader);
        cheeseMaterial.color = new Color(1f, 0.82f, 0.18f);
        return cheeseMaterial;
    }

    // La malla de un primitivo es un asset interno de Unity: sobrevive al objeto temporal.
    public static Mesh SphereMesh()
    {
        if (sphereMesh != null) return sphereMesh;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temp);
        return sphereMesh;
    }

    // Sprites/Default viene siempre incluido en el juego compilado, soporta transparencia
    // y se ve igual en URP que en el pipeline normal.
    private static Material MakeParticleMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        return new Material(shader) { mainTexture = texture };
    }

    private static Texture2D MakeTexture(bool ring)
    {
        const int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] px = new Color32[res * res];
        float r = res * 0.5f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;

                float a = ring
                    ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.2f)
                    : Mathf.Clamp01(1f - d);

                a *= a;
                px[y * res + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------- Particulas

    // Sistema de particulas parado y sin configurar: quien lo pide ajusta los modulos y llama a Play().
    public static ParticleSystem NewSystem(string name, Transform parent, Vector3 worldPosition, Material material)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;   // no depende de la escala del padre

        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        if (material != null) pr.sharedMaterial = material;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;

        return ps;
    }

    // Degradado de 4 colores repartidos a lo largo de la vida de la particula (el alfa de cada color cuenta).
    public static Gradient MakeGradient(Color c0, Color c1, Color c2, Color c3)
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(c0, 0f),
                new GradientColorKey(c1, 0.35f),
                new GradientColorKey(c2, 0.7f),
                new GradientColorKey(c3, 1f)
            },
            new[]
            {
                new GradientAlphaKey(c0.a, 0f),
                new GradientAlphaKey(c1.a, 0.35f),
                new GradientAlphaKey(c2.a, 0.7f),
                new GradientAlphaKey(c3.a, 1f)
            });
        return g;
    }

    // Nube de humo gris que sube y se disipa. "scale" ~ tamano del enemigo en metros.
    public static void SmokePuff(Vector3 position, float scale)
    {
        ParticleSystem ps = NewSystem("HumoGris", null, position, SoftMaterial());

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f * scale, 1.7f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)22) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f * scale;

        // Sube despacio con un poco de deriva lateral.
        ParticleSystem.VelocityOverLifetimeModule vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        vol.y = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        vol.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = MakeGradient(
            new Color(0.55f, 0.55f, 0.55f, 0f),
            new Color(0.5f, 0.5f, 0.5f, 0.85f),
            new Color(0.38f, 0.38f, 0.38f, 0.6f),
            new Color(0.3f, 0.3f, 0.3f, 0f));

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));

        ps.Play();
        Object.Destroy(ps.gameObject, 5f);
    }

    // Explosion de la Hot Sauce: bola de fuego, onda expansiva en el suelo y un poco de humo.
    public static void Explosion(Vector3 position, float radius)
    {
        // 1) Bola de fuego.
        ParticleSystem fire = NewSystem("ExplosionFuego", null, position, SoftMaterial());

        ParticleSystem.MainModule fm = fire.main;
        fm.loop = false;
        fm.duration = 0.3f;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        fm.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.5f, radius * 3f);
        fm.startSize = new ParticleSystem.MinMaxCurve(radius * 0.45f, radius * 0.9f);
        fm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fm.startColor = Color.white;
        fm.maxParticles = 60;

        ParticleSystem.EmissionModule fe = fire.emission;
        fe.rateOverTime = 0f;
        fe.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)28) });

        ParticleSystem.ShapeModule fs = fire.shape;
        fs.shapeType = ParticleSystemShapeType.Sphere;
        fs.radius = 0.2f;

        ParticleSystem.ColorOverLifetimeModule fc = fire.colorOverLifetime;
        fc.enabled = true;
        fc.color = MakeGradient(
            new Color(1f, 0.95f, 0.6f, 1f),
            new Color(1f, 0.55f, 0.1f, 0.95f),
            new Color(0.85f, 0.12f, 0.05f, 0.6f),
            new Color(0.2f, 0.08f, 0.05f, 0f));

        ParticleSystem.SizeOverLifetimeModule fz = fire.sizeOverLifetime;
        fz.enabled = true;
        fz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));

        fire.Play();
        Object.Destroy(fire.gameObject, 3f);

        // 2) Onda expansiva: un anillo plano que se abre sobre el suelo.
        ParticleSystem ring = NewSystem("ExplosionOnda", null, position, RingMaterial());

        ParticleSystem.MainModule rm = ring.main;
        rm.loop = false;
        rm.duration = 0.3f;
        rm.startLifetime = 0.35f;
        rm.startSpeed = 0f;
        rm.startSize = radius * 2.2f;
        rm.startColor = Color.white;
        rm.maxParticles = 4;

        ParticleSystem.EmissionModule re = ring.emission;
        re.rateOverTime = 0f;
        re.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });

        ParticleSystem.ShapeModule rs = ring.shape;
        rs.enabled = false;

        ParticleSystem.ColorOverLifetimeModule rc = ring.colorOverLifetime;
        rc.enabled = true;
        rc.color = MakeGradient(
            new Color(1f, 0.85f, 0.45f, 0.95f),
            new Color(1f, 0.6f, 0.2f, 0.7f),
            new Color(1f, 0.4f, 0.1f, 0.3f),
            new Color(1f, 0.3f, 0.1f, 0f));

        ParticleSystem.SizeOverLifetimeModule rz = ring.sizeOverLifetime;
        rz.enabled = true;
        rz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.15f, 1f, 1f));

        ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

        ring.Play();
        Object.Destroy(ring.gameObject, 3f);

        // 3) Humo oscuro, pequeno.
        SmokePuff(position + Vector3.up * 0.2f, Mathf.Max(0.5f, radius * 0.3f));
    }
}