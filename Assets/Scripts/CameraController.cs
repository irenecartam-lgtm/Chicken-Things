using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Camara con raton: primera persona <-> tercera persona (tecla V por defecto).
// Va en la Main Camera, SUSTITUYENDO a FollowCamera.
// Se ejecuta antes que el Player para que el giro del nugget use el yaw de este mismo frame.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public enum Mode { FirstPerson, ThirdPerson }

    [Header("Objetivo")]
    public Transform target;
    [Tooltip("Renderers que se ocultan en primera persona (siguen proyectando sombra). " +
             "Si lo dejas vacio se ocultan TODOS los del target, pistola incluida.")]
    public Renderer[] hideInFirstPerson;

    [Header("Modo")]
    public Mode mode = Mode.ThirdPerson;
    public Key toggleKey = Key.V;
    [Tooltip("Velocidad de la transicion entre modos. 0 = cambio instantaneo.")]
    public float transitionSpeed = 6f;

    [Header("Primera persona")]
    public float eyeHeight = 0.6f;
    public float fpFieldOfView = 75f;

    [Header("Tercera persona")]
    public float distance = 4f;
    public float pivotHeight = 1.2f;
    [Tooltip("Desplazamiento lateral: camara al hombro. 0 = centrada detras.")]
    public float shoulderOffset = 0.6f;
    public float tpFieldOfView = 60f;
    [Tooltip("Capas contra las que choca la camara (suelo y paredes). Evita que atraviese muros.")]
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.2f;

    [Header("Raton")]
    public float sensitivity = 0.12f;
    public bool invertY = false;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Mirilla")]
    public bool showCrosshair = true;
    public float crosshairSize = 4f;

    public float Yaw => yaw;
    public bool IsFirstPerson => mode == Mode.FirstPerson;

    private Camera cam;
    private float yaw;
    private float pitch;
    private float blend;                 // 0 = primera persona, 1 = tercera
    private bool bodyHidden;
    private ShadowCastingMode[] originalShadowModes;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
            else Debug.LogWarning("[CameraController] Sin target y sin objeto con tag Player.");
        }

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            if (hideInFirstPerson == null || hideInFirstPerson.Length == 0)
                hideInFirstPerson = target.GetComponentsInChildren<Renderer>();
        }

        originalShadowModes = new ShadowCastingMode[hideInFirstPerson != null ? hideInFirstPerson.Length : 0];
        for (int i = 0; i < originalShadowModes.Length; i++)
            if (hideInFirstPerson[i] != null) originalShadowModes[i] = hideInFirstPerson[i].shadowCastingMode;

        blend = mode == Mode.ThirdPerson ? 1f : 0f;
    }

    private void OnEnable()  { SetCursorLocked(true); }
    private void OnDisable() { SetCursorLocked(false); SetBodyHidden(false); }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb != null)
        {
            if (kb[toggleKey].wasPressedThisFrame)
                mode = mode == Mode.FirstPerson ? Mode.ThirdPerson : Mode.FirstPerson;

            if (kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        }

        if (mouse == null) return;

        // Click para volver a capturar el raton despues de pulsar Escape.
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);
            return;
        }

        // El delta del Input System ya es "lo movido este frame": no se multiplica por deltaTime.
        Vector2 delta = mouse.delta.ReadValue() * sensitivity;
        yaw += delta.x;
        pitch += invertY ? delta.y : -delta.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float targetBlend = mode == Mode.ThirdPerson ? 1f : 0f;
        blend = transitionSpeed <= 0f
            ? targetBlend
            : Mathf.MoveTowards(blend, targetBlend, transitionSpeed * Time.deltaTime);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 fpPosition = target.position + Vector3.up * eyeHeight;

        Vector3 pivot = target.position + Vector3.up * pivotHeight + flatRotation * Vector3.right * shoulderOffset;
        Vector3 tpPosition = ResolveCollision(pivot, pivot - rotation * Vector3.forward * distance);

        float t = Mathf.SmoothStep(0f, 1f, blend);
        transform.SetPositionAndRotation(Vector3.Lerp(fpPosition, tpPosition, t), rotation);
        cam.fieldOfView = Mathf.Lerp(fpFieldOfView, tpFieldOfView, t);

        // Se oculta el cuerpo solo cuando la camara ya esta practicamente dentro.
        SetBodyHidden(blend < 0.15f);
    }

    // Punto del mundo al que apunta el centro de la pantalla (la mirilla).
    // PlayerShooting lo usa para orientar la bala, asi en tercera persona tambien
    // sale hacia donde mira la camara y no hacia donde mira la pistola.
    public Vector3 GetAimPoint(float maxDistance = 200f)
    {
        Ray ray = new Ray(transform.position, transform.forward);
        Vector3 point = ray.GetPoint(maxDistance);
        float closest = maxDistance;

        foreach (RaycastHit hit in Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (ShouldIgnore(hit.collider)) continue;
            if (hit.distance < closest)
            {
                closest = hit.distance;
                point = hit.point;
            }
        }
        return point;
    }

    // ---------- Internos ----------

    private Vector3 ResolveCollision(Vector3 pivot, Vector3 desired)
    {
        Vector3 dir = desired - pivot;
        float dist = dir.magnitude;
        if (dist < 0.001f) return desired;
        dir /= dist;

        float closest = dist;
        foreach (RaycastHit hit in Physics.SphereCastAll(pivot, collisionRadius, dir, dist,
                                                         collisionMask, QueryTriggerInteraction.Ignore))
        {
            if (ShouldIgnore(hit.collider)) continue;
            if (hit.distance > 0f && hit.distance < closest) closest = hit.distance;
        }
        return pivot + dir * closest;
    }

    // Ni el propio jugador ni las balas en vuelo cuentan como obstaculo o blanco:
    // si no, la mirilla se "pega" a la bala anterior al disparar en automatico.
    private bool ShouldIgnore(Collider c)
    {
        if (target != null && c.transform.IsChildOf(target)) return true;
        return c.GetComponentInParent<Bullet>() != null;
    }

    private void SetBodyHidden(bool hide)
    {
        if (hide == bodyHidden || hideInFirstPerson == null) return;
        bodyHidden = hide;

        for (int i = 0; i < hideInFirstPerson.Length; i++)
        {
            Renderer r = hideInFirstPerson[i];
            if (r == null) continue;
            r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : originalShadowModes[i];
        }
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnGUI()
    {
        if (!showCrosshair || Cursor.lockState != CursorLockMode.Locked) return;
        float s = crosshairSize;
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - s * 0.5f, Screen.height * 0.5f - s * 0.5f, s, s),
                        Texture2D.whiteTexture);
    }
}
