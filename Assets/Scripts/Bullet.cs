using System.Collections.Generic;
using UnityEngine;

// Va en el prefab de la bolita (Sphere.prefab).
// No necesita Collider ni Rigidbody: la colision la resuelve el raycast de abajo.
public class Bullet : MonoBehaviour
{
    [Header("Vuelo")]
    public float speed = 35f;
    [Tooltip("Segundos antes de destruirse sola si no choca con nada.")]
    public float lifetime = 3f;
    public int damage = 1;

    [Header("Aspecto")]
    [Tooltip("Giro sobre si misma mientras vuela. 0 para desactivar.")]
    public float spinSpeed = 540f;

    [Header("Impacto")]
    [Tooltip("Opcional: particulas o salpicadura al chocar.")]
    public GameObject impactEffect;

    private Transform shooter;
    private float age;

    // Salsa de pimienta negra: rebotes que le quedan y a que distancia busca el siguiente enemigo.
    private int bouncesLeft;
    private float bounceRange;

    // Enemigos que esta bala ya ha golpeado: no se repiten ni la frenan al rebotar.
    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();

    // La llama PlayerShooting justo despues de instanciar la bala.
    public void Init(Transform owner) => shooter = owner;

    private void Start()
    {
        GameAudio.Play(Sfx.Shoot, transform.position, 0.6f);
        bouncesLeft = SauceInventory.PepperBounces;
        bounceRange = SauceInventory.PepperRange;

        // Con una salsa activa la bolita se tine de su color.
        if (SauceInventory.TryGetActiveColor(out Color sauceColor))
        {
            TintBullet(sauceColor);
            AddTrail(sauceColor);
        }
    }

    // Estela del color de la salsa detras de la bala.
    private void AddTrail(Color color)
    {
        TrailRenderer tr = gameObject.AddComponent<TrailRenderer>();
        tr.sharedMaterial = SauceFX.SoftMaterial();
        tr.time = 0.25f;
        tr.startWidth = 0.18f;
        tr.endWidth = 0f;
        tr.minVertexDistance = 0.05f;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.colorGradient = SauceFX.MakeGradient(
            SauceFX.WithAlpha(color, 0.9f),
            SauceFX.WithAlpha(color, 0.6f),
            SauceFX.WithAlpha(color, 0.25f),
            SauceFX.WithAlpha(color, 0f));
    }

    // Tine la bala sin crear materiales nuevos (MaterialPropertyBlock).
    private void TintBullet(Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_Color", color);
        block.SetColor("_BaseColor", color);

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.SetPropertyBlock(block);
    }

    private void Update()
    {
        // El tiempo de vida se cuenta a mano (y no con Destroy(obj, t)) para poder reiniciarlo al rebotar.
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;

        if (spinSpeed > 0f)
            transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);

        // Se lanza el rayo por delante de lo que va a recorrer este frame:
        // asi no atraviesa paredes finas por ir rapida.
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step,
                            ~0, QueryTriggerInteraction.Ignore))
        {
            // Ni el tirador ni ninguno de sus hijos (pistola incluida) detienen la bala.
            bool isShooter = shooter != null && hit.collider.transform.IsChildOf(shooter);

            // Un enemigo que ya ha golpeado esta bala tampoco la detiene (al rebotar sale de su superficie).
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            bool alreadyHit = enemy != null && hitEnemies.Contains(enemy);

            if (isShooter || alreadyHit || hit.collider.CompareTag("Player"))
            {
                transform.position += transform.forward * step;
                return;
            }

            OnHit(hit, enemy);
            return;
        }

        transform.position += transform.forward * step;
    }

    private void OnHit(RaycastHit hit, EnemyHealth enemy)
    {
        if (enemy != null && !enemy.IsDead)
        {
            int before = enemy.CurrentHealth;
            enemy.TakeDamage(damage, transform.forward);
            int dealt = before - enemy.CurrentHealth;   // dano real: no cuenta mas de la vida que le quedaba
            hitEnemies.Add(enemy);

            // Salsa activa: fuego, miel, queso, curacion, explosion...
            SauceInventory.OnBulletHit(enemy, hit.point, transform.forward, dealt);

            if (impactEffect != null)
                Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));

            // Pimienta negra: en vez de morir, la bala sale hacia otro enemigo.
            if (TryBounce(hit.point)) return;
        }
        else if (impactEffect != null)
        {
            Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
        }

        Destroy(gameObject);
    }

    // Busca el enemigo vivo mas cercano al punto de impacto (que no haya sido golpeado ya) y se redirige hacia el.
    private bool TryBounce(Vector3 point)
    {
        if (bouncesLeft <= 0) return false;

        EnemyHealth best = null;
        Vector3 bestCenter = Vector3.zero;
        float bestSqr = bounceRange * bounceRange;

        foreach (EnemyHealth e in EnemyHealth.All)
        {
            if (e == null || e.IsDead || hitEnemies.Contains(e)) continue;

            Vector3 center = CenterOf(e);
            float sqr = (center - point).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = e;
                bestCenter = center;
            }
        }

        if (best == null) return false;

        Vector3 direction = bestCenter - point;
        if (direction.sqrMagnitude < 0.0001f) return false;

        transform.position = point;
        transform.rotation = Quaternion.LookRotation(direction.normalized);
        bouncesLeft--;
        age = 0f;   // cada rebote le da vida nueva
        return true;
    }

    private static Vector3 CenterOf(EnemyHealth e)
    {
        Collider c = e.GetComponent<Collider>();
        if (c == null) c = e.GetComponentInChildren<Collider>();
        return c != null ? c.bounds.center : e.transform.position + Vector3.up * 0.5f;
    }
}