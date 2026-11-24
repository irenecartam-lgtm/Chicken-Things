using System.Collections.Generic;
using UnityEngine;

// Efectos visuales compartidos por las salsas. Todo se crea por codigo (sin assets):
// particulas, salpicaduras, manchas en el suelo, humo, explosion y unos pocos materiales que se reutilizan.
public static class SauceFX
{
    private static Material softMaterial;
    private static Material ringMaterial;
    private static Material blobMaterial;
    private static Material plusMaterial;
    private static Mesh sphereMesh;
    private static Mesh cylinderMesh;
    private static readonly Dictionary<Color, Material> litMaterials = new Dictionary<Color, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        softMaterial = null;
        ringMaterial = null;
        blobMaterial = null;
        plusMaterial = null;
        sphereMesh = null;
        cylinderMesh = null;
        litMaterials.Clear();
    }

    // ------------------------------------------------------------ Materiales

    // Circulo suave: opaco en el centro y transparente en el borde.
    public static Material SoftMaterial()
    {
        if (softMaterial == null) softMaterial = MakeParticleMaterial(MakeTexture(Shape.Soft));
        return softMaterial;
    }

    // Anillo fino: ondas expansivas y burbujas.
    public static Material RingMaterial()
    {
        if (ringMaterial == null) ringMaterial = MakeParticleMaterial(MakeTexture(Shape.Ring));
        return ringMaterial;
    }

    // Gota o mancha de salsa: borde definido y un poco mas oscuro, con un brillo arriba a la izquierda.
    public static Material BlobMaterial()
    {
        if (blobMaterial == null) blobMaterial = MakeParticleMaterial(MakeTexture(Shape.Blob));
        return blobMaterial;
    }

    // Cruz "+" suave, como las de curacion de Overwatch.
    public static Material PlusMaterial()
    {
        if (plusMaterial == null) plusMaterial = MakeParticleMaterial(MakeTexture(Shape.Plus));
        return plusMaterial;
    }

    // Material con luz y brillo, un unico por color: charcos de queso, manchas de salsa...
    public static Material LitMaterial(Color color)
    {
        Material m;
        if (litMaterials.TryGetValue(color, out m) && m != null) return m;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        m = new Material(shader);
        m.color = color;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);    // URP: aspecto humedo
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.85f);    // Standard

        litMaterials[color] = m;
        return m;
    }

    public static Material CheeseMaterial() => LitMaterial(new Color(1f, 0.82f, 0.18f));

    // Las mallas de los primitivos son assets internos de Unity: sobreviven al objeto temporal.
    public static Mesh SphereMesh()
    {
        if (sphereMesh == null) sphereMesh = PrimitiveMesh(PrimitiveType.Sphere);
        return sphereMesh;
    }

    // Cilindro de 2 unidades de alto y 1 de ancho (para los hilos de queso).
    public static Mesh CylinderMesh()
    {
        if (cylinderMesh == null) cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder);
        return cylinderMesh;
    }

    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temp);
        return mesh;
    }

    // Sprites/Default viene siempre incluido en el juego compilado, soporta transparencia
    // y se ve igual en URP que en el pipeline normal.
    private static Material MakeParticleMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        return new Material(shader) { mainTexture = texture };
    }

    private enum Shape { Soft, Ring, Blob, Plus }

    private static Texture2D MakeTexture(Shape shape)
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
                float nx = (x + 0.5f - r) / r;   // -1..1
                float ny = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(nx * nx + ny * ny);

                float a;
                float shade = 1f;

                switch (shape)
                {
                    case Shape.Ring:
                        a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.2f);
                        a *= a;
                        break;

                    case Shape.Blob:
                        a = Mathf.Clamp01((1f - d) / 0.15f);                  // borde definido
                        shade = Mathf.Lerp(0.9f, 0.68f, d * d);               // mas oscuro hacia el borde
                        float hx = nx + 0.35f, hy = ny - 0.35f;               // brillo arriba a la izquierda
                        shade = Mathf.Min(1f, shade + 0.3f * Mathf.Exp(-(hx * hx + hy * hy) / 0.05f));
                        break;

                    case Shape.Plus:
                    {
                        const float arm = 0.2f, len = 0.85f, soft = 0.12f;
                        float ax = Mathf.Abs(nx), ay = Mathf.Abs(ny);
                        float horizontal = Mathf.Clamp01((arm - ay) / soft + 0.5f) * Mathf.Clamp01((len - ax) / soft + 0.5f);
                        float vertical = Mathf.Clamp01((arm - ax) / soft + 0.5f) * Mathf.Clamp01((len - ay) / soft + 0.5f);
                        a = Mathf.Max(horizontal, vertical);
                        break;
                    }

                    default:   // Soft
                        a = Mathf.Clamp01(1f - d);
                        a *= a;
                        break;
                }

                byte c = (byte)(Mathf.Clamp01(shade) * 255f);
                px[y * res + x] = new Color32(c, c, c, (byte)(a * 255f));
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

    // Mismo color con el alfa cambiado.
    public static Color WithAlpha(Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }

    // Mismo color mas oscuro (factor 0..1).
    public static Color Darker(Color c, float factor)
    {
        return new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
    }

    // ----------------------------------------------------------------- Suelo

    // Altura del suelo bajo un punto, ignorando enemigos y jugador. Si no hay suelo devuelve "fallback".
    public static float GroundY(Vector3 point, float fallback)
    {
        Ray ray = new Ray(point + Vector3.up * 1f, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 40f, ~0, QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;
        float result = fallback;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;
            if (c.CompareTag("Player")) continue;

            if (hits[i].distance < closest)
            {
                closest = hits[i].distance;
                result = hits[i].point.y;
            }
        }
        return result;
    }

    // Manchas de salsa en el suelo alrededor de un punto. Crecen de golpe, aguantan un poco y se encogen.
    public static void GroundStains(Vector3 center, float radius, Color color, int count, float minSize, float maxSize, float hold)
    {
        Mesh mesh = SphereMesh();
        Material mat = LitMaterial(color);

        for (int i = 0; i < count; i++)
        {
            Vector2 disc = Random.insideUnitCircle * radius;
            Vector3 spot = center + new Vector3(disc.x, 0f, disc.y);
            float y = GroundY(spot, center.y);

            GameObject holder = new GameObject("MachaSalsa");
            holder.transform.position = new Vector3(spot.x, y + 0.02f, spot.z);

            float size = Random.Range(minSize, maxSize);
            AddFlatBlob(holder.transform, mesh, mat, Vector3.zero, size, 0.1f);

            Puddle puddle = holder.AddComponent<Puddle>();
            puddle.Begin(0.12f);
            puddle.FadeOut(hold * Random.Range(0.8f, 1.2f), 1.5f);
        }
    }

    // Esfera aplastada (una mancha) hija de "parent".
    public static void AddFlatBlob(Transform parent, Mesh mesh, Material mat, Vector3 localOffset, float size, float thickness)
    {
        GameObject go = new GameObject("Mancha", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;
        go.transform.localScale = new Vector3(size, size * thickness, size);

        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------- Salpicaduras

    // Chorro de gotas de salsa que sale hacia "direction" y cae por la gravedad.
    public static void Splat(Vector3 position, Vector3 direction, Color color, int count, float speed, float size)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.up;

        ParticleSystem ps = NewSystem("Salpicadura", null, position, BlobMaterial());
        ps.transform.rotation = Quaternion.LookRotation(direction.normalized);   // el cono sale por su eje Z

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = 1.3f;
        main.maxParticles = Mathf.Max(count, 8);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.08f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = MakeGradient(
            WithAlpha(color, 1f),
            WithAlpha(color, 1f),
            WithAlpha(Darker(color, 0.85f), 0.9f),
            WithAlpha(Darker(color, 0.7f), 0f));

        ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));

        ps.Play();
        Object.Destroy(ps.gameObject, 3f);
    }

    // Destello de luz que se apaga rapido.
    public static void FlashLight(Vector3 position, Color color, float range, float intensity, float duration)
    {
        GameObject go = new GameObject("Destello");
        go.transform.position = position;

        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;

        go.AddComponent<LightFade>().duration = duration;   // despues de poner la intensidad: la lee al despertar
    }

    // ------------------------------------------------------------------ Humo

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

    // --------------------------------------------------------------- Explosion

    // Explosion de la Hot Sauce: bola de fuego, onda en el suelo, chorros de salsa roja, chispas,
    // manchas de salsa en el suelo, destello de luz y un poco de humo.
    public static void Explosion(Vector3 position, float radius)
    {
        Color sauceRed = new Color(0.8f, 0.05f, 0.08f);

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

        // 3) Salsa roja que salta por los aires, y chispas naranjas.
        Splat(position, Vector3.up, sauceRed, 26, radius * 2.4f, 0.3f);
        Splat(position, Vector3.up, new Color(1f, 0.7f, 0.2f), 18, radius * 3.5f, 0.1f);

        // 4) Manchas de salsa que se quedan en el suelo un rato.
        GroundStains(position, radius * 0.75f, sauceRed, 7, radius * 0.18f, radius * 0.42f, 2.5f);

        // 5) Destello de luz.
        FlashLight(position + Vector3.up * 0.5f, new Color(1f, 0.55f, 0.2f), radius * 3f, 5f, 0.35f);

        // 6) Humo oscuro, pequeno.
        SmokePuff(position + Vector3.up * 0.2f, Mathf.Max(0.5f, radius * 0.3f));
    }

    // Chispas de pimienta: polvo negro y chispitas grises al impactar (y antes de rebotar).
    public static void PepperBurst(Vector3 position, Vector3 direction)
    {
        Vector3 dir = (-direction + Vector3.up * 0.5f).normalized;
        Splat(position, dir, new Color(0.08f, 0.08f, 0.08f), 8, 3.2f, 0.09f);
        Splat(position, dir, new Color(0.6f, 0.6f, 0.58f), 5, 4.2f, 0.06f);
        FlashLight(position, new Color(0.9f, 0.9f, 0.85f), 2f, 0.8f, 0.12f);
    }
}