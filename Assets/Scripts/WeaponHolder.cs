using UnityEngine;

// Coloca el arma cada frame: en primera persona delante de la camara,
// en tercera al costado del nugget. Interpola entre las dos con el mismo
// blend que usa CameraController, y aplica el retroceso.
// Va en el objeto Gun. Se ejecuta DESPUES de CameraController.
[DefaultExecutionOrder(100)]
public class WeaponHolder : MonoBehaviour
{
    [Header("Referencias")]
    public CameraController cameraRig;
    [Tooltip("El Player. Si se deja vacio usa el padre de este objeto.")]
    public Transform player;

    [Header("Postura en primera persona (relativa a la camara)")]
    [Tooltip("x = derecha, y = abajo si es negativo, z = hacia delante.")]
    public Vector3 firstPersonOffset = new Vector3(0.35f, -0.28f, 0.65f);

    [Tooltip("Tamaño del arma solo en primera persona. 1 = igual que en tercera.")]
    [Range(0.2f, 1.5f)]
    public float firstPersonScale = 0.7f;

    [Header("Postura en tercera persona (relativa al jugador)")]
    public Vector3 thirdPersonOffset = new Vector3(0.55f, 1f, 0.25f);

    [Header("Retroceso")]
    public float kickBack = 0.12f;
    public float kickRotation = 14f;
    [Tooltip("Unidades de retroceso que se recuperan por segundo.")]
    public float kickRecovery = 8f;

    private float recoil;

    private void Awake()
    {
        if (cameraRig == null) cameraRig = FindFirstObjectByType<CameraController>();
        if (player == null) player = transform.parent;

        if (cameraRig == null)
            Debug.LogError("[WeaponHolder] No encuentro el CameraController.", this);
        if (player == null)
            Debug.LogError("[WeaponHolder] Sin Player asignado y sin padre.", this);
    }

    // La llama PlayerShooting en cada disparo.
    public void AddRecoil() => recoil = Mathf.Min(recoil + 1f, 1.5f);

    private void LateUpdate()
    {
        if (cameraRig == null || player == null) return;

        recoil = Mathf.MoveTowards(recoil, 0f, kickRecovery * Time.deltaTime);

        // El arma apunta siempre a donde mira la camara, cabeceo incluido.
        Quaternion aim = Quaternion.Euler(cameraRig.Pitch, cameraRig.Yaw, 0f);
        Quaternion flat = Quaternion.Euler(0f, cameraRig.Yaw, 0f);

        float t = Mathf.SmoothStep(0f, 1f, cameraRig.ViewBlend);

        // El offset de primera persona se escala con el arma: asi al encogerla
        // no se queda flotando lejos de la mano.
        Vector3 eye = player.position + Vector3.up * cameraRig.eyeHeight;
        Vector3 fpPosition = eye + aim * (firstPersonOffset * firstPersonScale);
        Vector3 tpPosition = player.position + flat * thirdPersonOffset;

        Vector3 position = Vector3.Lerp(fpPosition, tpPosition, t);

        Quaternion rotation = aim * Quaternion.Euler(-kickRotation * recoil, 0f, 0f);
        position -= rotation * Vector3.forward * (kickBack * recoil);

        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = Vector3.one * Mathf.Lerp(firstPersonScale, 1f, t);
    }
}