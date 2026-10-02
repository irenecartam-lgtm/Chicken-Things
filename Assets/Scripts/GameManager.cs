using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Estado")]
    public int score = 0;
    [Tooltip("Vida actual. Al empezar se rellena con Max Life.")]
    public int life = 10;
    public int maxLife = 10;
    [Tooltip("Segundos de inmunidad tras recibir un golpe (evita que varios enemigos te maten de golpe).")]
    public float invulnerableTime = 0.5f;

    [Header("HUD de vida (esquina inferior izquierda)")]
    [Tooltip("Foto del personaje que sale dentro del circulo. Si lo dejas vacio sale un circulo naranja.")]
    public Sprite playerPortrait;

    [Header("UI (opcional)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI lifeText;

    public bool IsGameOver => isGameOver;

    private bool isGameOver = false;
    private float invulnerableUntil = 0f;
    private PlayerHealthHUD hud;

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(this); return; }

        Time.timeScale = 1f;
        life = maxLife;
    }

    private void Start()
    {
        // El HUD se crea solo: no hace falta Canvas ni objetos en la escena.
        hud = new GameObject("PlayerHealthHUD").AddComponent<PlayerHealthHUD>();
        hud.Build(playerPortrait, life, maxLife);
        UpdateUI();
    }

    public void AddScore(int amount)
    {
        if (isGameOver) return;
        score += amount;
        UpdateUI();
    }

    public void TakeDamage(int amount)
    {
        if (isGameOver) return;
        if (Time.time < invulnerableUntil) return;   // inmunidad corta tras un golpe

        invulnerableUntil = Time.time + invulnerableTime;
        life = Mathf.Max(0, life - amount);

        UpdateUI();
        if (hud != null) hud.SetLife(life, maxLife, true);

        if (life <= 0) GameOver();
    }

    void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "Puntos: " + score;
        if (lifeText != null) lifeText.text = "Vidas: " + life;
        if (hud != null) hud.SetLife(life, maxLife, false);
    }

    void GameOver()
    {
        isGameOver = true;
        Debug.Log("GAME OVER");

        // Bloquea movimiento y disparo, y congela el juego.
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;
        PlayerShooting ps = FindFirstObjectByType<PlayerShooting>();
        if (ps != null) ps.enabled = false;

        Time.timeScale = 0f;
        if (hud != null) hud.ShowGameOver();
    }
}