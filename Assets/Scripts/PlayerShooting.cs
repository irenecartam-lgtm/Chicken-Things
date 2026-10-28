using UnityEngine;
using UnityEngine.InputSystem;

// Va en el objeto Player, junto a PlayerController.
public class PlayerShooting : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Empty en la boquilla del bote. Hijo de Gun. Su eje Z+ es la direccion de disparo.")]
    public Transform muzzle;

    [Tooltip("Prefab de la bolita (Sphere.prefab).")]
    public GameObject bulletPrefab;

    [Tooltip("Si se asigna, la bala va hacia la mirilla en vez de recta desde el canon.")]
    public CameraController cameraRig;

    [Tooltip("El objeto 'Gun'. Se desplaza y rota con el retroceso.")]
    public Transform gunPivot;

    [Header("Cadencia")]
    [Tooltip("Disparos por segundo.")]
    public float fireRate = 5f;

    [Tooltip("ON: mantener el click dispara en rafaga. OFF: un disparo por click.")]
    public bool automatic = true;

    [Header("Precision")]
    [Tooltip("Grados de desviacion aleatoria. 0 = punteria perfecta.")]
    public float spread = 1.5f;

    [Header("Retroceso")]
    public float kickBack = 0.1f;
    public float kickRotation = 12f;
    [Tooltip("Velocidad a la que el arma vuelve a su sitio.")]
    public float kickRecovery = 14f;

    [Header("Feedback")]
    public ParticleSystem muzzleFlash;
    public AudioSource shootSound;

    private float nextFireTime;
    private Vector3 gunRestPosition;
    private Quaternion gunRestRotation;

    private void Awake()
    {
        if (muzzle == null)
            Debug.LogError("[PlayerShooting] Falta asignar el Muzzle.", this);

        if (bulletPrefab == null)
            Debug.LogError("[PlayerShooting] Falta asignar el Bullet Prefab.", this);

        if (gunPivot != null)
        {
            gunRestPosition = gunPivot.localPosition;
            gunRestRotation = gunPivot.localRotation;
        }
    }

    private void Update()
    {
        HandleInput();
        RecoverFromRecoil();
    }

    private void HandleInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // Con el raton liberado (Escape), el click recaptura el cursor; no dispara.
        if (cameraRig != null && Cursor.lockState != CursorLockMode.Locked) return;

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

        Quaternion rotation = muzzle.rotation;

        // En tercera persona el canon y la mirilla no coinciden:
        // manda la mirilla, que es lo que el jugador esta viendo.
        if (cameraRig != null)
        {
            Vector3 toAim = cameraRig.GetAimPoint() - muzzle.position;
            if (toAim.sqrMagnitude > 0.01f) rotation = Quaternion.LookRotation(toAim);
        }

        rotation *= Quaternion.Euler(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0f);

        GameObject bullet = Instantiate(bulletPrefab, muzzle.position, rotation);

        // El tirador se ignora: si no, la bala muere contra el collider del propio nugget.
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null) b.Init(transform);

        ApplyRecoil();

        if (muzzleFlash != null) muzzleFlash.Play();

        if (shootSound != null)
        {
            shootSound.pitch = Random.Range(0.92f, 1.08f);
            shootSound.Play();
        }
    }

    private void ApplyRecoil()
    {
        if (gunPivot == null) return;

        gunPivot.localPosition -= Vector3.forward * kickBack;
        gunPivot.localRotation *= Quaternion.Euler(-kickRotation, 0f, 0f);
    }

    private void RecoverFromRecoil()
    {
        if (gunPivot == null) return;

        gunPivot.localPosition = Vector3.Lerp(
            gunPivot.localPosition, gunRestPosition, kickRecovery * Time.deltaTime);

        gunPivot.localRotation = Quaternion.Slerp(
            gunPivot.localRotation, gunRestRotation, kickRecovery * Time.deltaTime);
    }
}
