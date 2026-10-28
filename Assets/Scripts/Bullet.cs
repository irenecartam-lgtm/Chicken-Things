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

    // La llama PlayerShooting justo despues de instanciar la bala.
    public void Init(Transform owner) => shooter = owner;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
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

            if (isShooter || hit.collider.CompareTag("Player"))
            {
                transform.position += transform.forward * step;
                return;
            }

            OnHit(hit);
            return;
        }

        transform.position += transform.forward * step;
    }

    private void OnHit(RaycastHit hit)
    {
        EnemyHealth health = hit.collider.GetComponentInParent<EnemyHealth>();
        if (health != null) health.TakeDamage(damage, transform.forward);

        if (impactEffect != null)
            Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));

        Destroy(gameObject);
    }
}