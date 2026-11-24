using UnityEngine;

// Estados sobre un enemigo: QUEMADO (salsa Buffalo), RALENTIZADO (miel) y PEGADO AL SUELO (queso).
// Se anade solo al enemigo con EnemyStatus.Burn / Slow / Root y se queda ahi.
// Es el unico que tine al enemigo (a traves de EnemyHealth.SetTint), asi los efectos
// no se pisan entre si. Prioridad de color: fuego > queso > miel.
[DisallowMultipleComponent]
public class EnemyStatus : MonoBehaviour
{
    // ------------------------------------------------------------------ API

    // Prende fuego (o reinicia el tiempo si ya arde).
    public static void Burn(EnemyHealth target, float duration, int damagePerTick, float tickInterval)
    {
        EnemyStatus s = GetOrAdd(target);
        if (s != null) s.StartBurn(duration, damagePerTick, tickInterval);
    }

    // Reduce la velocidad: speedFactor 0.35 = va al 35 % de su velocidad.
    public static void Slow(EnemyHealth target, float speedFactor, float duration)
    {
        EnemyStatus s = GetOrAdd(target);
        if (s != null) s.StartSlow(speedFactor, duration);
    }

    // Lo pega al suelo: no se mueve ni le empujan los disparos. Los voladores caen al suelo.
    public static void Root(EnemyHealth target, float duration)
    {
        EnemyStatus s = GetOrAdd(target);
        if (s != null) s.StartRoot(duration);
    }

    private static EnemyStatus GetOrAdd(EnemyHealth target)
    {
        if (target == null || target.IsDead) return null;

        EnemyStatus s = target.GetComponent<EnemyStatus>();
        if (s == null) s = target.gameObject.AddComponent<EnemyStatus>();
        return s;
    }

    // ---------------------------------------------------------------- Estado
    private EnemyHealth health;
    private Rigidbody rb;
    private EnemyChaser chaser;
    private EnemyController controller;
    private EnemyFlyer flyer;

    // Quemado
    private float burnLeft, burnTimer, burnInterval = 0.8f;
    private int burnDamage = 1;
    private GameObject fireRoot;
    private Light fireLight;
    private float flickerSeed;

    // Ralentizado
    private float slowLeft;
    private ParticleSystem drips;
    private float chaserSpeed;
    private float controllerPatrolSpeed, controllerChaseSpeed;
    private float flyerFlySpeed, flyerDiveSpeed, flyerOrbitSpeed;

    // Pegado al suelo
    private float rootLeft;
    private GameObject cheeseRoot;
    private RigidbodyConstraints savedConstraints;
    private bool savedGravity;
    private bool chaserWasOn, controllerWasOn, flyerWasOn;

    private enum TintKind { None, Burn, Cheese, Honey }
    private TintKind tint = TintKind.None;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody>();
        chaser = GetComponent<EnemyChaser>();
        controller = GetComponent<EnemyController>();
        flyer = GetComponent<EnemyFlyer>();
        flickerSeed = Random.value * 100f;

        if (health != null) health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;

        // El charco de queso no es hijo del enemigo (se queda en el suelo): se limpia aparte.
        if (cheeseRoot != null) Destroy(cheeseRoot);
    }

    private void Update()
    {
        if (health == null) { Destroy(this); return; }
        if (health.IsDead) return;

        float dt = Time.deltaTime;

        if (burnLeft > 0f)
        {
            burnLeft -= dt;
            burnTimer -= dt;

            if (burnTimer <= 0f)
            {
                burnTimer += burnInterval;
                health.TakeBurnDamage(burnDamage);
                if (health.IsDead) return;   // murio quemado: HandleDied ya se ha encargado del humo
            }

            if (burnLeft <= 0f) StopBurn();
        }

        if (slowLeft > 0f)
        {
            slowLeft -= dt;
            if (slowLeft <= 0f) StopSlow();
        }

        if (rootLeft > 0f)
        {
            rootLeft -= dt;
            if (rootLeft <= 0f) StopRoot();
        }

        UpdateTint();

        // La luz del fuego titila.
        if (fireLight != null)
            fireLight.intensity = 1.2f + Mathf.PerlinNoise(Time.time * 14f, flickerSeed) * 1.4f;
    }

    // Color del cuerpo segun el estado de mayor prioridad.
    private void UpdateTint()
    {
        if (burnLeft > 0f)
        {
            float t = (Mathf.Sin(Time.time * 14f + flickerSeed) + 1f) * 0.5f;
            health.SetTint(Color.Lerp(new Color(1f, 0.2f, 0.03f), new Color(1f, 0.75f, 0.1f), t), 0.85f);
            tint = TintKind.Burn;
        }
        else if (rootLeft > 0f)
        {
            if (tint != TintKind.Cheese)
            {
                health.SetTint(new Color(1f, 0.88f, 0.25f), 0.7f);
                tint = TintKind.Cheese;
            }
        }
        else if (slowLeft > 0f)
        {
            if (tint != TintKind.Honey)
            {
                health.SetTint(new Color(0.95f, 0.55f, 0.08f), 0.55f);
                tint = TintKind.Honey;
            }
        }
        else if (tint != TintKind.None)
        {
            health.ClearTint();
            tint = TintKind.None;
        }
    }

    // Si muere ardiendo: nube de humo gris. Las llamas se quedan un momento donde cayo.
    private void HandleDied(EnemyHealth h)
    {
        if (burnLeft > 0f)
        {
            Bounds b = BodyBounds();
            SauceFX.SmokePuff(b.center, Mathf.Max(0.8f, b.size.y));
        }

        DismantleFire();
    }

    // ---------------------------------------------------------------- Fuego
    private void StartBurn(float duration, int damagePerTick, float tickInterval)
    {
        burnDamage = damagePerTick;
        burnInterval = Mathf.Max(0.05f, tickInterval);

        bool alreadyBurning = burnLeft > 0f;
        burnLeft = duration;
        if (alreadyBurning) return;

        burnTimer = burnInterval;
        BuildFire();
    }

    private void StopBurn()
    {
        burnLeft = 0f;
        DismantleFire();
    }

    private void BuildFire()
    {
        Bounds b = BodyBounds();
        float height = Mathf.Max(0.5f, b.size.y);
        float width = Mathf.Max(0.4f, Mathf.Max(b.size.x, b.size.z));

        fireRoot = new GameObject("Fuego");
        fireRoot.transform.SetParent(transform, false);
        fireRoot.transform.position = b.center;

        // Llamas: salen de TODO el volumen del cuerpo y suben, asi parece que arde entero.
        ParticleSystem flames = SauceFX.NewSystem("Llamas", fireRoot.transform, b.center, SauceFX.SoftMaterial());

        ParticleSystem.MainModule fm = flames.main;
        fm.loop = true;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        fm.startSpeed = 0f;
        fm.startSize = new ParticleSystem.MinMaxCurve(height * 0.35f, height * 0.75f);
        fm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fm.startColor = Color.white;
        fm.maxParticles = 90;

        ParticleSystem.EmissionModule fe = flames.emission;
        fe.rateOverTime = 70f;

        ParticleSystem.ShapeModule fs = flames.shape;
        fs.shapeType = ParticleSystemShapeType.Box;
        fs.scale = new Vector3(width * 0.8f, height * 0.8f, width * 0.8f);

        ParticleSystem.VelocityOverLifetimeModule fv = flames.velocityOverLifetime;
        fv.enabled = true;
        fv.space = ParticleSystemSimulationSpace.World;
        fv.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        fv.y = new ParticleSystem.MinMaxCurve(height * 1.2f, height * 2.2f);
        fv.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        ParticleSystem.ColorOverLifetimeModule fc = flames.colorOverLifetime;
        fc.enabled = true;
        fc.color = SauceFX.MakeGradient(
            new Color(1f, 0.95f, 0.5f, 0.9f),
            new Color(1f, 0.6f, 0.1f, 0.85f),
            new Color(0.9f, 0.2f, 0.05f, 0.55f),
            new Color(0.25f, 0.1f, 0.05f, 0f));

        ParticleSystem.SizeOverLifetimeModule fz = flames.sizeOverLifetime;
        fz.enabled = true;
        fz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

        flames.Play();

        // Chispas: puntitos brillantes que saltan hacia arriba.
        ParticleSystem embers = SauceFX.NewSystem("Chispas", fireRoot.transform, b.center, SauceFX.SoftMaterial());

        ParticleSystem.MainModule em = embers.main;
        em.loop = true;
        em.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        em.startSpeed = 0f;
        em.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        em.startColor = Color.white;
        em.maxParticles = 40;

        ParticleSystem.EmissionModule ee = embers.emission;
        ee.rateOverTime = 22f;

        ParticleSystem.ShapeModule es = embers.shape;
        es.shapeType = ParticleSystemShapeType.Box;
        es.scale = new Vector3(width * 0.8f, height * 0.8f, width * 0.8f);

        ParticleSystem.VelocityOverLifetimeModule ev = embers.velocityOverLifetime;
        ev.enabled = true;
        ev.space = ParticleSystemSimulationSpace.World;
        ev.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        ev.y = new ParticleSystem.MinMaxCurve(height * 2f, height * 3.5f);
        ev.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        ParticleSystem.ColorOverLifetimeModule ec = embers.colorOverLifetime;
        ec.enabled = true;
        ec.color = SauceFX.MakeGradient(
            new Color(1f, 0.95f, 0.5f, 1f),
            new Color(1f, 0.75f, 0.2f, 1f),
            new Color(1f, 0.4f, 0.1f, 0.6f),
            new Color(0.8f, 0.2f, 0.05f, 0f));

        embers.Play();

        // Luz naranja que titila y ilumina el suelo de alrededor.
        fireLight = fireRoot.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.5f, 0.15f);
        fireLight.range = Mathf.Max(3f, height * 3f);
        fireLight.intensity = 1.8f;
        fireLight.shadows = LightShadows.None;
    }

    // Quita el fuego: deja de emitir y se separa del enemigo para que las llamas se apaguen solas.
    private void DismantleFire()
    {
        if (fireRoot == null) return;

        if (fireLight != null) fireLight.enabled = false;

        foreach (ParticleSystem ps in fireRoot.GetComponentsInChildren<ParticleSystem>())
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        fireRoot.transform.SetParent(null, true);
        Destroy(fireRoot, 1.6f);

        fireRoot = null;
        fireLight = null;
    }

    // --------------------------------------------------------------- Miel
    private void StartSlow(float speedFactor, float duration)
    {
        bool alreadySlow = slowLeft > 0f;
        slowLeft = duration;
        if (alreadySlow) return;   // ya va lento: solo se reinicia el tiempo

        float f = Mathf.Clamp(speedFactor, 0.05f, 1f);

        // Las velocidades son campos publicos de los scripts de movimiento: se guardan y se restauran al acabar.
        if (chaser != null)
        {
            chaserSpeed = chaser.speed;
            chaser.speed = chaserSpeed * f;
        }
        if (controller != null)
        {
            controllerPatrolSpeed = controller.patrolSpeed;
            controllerChaseSpeed = controller.chaseSpeed;
            controller.patrolSpeed = controllerPatrolSpeed * f;
            controller.chaseSpeed = controllerChaseSpeed * f;
        }
        if (flyer != null)
        {
            flyerFlySpeed = flyer.flySpeed;
            flyerDiveSpeed = flyer.diveSpeed;
            flyerOrbitSpeed = flyer.orbitSpeed;
            flyer.flySpeed = flyerFlySpeed * f;
            flyer.diveSpeed = flyerDiveSpeed * f;
            flyer.orbitSpeed = flyerOrbitSpeed * f;
        }

        BuildDrips();
    }

    private void StopSlow()
    {
        slowLeft = 0f;

        if (chaser != null) chaser.speed = chaserSpeed;
        if (controller != null)
        {
            controller.patrolSpeed = controllerPatrolSpeed;
            controller.chaseSpeed = controllerChaseSpeed;
        }
        if (flyer != null)
        {
            flyer.flySpeed = flyerFlySpeed;
            flyer.diveSpeed = flyerDiveSpeed;
            flyer.orbitSpeed = flyerOrbitSpeed;
        }

        if (drips != null)
        {
            drips.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            drips.transform.SetParent(null, true);
            Destroy(drips.gameObject, 1.5f);
            drips = null;
        }
    }

    // Gotas de miel que caen del cuerpo.
    private void BuildDrips()
    {
        Bounds b = BodyBounds();
        float height = Mathf.Max(0.5f, b.size.y);
        float width = Mathf.Max(0.4f, Mathf.Max(b.size.x, b.size.z));

        drips = SauceFX.NewSystem("GotasMiel", transform, b.center, SauceFX.SoftMaterial());

        ParticleSystem.MainModule m = drips.main;
        m.loop = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
        m.startColor = Color.white;
        m.gravityModifier = 0.5f;   // positivo = caen
        m.maxParticles = 30;

        ParticleSystem.EmissionModule e = drips.emission;
        e.rateOverTime = 12f;

        ParticleSystem.ShapeModule s = drips.shape;
        s.shapeType = ParticleSystemShapeType.Box;
        s.scale = new Vector3(width * 0.9f, height * 0.6f, width * 0.9f);

        ParticleSystem.ColorOverLifetimeModule c = drips.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            new Color(0.98f, 0.65f, 0.1f, 0.95f),
            new Color(0.92f, 0.55f, 0.08f, 0.95f),
            new Color(0.8f, 0.42f, 0.05f, 0.8f),
            new Color(0.7f, 0.35f, 0.05f, 0f));

        drips.Play();
    }

    // -------------------------------------------------------------- Queso
    private void StartRoot(float duration)
    {
        bool alreadyRooted = rootLeft > 0f;
        rootLeft = duration;
        if (alreadyRooted) return;

        // Se apagan los scripts de movimiento: asi el enemigo no puede ni andar ni volar.
        chaserWasOn = chaser != null && chaser.enabled;
        controllerWasOn = controller != null && controller.enabled;
        flyerWasOn = flyer != null && flyer.enabled;
        if (chaser != null) chaser.enabled = false;
        if (controller != null) controller.enabled = false;
        if (flyer != null) flyer.enabled = false;

        if (rb != null)
        {
            savedConstraints = rb.constraints;
            savedGravity = rb.useGravity;

            // Se queda clavado en horizontal; en vertical sigue libre: los voladores caen al suelo.
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
            rb.constraints = savedConstraints
                           | RigidbodyConstraints.FreezePositionX
                           | RigidbodyConstraints.FreezePositionZ
                           | RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
        }

        health.ignoreKnockback = true;
        BuildCheese();
    }

    private void StopRoot()
    {
        rootLeft = 0f;

        if (rb != null)
        {
            rb.constraints = savedConstraints;
            rb.useGravity = savedGravity;
        }

        health.ignoreKnockback = false;

        if (chaser != null && chaserWasOn) chaser.enabled = true;
        if (controller != null && controllerWasOn) controller.enabled = true;
        if (flyer != null && flyerWasOn) flyer.enabled = true;

        if (cheeseRoot != null)
        {
            Destroy(cheeseRoot);
            cheeseRoot = null;
        }
    }

    // Charco de queso derretido en el suelo, justo debajo del enemigo.
    private void BuildCheese()
    {
        Bounds b = BodyBounds();
        float width = Mathf.Max(0.8f, Mathf.Max(b.size.x, b.size.z));
        float groundY = FindGroundY(b);

        cheeseRoot = new GameObject("QuesoDerretido");
        cheeseRoot.transform.position = new Vector3(b.center.x, groundY + 0.02f, b.center.z);

        Mesh mesh = SauceFX.SphereMesh();
        Material mat = SauceFX.CheeseMaterial();

        // Una mancha grande en el centro y cuatro mas pequenas alrededor.
        AddBlob(cheeseRoot.transform, mesh, mat, Vector3.zero, width * 1.5f);
        for (int i = 0; i < 4; i++)
        {
            float angle = (i * 90f + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width * 0.6f;
            AddBlob(cheeseRoot.transform, mesh, mat, offset, width * Random.Range(0.5f, 0.8f));
        }
    }

    private static void AddBlob(Transform parent, Mesh mesh, Material mat, Vector3 localOffset, float size)
    {
        GameObject go = new GameObject("Mancha", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;
        go.transform.localScale = new Vector3(size, size * 0.12f, size);

        go.GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Rayo hacia abajo ignorando al propio enemigo y al jugador.
    private float FindGroundY(Bounds b)
    {
        Ray ray = new Ray(new Vector3(b.center.x, b.center.y + 1f, b.center.z), Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 40f, ~0, QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;
        float result = b.min.y;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c.transform.IsChildOf(transform)) continue;
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

    // ------------------------------------------------------------ Utilidades

    // Caja que envuelve las mallas del cuerpo (sin particulas, sprites ni la barra de vida).
    private Bounds BodyBounds()
    {
        Bounds b = default;
        bool found = false;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            if (!found) { b = r.bounds; found = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!found) b = new Bounds(transform.position + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f));
        return b;
    }
}
