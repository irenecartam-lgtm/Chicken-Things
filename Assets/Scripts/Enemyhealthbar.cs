using UnityEngine;

// Barra de vida flotante sobre el enemigo. Se crea sola por codigo (no necesita assets).
// Se decrementa con la vida y desaparece junto con el enemigo.
[RequireComponent(typeof(EnemyHealth))]
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Aspecto")]
    public float heightAboveEnemy = 1.6f;
    public Vector2 size = new Vector2(1.2f, 0.15f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
    public Color fullColor = new Color(0.2f, 0.9f, 0.2f);
    public Color emptyColor = new Color(0.9f, 0.1f, 0.1f);

    private EnemyHealth health;
    private Transform barRoot;
    private Transform fill;
    private SpriteRenderer fillRenderer;
    private Camera cam;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        cam = Camera.main;
        BuildBar();
    }

    private void OnEnable()  { health.OnHealthChanged += Refresh; }
    private void OnDisable() { health.OnHealthChanged -= Refresh; }

    private void Start()
    {
        Refresh(health.CurrentHealth, health.maxHealth);
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;
        if (cam == null) cam = Camera.main;

        // Sigue al enemigo y siempre mira a la camara (no rota ni se deforma con el enemigo).
        barRoot.position = transform.position + Vector3.up * heightAboveEnemy;
        if (cam != null) barRoot.rotation = cam.transform.rotation;
    }

    private void OnDestroy()
    {
        if (barRoot != null) Destroy(barRoot.gameObject);
    }

    private void Refresh(int current, int max)
    {
        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        fill.localScale = new Vector3(ratio * size.x, size.y, 1f);
        fillRenderer.color = Color.Lerp(emptyColor, fullColor, ratio);
    }

    private void BuildBar()
    {
        // Sprite blanco de 1x1 unidades; el de relleno tiene el pivote a la izquierda.
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        Sprite centered = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        Sprite leftPivot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);

        barRoot = new GameObject(name + "_HealthBar").transform;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(barRoot, false);
        bg.transform.localScale = new Vector3(size.x + 0.06f, size.y + 0.06f, 1f);
        SpriteRenderer bgRenderer = bg.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = centered;
        bgRenderer.color = backgroundColor;
        bgRenderer.sortingOrder = 100;

        GameObject fg = new GameObject("Fill");
        fg.transform.SetParent(barRoot, false);
        fg.transform.localPosition = new Vector3(-size.x * 0.5f, 0f, -0.001f);
        fill = fg.transform;
        fillRenderer = fg.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = leftPivot;
        fillRenderer.sortingOrder = 101;
    }
}