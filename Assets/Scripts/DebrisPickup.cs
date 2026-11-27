using UnityEngine;

// Va en cada bolita (resto) que deja un enemigo al morir. Lo anade DeathDebris.Burst().
// Cuando el jugador se acerca, la bolita vuela hacia el y se recoge: suma al contador
// de SauceInventory. Los parametros (radios, velocidad...) se ajustan en SauceInventory.
public class DebrisPickup : MonoBehaviour
{
    private float age;

    private void Update()
    {
        SauceInventory inv = SauceInventory.Instance;
        if (inv == null) return;

        // Una pausa corta antes de poder cogerla: asi no se aspiran en el mismo instante de morir el enemigo.
        age += Time.deltaTime;
        if (age < inv.pickupDelay) return;

        Transform player = inv.PlayerTransform;
        if (player == null) return;

        Vector3 target = player.position + Vector3.up * inv.pickupHeight;
        Vector3 toPlayer = target - transform.position;
        float distance = toPlayer.magnitude;

        if (distance <= inv.collectRadius)
        {
            inv.Collect(1);
            Destroy(gameObject);
            return;
        }

        if (distance <= inv.magnetRadius)
        {
            // Cuanto mas cerca, mas rapido vuela hacia el jugador.
            float closeness = 1f - distance / inv.magnetRadius;
            float speed = Mathf.Lerp(inv.magnetMinSpeed, inv.magnetMaxSpeed, closeness);
            float step = Mathf.Min(speed * Time.deltaTime, distance);
            transform.position += toPlayer / distance * step;
        }
    }
}
