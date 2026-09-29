using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Lanza oleadas de N enemigos desde puntos distintos.
// La siguiente oleada NO empieza hasta que todos los enemigos de la actual han muerto.
public class WaveSpawner : MonoBehaviour
{
    [Header("Enemigos")]
    public GameObject enemyPrefab;
    public int enemiesPerWave = 5;

    [Header("Puntos de aparicion")]
    [Tooltip("Arrastra aqui empties repartidos por el mapa. Si lo dejas vacio se usa un anillo alrededor de este objeto.")]
    public Transform[] spawnPoints;
    [Tooltip("Solo se usa si no hay spawnPoints.")]
    public float fallbackRadius = 10f;

    [Header("Ritmo")]
    public float delayBeforeFirstWave = 1f;
    public float delayBetweenWaves = 2f;

    [Header("Diagnostico")]
    public int currentWave = 0;

    private readonly List<GameObject> alive = new List<GameObject>();
    private bool waitingForNextWave;

    private void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[WaveSpawner] Falta asignar el Enemy Prefab.");
            enabled = false;
            return;
        }
        StartCoroutine(SpawnAfter(delayBeforeFirstWave));
    }

    private void Update()
    {
        if (waitingForNextWave) return;

        // Los enemigos muertos se destruyen -> pasan a ser null.
        alive.RemoveAll(e => e == null);

        if (alive.Count == 0)
            StartCoroutine(SpawnAfter(delayBeforeFirstWave > 0 && currentWave == 0 ? 0f : delayBetweenWaves));
    }

    private IEnumerator SpawnAfter(float delay)
    {
        waitingForNextWave = true;
        yield return new WaitForSeconds(delay);
        SpawnWave();
        waitingForNextWave = false;
    }

    private void SpawnWave()
    {
        currentWave++;
        List<Vector3> positions = PickPositions(enemiesPerWave);

        foreach (Vector3 pos in positions)
            alive.Add(Instantiate(enemyPrefab, pos, Quaternion.identity));

        Debug.Log($"[WaveSpawner] Oleada {currentWave}: {positions.Count} enemigos.");
    }

    // Elige N posiciones distintas (sin repetir punto mientras haya suficientes).
    private List<Vector3> PickPositions(int count)
    {
        List<Vector3> result = new List<Vector3>();

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            List<Transform> pool = new List<Transform>(spawnPoints);
            for (int i = 0; i < count; i++)
            {
                if (pool.Count == 0) pool.AddRange(spawnPoints);   // menos puntos que enemigos: se reutilizan
                int idx = Random.Range(0, pool.Count);
                Vector3 p = pool[idx].position;
                if (result.Contains(p)) p += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                result.Add(p);
                pool.RemoveAt(idx);
            }
        }
        else
        {
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (offset + i * 360f / count) * Mathf.Deg2Rad;
                result.Add(transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * fallbackRadius);
            }
        }
        return result;
    }
}
