using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Empty situado en la punta del canon. DEBE ser hijo del Visual, no del Player.")]
    public Transform muzzle;
    public GameObject bulletPrefab;

    [Header("Cadencia")]
    [Tooltip("Disparos por segundo.")]
    public float fireRate = 5f;
    public bool automatic = true;

    private float nextFireTime;

    private void Awake()
    {
        if (muzzle == null) Debug.LogError("[PlayerShooting] Falta asignar el Muzzle.");
        if (bulletPrefab == null) Debug.LogError("[PlayerShooting] Falta asignar el Bullet Prefab.");
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        bool wantsToShoot = automatic
            ? mouse.leftButton.isPressed
            : mouse.leftButton.wasPressedThisFrame;

        if (wantsToShoot && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + (1f / Mathf.Max(0.01f, fireRate));
        }
    }

    private void Shoot()
    {
        if (muzzle == null || bulletPrefab == null) return;

        Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
    }
}
