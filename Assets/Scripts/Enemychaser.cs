using UnityEngine;

// Enemigo que persigue SIEMPRE al jugador (sin patrulla ni cono de vision).
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyChaser : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 3.5f;
    public float acceleration = 20f;
    public float turnSpeed = 10f;

    [Header("Daño por contacto")]
    public int damageAmount = 1;
    public float damageCooldown = 1f;

    private Rigidbody rb;
    private Transform player;
    private float lastDamageTime = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        // Barra de vida automatica: no hace falta añadirla a mano al prefab.
        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        else Debug.LogWarning("[EnemyChaser] No hay objeto con tag Player en la escena.");
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        Vector3 direction = player.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;
        direction.Normalize();

        Vector3 current = rb.linearVelocity;
        Vector3 horizontal = new Vector3(current.x, 0f, current.z);
        horizontal = Vector3.MoveTowards(horizontal, direction * speed, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(horizontal.x, current.y, horizontal.z);

        Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.fixedDeltaTime);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (Time.time - lastDamageTime < damageCooldown) return;
        if (GameManager.instance == null) return;

        lastDamageTime = Time.time;
        GameManager.instance.TakeDamage(damageAmount);
    }
}
