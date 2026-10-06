using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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

    [Header("Guardado")]
    public Key saveKey = Key.K;
    public Key loadKey = Key.L;

    [Header("Pausa")]
    public Key pauseKey = Key.Escape;   // tambien funciona la tecla P

    [Header("UI (opcional)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI lifeText;

    public bool IsGameOver => isGameOver;
    public bool IsPaused => isPaused;

    private bool isGameOver = false;
    private bool isPaused = false;
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

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb[pauseKey].wasPressedThisFrame || kb.pKey.wasPressedThisFrame) TogglePause();

        if (kb[saveKey].wasPressedThisFrame) SaveGame();
        if (kb[loadKey].wasPressedThisFrame) LoadGame();

        // R reinicia la partida desde la pausa o desde el Game Over.
        if ((isPaused || isGameOver) && kb.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // --------------------------------------------------------------- Pausa
    public void TogglePause()
    {
        if (isGameOver) return;
        SetPaused(!isPaused);
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        // Con el tiempo a 0 el input seguiria activo (se podria disparar), asi que se bloquea.
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) pc.enabled = !paused;
        PlayerShooting ps = FindFirstObjectByType<PlayerShooting>();
        if (ps != null) ps.enabled = !paused;

        if (hud != null)
        {
            if (paused) hud.ShowPause();
            else hud.HidePause();
        }
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

    // ------------------------------------------------------------ Guardado
    public void SaveGame()
    {
        if (isGameOver)
        {
            if (hud != null) hud.ShowMessage("No puedes guardar estando muerto");
            return;
        }

        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc == null)
        {
            Debug.LogWarning("[GameManager] No se encontro al jugador, no se puede guardar.");
            return;
        }

        SaveData data = new SaveData
        {
            score = score,
            life = life,
            playerPosition = pc.transform.position
        };
        SaveSystem.Save(data);

        if (hud != null) hud.ShowMessage("Partida guardada");
    }

    public void LoadGame()
    {
        SaveData data = SaveSystem.Load();
        if (data == null)
        {
            if (hud != null) hud.ShowMessage("No hay partida guardada");
            return;
        }

        score = data.score;
        life = Mathf.Clamp(data.life, 1, maxLife);

        // Recoloca al jugador y le quita cualquier velocidad que llevase.
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        if (pc != null)
        {
            pc.transform.position = data.playerPosition;
            Rigidbody rb = pc.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = data.playerPosition;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            pc.enabled = true;
        }
        PlayerShooting ps = FindFirstObjectByType<PlayerShooting>();
        if (ps != null) ps.enabled = true;

        // Si estabas en Game Over o en pausa, la partida vuelve a estar en marcha.
        isGameOver = false;
        isPaused = false;
        Time.timeScale = 1f;
        invulnerableUntil = Time.time + 1f;

        UpdateUI();
        if (hud != null)
        {
            hud.HideGameOver();
            hud.HidePause();
            hud.ShowMessage("Partida cargada");
        }
    }
}