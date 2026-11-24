using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    // Todos los enemigos vivos. Las salsas (explosion, rebote) buscan aqui a los cercanos.
    public static readonly List<EnemyHealth> All = new List<EnemyHealth>();

    [Header("Vida")]
    public int maxHealth = 3;
    public int scoreOnDeath = 50;

    [Header("Feedback")]
    public Color hitColor = Color.white;
    public float flashDuration = 0.08f;
    public float knockbackForce = 4f;

    [Tooltip("Opcional: prefab de particulas o similar al morir.")]
    public GameObject deathEffect;

    [Tooltip("Si esta activo, los disparos no empujan al enemigo (lo usa el queso: pegado al suelo).")]
    public bool ignoreKnockback;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    // (vidaActual, vidaMaxima). La barra de vida se suscribe a esto.
    public event System.Action<int, int> OnHealthChanged;

    // Se lanza justo antes de destruir al enemigo (EnemyStatus lo usa para el humo).
    public event System.Action<EnemyHealth> OnDied;

    private Renderer[] renderers;
    private Color[] originalColors;
    private Rigidbody rb;
    private DeathDebris debris;
    private bool isDead;

    // Tinte de estado (fuego, miel, queso). Lo gestiona EnemyStatus.
    private bool hasTint;
    private Color tintColor;
    private float tintStrength;
    private bool flashing;

    // Brillo de cada material (miel y queso lo suben para que parezca mojado de salsa).
    private string[] glossProps;
    private float[] originalGloss;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        debris = GetComponent<DeathDebris>();

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        glossProps = new string[renderers.Length];
        originalGloss = new float[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material m = renderers[i].material;
            originalColors[i] = m.color;

            // URP usa _Smoothness y el pipeline antiguo _Glossiness.
            if (m.HasProperty("_Smoothness")) glossProps[i] = "_Smoothness";
            else if (m.HasProperty("_Glossiness")) glossProps[i] = "_Glossiness";

            originalGloss[i] = glossProps[i] != null ? m.GetFloat(glossProps[i]) : 0f;
        }
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public void TakeDamage(int amount) => TakeDamage(amount, Vector3.zero);

    public void TakeDamage(int amount, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        StopAllCoroutines();
        StartCoroutine(Flash());

        if (rb != null && !rb.isKinematic && !ignoreKnockback && hitDirection != Vector3.zero)
        {
            hitDirection.y = 0f;
            rb.AddForce(hitDirection.normalized * knockbackForce, ForceMode.Impulse);
        }

        if (currentHealth <= 0) Die();
    }

    // Dano de quemadura (lo usa EnemyStatus): sin parpadeo blanco ni empujon,
    // para que se vea el color y el fuego del enemigo ardiendo.
    public void TakeBurnDamage(int amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0) Die();
    }

    // ------------------------------------------------------------- Tinte

    // Mezcla el color original del enemigo con "color". strength: 0 = nada, 1 = todo el color.
    public void SetTint(Color color, float strength)
    {
        hasTint = true;
        tintColor = color;
        tintStrength = Mathf.Clamp01(strength);
        if (!flashing) ApplyBaseColors();
    }

    public void ClearTint()
    {
        if (!hasTint) return;

        hasTint = false;
        if (!flashing) ApplyBaseColors();
    }

    // Sube el brillo de todos los materiales (0..1). Con un valor negativo vuelve al brillo original.
    public void SetGloss(float value)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || glossProps[i] == null) continue;
            renderers[i].material.SetFloat(glossProps[i], value < 0f ? originalGloss[i] : value);
        }
    }

    private void ApplyBaseColors()
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            Color c = originalColors[i];
            if (hasTint) c = Color.Lerp(c, tintColor, tintStrength);
            renderers[i].material.color = c;
        }
    }

    private IEnumerator Flash()
    {
        flashing = true;
        foreach (Renderer r in renderers)
            if (r != null) r.material.color = hitColor;

        yield return new WaitForSeconds(flashDuration);

        flashing = false;
        ApplyBaseColors();   // vuelve al color normal, o al del tinte si esta ardiendo / pegajoso
    }

    private void Die()
    {
        isDead = true;
        OnDied?.Invoke(this);

        if (GameManager.instance != null)
            GameManager.instance.AddScore(scoreOnDeath);

        // Las bolitas se lanzan ANTES de destruir el objeto: salen como hijas de la escena.
        if (debris != null) debris.Burst();

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}