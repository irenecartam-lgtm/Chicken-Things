using UnityEngine;

// Efecto de curacion del jugador, estilo Overwatch. Sale del propio personaje y le acompana al moverse:
//   - cruces "+" blancas que flotan hacia arriba alrededor del cuerpo
//   - una cruz grande que salta sobre la cabeza
//   - destellos que suben
//   - un aura blanca con la forma del cuerpo (solo en tercera persona; en primera taparia la pantalla)
//   - una onda y un charco de luz blanca en el suelo bajo los pies
// HealEffect.Play() lo llama GameManager.Heal cada vez que el jugador recupera vida.
// Se crea solo, como hijo del jugador (el objeto con tag Player).
public class HealEffect : MonoBehaviour
{
    private static HealEffect instance;

    private ParticleSystem plus, headPlus, sparkles, ring, pad, aura;
    private float chestHeight;   // altura del pecho sobre el origen del jugador

    // Una curacion: tanda completa de efectos.
    public static void Play()
    {
        HealEffect fx = Get();
        if (fx != null) fx.Pulse();
    }

    // Un orbe de vida llega al jugador: solo unos destellos (la curacion en si la hace Play).
    public static void Absorb(bool big)
    {
        HealEffect fx = Get();
        if (fx != null) fx.sparkles.Emit(big ? 5 : 2);
    }

    private static HealEffect Get()
    {
        if (instance != null) return instance;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;

        instance = new GameObject("HealEffect").AddComponent<HealEffect>();
        instance.transform.SetParent(player.transform, false);
        instance.Build(player);
        return instance;
    }

    private void Pulse()
    {
        plus.Emit(6);
        headPlus.Emit(1);
        sparkles.Emit(14);
        ring.Emit(1);
        pad.Emit(1);

        // El aura es un panel translucido delante del personaje: en primera persona la camara esta
        // dentro y se veria una neblina blanca por toda la pantalla, asi que solo se usa con la camara lejos.
        if (CameraIsOutside()) aura.Emit(1);
    }

    private bool CameraIsOutside()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 chest = transform.position + Vector3.up * chestHeight;
        return (cam.transform.position - chest).sqrMagnitude > 2.2f * 2.2f;
    }

    private void Build(GameObject player)
    {
        // Tamano del personaje: del collider de su objeto raiz.
        Bounds b;
        Collider col = player.GetComponent<Collider>();
        if (col != null) b = col.bounds;
        else b = new Bounds(player.transform.position + Vector3.up, new Vector3(1f, 2f, 1f));

        float height = Mathf.Max(1f, b.size.y);
        float radius = Mathf.Max(0.35f, Mathf.Max(b.size.x, b.size.z) * 0.5f);
        Vector3 feet = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
        Vector3 head = new Vector3(b.center.x, b.max.y + 0.35f, b.center.z);
        chestHeight = b.center.y - player.transform.position.y;

        BuildPlus(feet, height, radius);
        BuildHeadPlus(head);
        BuildSparkles(feet, height, radius);
        BuildRing(feet, radius);
        BuildPad(feet, radius);
        BuildAura(b.center, height, radius);
    }

    // Cruces blancas que suben desde los pies, rodeando el cuerpo, con un balanceo suave.
    private void BuildPlus(Vector3 feet, float height, float radius)
    {
        plus = NewLocalSystem("Cruces", feet, SauceFX.PlusMaterial());

        ParticleSystem.MainModule m = plus.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
        m.maxParticles = 60;

        ParticleSystem.ShapeModule s = plus.shape;
        s.shapeType = ParticleSystemShapeType.Circle;
        s.radius = radius * 1.15f;
        s.radiusThickness = 0.35f;              // salen cerca del borde del circulo, rodeando al personaje
        s.rotation = new Vector3(90f, 0f, 0f);  // circulo tumbado en el suelo

        ParticleSystem.VelocityOverLifetimeModule v = plus.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        v.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        v.y = new ParticleSystem.MinMaxCurve(height * 0.55f, height * 0.9f);
        v.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

        ParticleSystem.NoiseModule n = plus.noise;
        n.enabled = true;
        n.strength = 0.3f;
        n.frequency = 0.8f;
        n.scrollSpeed = 0.6f;

        ParticleSystem.ColorOverLifetimeModule c = plus.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 0.92f, 1f),
            new Color(1f, 0.97f, 0.75f, 0.9f),
            new Color(1f, 0.95f, 0.7f, 0f));

        AnimationCurve grow = new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.7f));
        ParticleSystem.SizeOverLifetimeModule z = plus.sizeOverLifetime;
        z.enabled = true;
        z.size = new ParticleSystem.MinMaxCurve(1f, grow);

        plus.Play();
    }

    // Una cruz grande que "salta" sobre la cabeza, como el icono de vida de muchos juegos.
    private void BuildHeadPlus(Vector3 head)
    {
        headPlus = NewLocalSystem("CruzCabeza", head, SauceFX.PlusMaterial());

        ParticleSystem.MainModule m = headPlus.main;
        m.startLifetime = 0.9f;
        m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 0.75f);
        m.maxParticles = 4;

        ParticleSystem.ShapeModule s = headPlus.shape;
        s.enabled = false;

        ParticleSystem.VelocityOverLifetimeModule v = headPlus.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        v.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        v.y = new ParticleSystem.MinMaxCurve(0.5f, 0.6f);
        v.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ColorOverLifetimeModule c = headPlus.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            new Color(1f, 1f, 1f, 0.2f),
            new Color(1f, 1f, 0.95f, 1f),
            new Color(1f, 0.97f, 0.8f, 0.9f),
            new Color(1f, 0.95f, 0.7f, 0f));

        // Entra con un "pop": pasa de pequena a un poco mas grande que su tamano y se asienta.
        AnimationCurve pop = new AnimationCurve(
            new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.9f));
        ParticleSystem.SizeOverLifetimeModule z = headPlus.sizeOverLifetime;
        z.enabled = true;
        z.size = new ParticleSystem.MinMaxCurve(1f, pop);

        headPlus.Play();
    }

    // Destellos: puntitos blancos mas rapidos y pequenos.
    private void BuildSparkles(Vector3 feet, float height, float radius)
    {
        sparkles = NewLocalSystem("Destellos", feet, SauceFX.SoftMaterial());

        ParticleSystem.MainModule m = sparkles.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        m.maxParticles = 80;

        ParticleSystem.ShapeModule s = sparkles.shape;
        s.shapeType = ParticleSystemShapeType.Circle;
        s.radius = radius;
        s.radiusThickness = 1f;                  // por todo el circulo
        s.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule v = sparkles.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        v.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        v.y = new ParticleSystem.MinMaxCurve(height * 0.8f, height * 1.5f);
        v.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        ParticleSystem.NoiseModule n = sparkles.noise;
        n.enabled = true;
        n.strength = 0.25f;
        n.frequency = 1.2f;

        ParticleSystem.ColorOverLifetimeModule c = sparkles.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            new Color(1f, 1f, 1f, 0f),
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 1f, 0.9f, 0.8f),
            new Color(1f, 1f, 0.85f, 0f));

        sparkles.Play();
    }

    // Onda blanca que se abre en el suelo alrededor de los pies.
    private void BuildRing(Vector3 feet, float radius)
    {
        ring = NewLocalSystem("Onda", feet, SauceFX.RingMaterial());

        ParticleSystem.MainModule m = ring.main;
        m.startLifetime = 0.6f;
        m.startSize = radius * 3f;
        m.maxParticles = 6;

        ParticleSystem.ShapeModule s = ring.shape;
        s.enabled = false;

        ParticleSystem.ColorOverLifetimeModule c = ring.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            new Color(1f, 1f, 1f, 0.9f),
            new Color(1f, 1f, 0.95f, 0.65f),
            new Color(1f, 1f, 0.9f, 0.3f),
            new Color(1f, 1f, 0.9f, 0f));

        ParticleSystem.SizeOverLifetimeModule z = ring.sizeOverLifetime;
        z.enabled = true;
        z.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.35f, 1f, 1f));

        ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        ring.Play();
    }

    // Charco de luz suave bajo los pies, que se apaga despacio.
    private void BuildPad(Vector3 feet, float radius)
    {
        pad = NewLocalSystem("LuzSuelo", feet, SauceFX.SoftMaterial());

        ParticleSystem.MainModule m = pad.main;
        m.startLifetime = 0.8f;
        m.startSize = radius * 3.4f;
        m.maxParticles = 6;

        ParticleSystem.ShapeModule s = pad.shape;
        s.enabled = false;

        ParticleSystem.ColorOverLifetimeModule c = pad.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            new Color(1f, 1f, 0.95f, 0f),
            new Color(1f, 1f, 0.92f, 0.55f),
            new Color(1f, 1f, 0.9f, 0.25f),
            new Color(1f, 1f, 0.9f, 0f));

        ParticleSystem.SizeOverLifetimeModule z = pad.sizeOverLifetime;
        z.enabled = true;
        z.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1f));

        pad.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        pad.Play();
    }

    // Aura: un resplandor blanco con forma de elipse vertical (la silueta del personaje) que se desvanece.
    private void BuildAura(Vector3 center, float height, float radius)
    {
        aura = NewLocalSystem("Aura", center, SauceFX.SoftMaterial());

        ParticleSystem.MainModule m = aura.main;
        m.startLifetime = 0.7f;
        m.startSize3D = true;                        // ancho y alto distintos: una elipse alta y estrecha
        m.startSizeX = radius * 2.8f;
        m.startSizeY = height * 1.4f;
        m.startSizeZ = 1f;
        m.maxParticles = 6;

        ParticleSystem.ShapeModule s = aura.shape;
        s.enabled = false;

        ParticleSystem.ColorOverLifetimeModule c = aura.colorOverLifetime;
        c.enabled = true;
        c.color = SauceFX.MakeGradient(
            new Color(1f, 1f, 0.95f, 0f),
            new Color(1f, 1f, 0.92f, 0.32f),
            new Color(1f, 1f, 0.9f, 0.16f),
            new Color(1f, 1f, 0.9f, 0f));

        ParticleSystem.SizeOverLifetimeModule z = aura.sizeOverLifetime;
        z.enabled = true;
        z.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.85f, 1f, 1.1f));

        // Vertical: siempre de pie, aunque la camara mire desde arriba.
        aura.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
        aura.Play();
    }

    // Sistema que solo emite cuando se le pide (Emit) y cuyas particulas viajan con el jugador (espacio local).
    private ParticleSystem NewLocalSystem(string systemName, Vector3 worldPosition, Material material)
    {
        ParticleSystem ps = SauceFX.NewSystem(systemName, transform, worldPosition, material);

        ParticleSystem.MainModule m = ps.main;
        m.loop = true;
        m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.startSpeed = 0f;
        m.startRotation = 0f;
        m.startColor = Color.white;

        ParticleSystem.EmissionModule e = ps.emission;
        e.rateOverTime = 0f;

        return ps;
    }
}