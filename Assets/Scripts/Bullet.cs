using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 35f;
    public float lifetime = 3f;
    public int damage = 1;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;

        // Raycast por delante de lo que va a recorrer este frame:
        // asi no atraviesa paredes finas por ir rapido.
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, step,
                            ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Player"))
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
        if (health != null) health.TakeDamage(damage);

        Destroy(gameObject);
    }
}