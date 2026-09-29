using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Estado")]
    public int score = 0;
    public int life = 3;

    [Header("UI (opcional)")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI lifeText;

    private bool isGameOver = false;

    private void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(this); return; }
    }

    private void Start()
    {
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

        life = Mathf.Max(0, life - amount);
        UpdateUI();

        if (life <= 0) GameOver();
    }

    void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "Puntos: " + score;
        if (lifeText != null) lifeText.text = "Vidas: " + life;
    }

    void GameOver()
    {
        isGameOver = true;
        Debug.Log("GAME OVER");
    }
}