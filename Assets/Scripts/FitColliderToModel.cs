using UnityEngine;

// Ajusta el collider (Box o Capsule) al tamaño real del modelo 3D,
// para que el personaje se apoye en el suelo con sus pies y no "flote".
// Va en el Player y en el prefab del enemigo.
public class FitColliderToModel : MonoBehaviour
{
    [Tooltip("Hijos que NO cuentan para el tamaño (por ejemplo el arma).")]
    public string[] ignoreNames = { "Gun", "Muzzle" };

    private void Awake()
    {
        Fit();
    }

    [ContextMenu("Ajustar ahora")]
    public void Fit()
    {
        // Se mide con el objeto sin girar para que el tamaño no dependa de hacia donde mire.
        Quaternion savedRotation = transform.rotation;
        transform.rotation = Quaternion.identity;

        bool found = false;
        Bounds bounds = new Bounds();

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r.gameObject == gameObject) continue;   // ignora el cubo/capsula viejos del propio objeto
            if (IsIgnored(r.transform)) continue;

            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!found)
        {
            transform.rotation = savedRotation;
            Debug.LogWarning("[FitColliderToModel] No encuentro el modelo 3D dentro de " + name);
            return;
        }

        Vector3 scale = transform.lossyScale;
        Vector3 center = transform.InverseTransformPoint(bounds.center);
        Vector3 size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

        BoxCollider box = GetComponent<BoxCollider>();
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();

        if (box != null)
        {
            box.center = center;
            box.size = size;
        }
        else if (capsule != null)
        {
            capsule.direction = 1;   // vertical (eje Y)
            capsule.center = center;
            capsule.radius = (size.x + size.z) * 0.25f;
            capsule.height = Mathf.Max(size.y, capsule.radius * 2f);
        }
        else
        {
            Debug.LogWarning("[FitColliderToModel] " + name + " no tiene BoxCollider ni CapsuleCollider.");
        }

        transform.rotation = savedRotation;
    }

    private bool IsIgnored(Transform t)
    {
        while (t != null && t != transform)
        {
            foreach (string n in ignoreNames)
                if (t.name == n) return true;
            t = t.parent;
        }
        return false;
    }
}
