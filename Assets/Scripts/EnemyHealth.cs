using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 3;
    public int scoreOnDeath = 50;

    [Header("Feedback")]
    public Color hitColor = Color.white;
    public float flashDuration = 0.08f;
    public float knockbackForce = 4f;

    [Tooltip("Opcional: prefab de particulas o similar al morir.")]
    public GameObject deathEffect;

    private int currentHealth;

    public int CurrentHealth => currentHealth;

    // (vidaActual, vidaMaxima). La barra de vida se suscribe a esto.
    public event System.Action<int, int> OnHealthChanged;

    private Renderer[] renderers;
    private Color[] originalColors;
    private Rigidbody rb;
    private bool isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;
    }

    public void TakeDamage(int amount) => TakeDamage(amount, Vector3.zero);

    public void TakeDamage(int amount, Vector3 hitDirection)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        StopAllCoroutines();
        StartCoroutine(Flash());

        if (rb != null && !rb.isKinematic && hitDirection != Vector3.zero)
        {
            hitDirection.y = 0f;
            rb.AddForce(hitDirection.normalized * knockbackForce, ForceMode.Impulse);
        }

        if (currentHealth <= 0) Die();
    }

    private IEnumerator Flash()
    {
        foreach (Renderer r in renderers) r.material.color = hitColor;
        yield return new WaitForSeconds(flashDuration);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].material.color = originalColors[i];
        }
    }

    private void Die()
    {
        isDead = true;

        if (GameManager.instance != null)
            GameManager.instance.AddScore(scoreOnDeath);

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}