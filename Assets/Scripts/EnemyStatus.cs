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
    private EnemyRoller roller;

    // Quemado
    private float burnLeft, burnTimer, burnInterval = 0.8f;
    private int burnDamage = 1;
    private GameObject fireRoot;
    private Light fireLight;
    private float flickerSeed;

    // Ralentizado (miel)
    private float slowLeft;
    private ParticleSystem honeyDrips, honeyTrail;
    private float chaserSpeed;
    private float controllerPatrolSpeed, controllerChaseSpeed;
    private float flyerFlySpeed, flyerDiveSpeed, flyerOrbitSpeed;
    private float rollerSpeed, rollerChargeSpeed;

    // Pegado al suelo (queso)
    private float rootLeft;
    private GameObject cheeseRoot;
    private Puddle cheesePuddle;
    private ParticleSystem cheeseBubbles;
    private GameObject stringsRoot;
    private Transform[] strings;
    private Vector3[] stringGround;      // donde se agarra cada hilo en el charco
    private Vector3[] stringAttach;      // donde se agarra al cuerpo (desplazamiento horizontal)
    private float stringThickness;
    private Vector3 bodyOffset;          // centro del cuerpo respecto al transform del enemigo
    private float bodyHeight;
    private RigidbodyConstraints savedConstraints;
    private bool savedGravity;
    private bool chaserWasOn, controllerWasOn, flyerWasOn, rollerWasOn;

    private enum TintKind { None, Burn, Cheese, Honey }
    private TintKind tint = TintKind.None;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody>();
        chaser = GetComponent<EnemyChaser>();
        controller = GetComponent<EnemyController>();
        flyer = GetComponent<EnemyFlyer>();
        roller = GetComponent<EnemyRoller>();
        flickerSeed = Random.value * 100f;

        if (health != null) health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;

        // Lo que no es hijo del enemigo (charco de queso, hilos) se limpia aparte.
        ReleaseCheese();
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
            else UpdateStrings();
        }

        UpdateTint();

        // La luz del fuego titila.
        if (fireLight != null)
            fireLight.intensity = 1.2f + Mathf.PerlinNoise(Time.time * 14f, flickerSeed) * 1.4f;
    }

    // Color del cuerpo segun el estado de mayor prioridad. Miel y queso ademas lo dejan brillante, como mojado.
    private void UpdateTint()
    {
        if (burnLeft > 0f)
        {
            float t = (Mathf.Sin(Time.time * 14f + flickerSeed) + 1f) * 0.5f;
            health.SetTint(Color.Lerp(new Color(1f, 0.2f, 0.03f), new Color(1f, 0.75f, 0.1f), t), 0.85f);
            SetKind(TintKind.Burn, false);
        }
        else if (rootLeft > 0f)
        {
            if (tint != TintKind.Cheese)
            {
                health.SetTint(new Color(1f, 0.85f, 0.2f), 0.8f);
                SetKind(TintKind.Cheese, true);
            }
        }
        else if (slowLeft > 0f)
        {
            if (tint != TintKind.Honey)
            {
                health.SetTint(new Color(0.95f, 0.45f, 0.04f), 0.85f);
                SetKind(TintKind.Honey, true);
            }
        }
        else if (tint != TintKind.None)
        {
            health.ClearTint();
            SetKind(TintKind.None, false);
        }
    }

    private void SetKind(TintKind kind, bool glossy)
    {
        bool wasGlossy = tint == TintKind.Cheese || tint == TintKind.Honey;
        tint = kind;

        if (glossy && !wasGlossy) health.SetGloss(0.9f);
        else if (!glossy && wasGlossy) health.SetGloss(-1f);
    }

    // Si muere ardiendo: nube de humo gris. Las llamas y el rastro de miel se quedan donde cayo,
    // y el charco de queso aguanta un poco en el suelo.
    private void HandleDied(EnemyHealth h)
    {
        if (burnLeft > 0f)
        {
            Bounds b = BodyBounds();
            SauceFX.SmokePuff(b.center, Mathf.Max(0.8f, b.size.y));
        }

        DismantleFire();
        DetachSystem(ref honeyDrips, 1.5f);
        DetachSystem(ref honeyTrail, 7f);
        ReleaseCheese();
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
        if (roller != null)
        {
            rollerSpeed = roller.speed;
            rollerChargeSpeed = roller.chargeSpeed;
            roller.speed = rollerSpeed * f;
            roller.chargeSpeed = rollerChargeSpeed * f;
        }

        BuildHoney();
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
        if (roller != null)
        {
            roller.speed = rollerSpeed;
            roller.chargeSpeed = rollerChargeSpeed;
        }

        DetachSystem(ref honeyDrips, 1.5f);
        DetachSystem(ref honeyTrail, 7f);   // las manchas del rastro se quedan en el suelo hasta que se desvanecen
    }

    // Salsa naranja: gotas gordas que caen del cuerpo y un rastro de manchas en el suelo por donde pasa.
    private void BuildHoney()
    {
        Bounds b = BodyBounds();
        float height = Mathf.Max(0.5f, b.size.y);
        float width = Mathf.Max(0.4f, Mathf.Max(b.size.x, b.size.z));
        Color honey = new Color(0.97f, 0.5f, 0.05f);

        // Gotas.
        honeyDrips = SauceFX.NewSystem("GotasMiel", transform, b.center, SauceFX.BlobMaterial());

        ParticleSystem.MainModule m = honeyDrips.main;
        m.loop = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = Color.white;
        m.gravityModifier = 1.1f;   // positivo = caen
        m.maxParticles = 40;

        ParticleSystem.EmissionModule e = honeyDrips.emission;
        e.rateOverTime = 18f;

        ParticleSystem.ShapeModule s = honeyDrips.shape;
        s.shapeType = ParticleSystemShapeType.Box;
        s.scale = new Vector3(width * 0.9f, height * 0.6f, width * 0.9f);

        ParticleSystem.ColorOverLifetimeModule c = honeyDrips.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            SauceFX.WithAlpha(honey, 1f),
            SauceFX.WithAlpha(honey, 1f),
            SauceFX.WithAlpha(SauceFX.Darker(honey, 0.85f), 0.9f),
            SauceFX.WithAlpha(SauceFX.Darker(honey, 0.7f), 0f));

        honeyDrips.Play();

        // Rastro: manchas planas en el suelo, una cada poco trecho. No tiene sentido en los voladores.
        if (flyer == null)
        {
            honeyTrail = SauceFX.NewSystem("RastroMiel", transform,
                                           new Vector3(b.center.x, b.min.y + 0.05f, b.center.z), SauceFX.BlobMaterial());

            ParticleSystem.MainModule tm = honeyTrail.main;
            tm.loop = true;
            tm.startLifetime = new ParticleSystem.MinMaxCurve(4f, 5.5f);
            tm.startSpeed = 0f;
            tm.startSize = new ParticleSystem.MinMaxCurve(width * 0.35f, width * 0.6f);
            tm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            tm.startColor = Color.white;
            tm.maxParticles = 150;

            ParticleSystem.EmissionModule te = honeyTrail.emission;
            te.rateOverTime = 1.5f;       // aunque este quieto va dejando algo
            te.rateOverDistance = 3f;     // y mas cuanto mas se mueva

            ParticleSystem.ShapeModule ts = honeyTrail.shape;
            ts.shapeType = ParticleSystemShapeType.Circle;
            ts.radius = width * 0.25f;
            ts.rotation = new Vector3(90f, 0f, 0f);   // reparte las manchas sobre el suelo

            ParticleSystem.ColorOverLifetimeModule tc = honeyTrail.colorOverLifetime;
            tc.enabled = true;
            tc.color = SauceFX.MakeGradient(
                SauceFX.WithAlpha(honey, 0.95f),
                SauceFX.WithAlpha(honey, 0.95f),
                SauceFX.WithAlpha(SauceFX.Darker(honey, 0.85f), 0.8f),
                SauceFX.WithAlpha(SauceFX.Darker(honey, 0.7f), 0f));

            honeyTrail.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            honeyTrail.Play();
        }
    }

    // Deja de emitir y suelta el sistema del enemigo para que las particulas que quedan se desvanezcan solas.
    private void DetachSystem(ref ParticleSystem ps, float destroyAfter)
    {
        if (ps == null) return;

        ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        ps.transform.SetParent(null, true);
        Destroy(ps.gameObject, destroyAfter);
        ps = null;
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
        rollerWasOn = roller != null && roller.enabled;
        if (roller != null) roller.enabled = false;
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
        if (roller != null && rollerWasOn) roller.enabled = true;

        ReleaseCheese();
    }

    // Charco de queso derretido en el suelo, con hilos que lo unen al enemigo y burbujas que revientan.
    private void BuildCheese()
    {
        Bounds b = BodyBounds();
        float width = Mathf.Max(0.8f, Mathf.Max(b.size.x, b.size.z));
        float groundY = FindGroundY(b);

        bodyOffset = b.center - transform.position;
        bodyHeight = Mathf.Max(0.5f, b.size.y);

        Mesh sphere = SauceFX.SphereMesh();
        Material cheese = SauceFX.CheeseMaterial();

        // Charco: crece de golpe desde el centro. Una mancha grande, cuatro mas pequenas y dos "bultos" de queso.
        cheeseRoot = new GameObject("QuesoDerretido");
        cheeseRoot.transform.position = new Vector3(b.center.x, groundY + 0.02f, b.center.z);
        cheesePuddle = cheeseRoot.AddComponent<Puddle>();

        SauceFX.AddFlatBlob(cheeseRoot.transform, sphere, cheese, Vector3.zero, width * 1.7f, 0.1f);
        for (int i = 0; i < 4; i++)
        {
            float angle = (i * 90f + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width * 0.7f;
            SauceFX.AddFlatBlob(cheeseRoot.transform, sphere, cheese, offset, width * Random.Range(0.55f, 0.9f), 0.1f);
        }
        for (int i = 0; i < 2; i++)
        {
            Vector2 disc = Random.insideUnitCircle * width * 0.35f;
            SauceFX.AddFlatBlob(cheeseRoot.transform, sphere, cheese,
                                new Vector3(disc.x, 0f, disc.y), width * Random.Range(0.4f, 0.6f), 0.4f);
        }

        // Burbujas que suben del charco y revientan.
        cheeseBubbles = SauceFX.NewSystem("BurbujasQueso", cheeseRoot.transform,
                                          cheeseRoot.transform.position + Vector3.up * 0.1f, SauceFX.RingMaterial());

        ParticleSystem.MainModule bm = cheeseBubbles.main;
        bm.loop = true;
        bm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        bm.startSpeed = 0f;
        bm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        bm.startColor = Color.white;
        bm.maxParticles = 30;

        ParticleSystem.EmissionModule be = cheeseBubbles.emission;
        be.rateOverTime = 10f;

        ParticleSystem.ShapeModule bs = cheeseBubbles.shape;
        bs.shapeType = ParticleSystemShapeType.Circle;
        bs.radius = width * 0.8f;
        bs.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule bv = cheeseBubbles.velocityOverLifetime;
        bv.enabled = true;
        bv.space = ParticleSystemSimulationSpace.World;
        bv.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        bv.y = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        bv.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        ParticleSystem.ColorOverLifetimeModule bc = cheeseBubbles.colorOverLifetime;
        bc.enabled = true;
        bc.color = SauceFX.MakeGradient(
            new Color(1f, 0.95f, 0.5f, 0f),
            new Color(1f, 0.92f, 0.45f, 0.9f),
            new Color(1f, 0.88f, 0.35f, 0.6f),
            new Color(1f, 0.85f, 0.3f, 0f));

        ParticleSystem.SizeOverLifetimeModule bz = cheeseBubbles.sizeOverLifetime;
        bz.enabled = true;
        bz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));

        cheeseBubbles.Play();

        // Ahora que esta todo colocado, el charco empieza a crecer (arranca a escala 0: antes de esto
        // colocar un hijo por posicion mundial daria valores invalidos).
        cheesePuddle.Begin(0.35f);

        // Hilos de queso estirado entre el charco y el cuerpo.
        const int stringCount = 5;
        stringsRoot = new GameObject("HilosQueso");
        strings = new Transform[stringCount];
        stringGround = new Vector3[stringCount];
        stringAttach = new Vector3[stringCount];
        stringThickness = Mathf.Clamp(width * 0.07f, 0.05f, 0.14f);

        Mesh cylinder = SauceFX.CylinderMesh();
        for (int i = 0; i < stringCount; i++)
        {
            float angle = (i * (360f / stringCount) + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            stringGround[i] = new Vector3(b.center.x, groundY + 0.04f, b.center.z) + dir * width * 0.65f;
            stringAttach[i] = dir * width * 0.22f;

            GameObject go = new GameObject("Hilo", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(stringsRoot.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = cylinder;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (cheese != null) mr.sharedMaterial = cheese;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            strings[i] = go.transform;
        }

        UpdateStrings();
    }

    // Recoloca los hilos: del borde del charco a la parte baja del cuerpo (se estiran si el enemigo cae).
    private void UpdateStrings()
    {
        if (stringsRoot == null || strings == null) return;

        Vector3 body = transform.position + bodyOffset;
        float attachY = body.y - bodyHeight * 0.3f;

        for (int i = 0; i < strings.Length; i++)
        {
            Vector3 top = new Vector3(body.x + stringAttach[i].x, attachY, body.z + stringAttach[i].z);
            Vector3 bottom = stringGround[i];
            Vector3 d = top - bottom;
            float length = d.magnitude;

            if (length < 0.05f)
            {
                strings[i].gameObject.SetActive(false);
                continue;
            }

            strings[i].gameObject.SetActive(true);
            strings[i].position = (top + bottom) * 0.5f;
            strings[i].rotation = Quaternion.FromToRotation(Vector3.up, d / length);
            strings[i].localScale = new Vector3(stringThickness, length * 0.5f, stringThickness);   // el cilindro mide 2 de alto
        }
    }

    // Suelta el queso: los hilos desaparecen y el charco aguanta un momento en el suelo antes de encogerse.
    private void ReleaseCheese()
    {
        if (stringsRoot != null)
        {
            Destroy(stringsRoot);
            stringsRoot = null;
            strings = null;
        }

        if (cheeseBubbles != null)
        {
            cheeseBubbles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            cheeseBubbles = null;
        }

        if (cheesePuddle != null)
        {
            cheesePuddle.FadeOut(2.5f, 1.5f);
            cheesePuddle = null;
        }

        cheeseRoot = null;
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