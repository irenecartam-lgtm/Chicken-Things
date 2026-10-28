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

    [Header("Separacion entre enemigos")]
    [Tooltip("Radio alrededor del punto de aparicion donde pueden salir los enemigos que comparten punto.")]
    public float spawnSpread = 5f;
    [Tooltip("Distancia minima que se intenta mantener entre dos enemigos de la misma oleada.")]
    public float minEnemySeparation = 3.5f;
    [Tooltip("Segundos entre un enemigo y el siguiente (0 = todos a la vez).")]
    public float spawnInterval = 0.4f;

    [Header("Limites del mapa")]
    [Tooltip("Evita que los enemigos salgan fuera del suelo.")]
    public bool clampToMap = true;
    public Vector2 mapCenter = Vector2.zero;
    [Tooltip("Mitad del lado del suelo. Un Plane con escala 3 mide 30, asi que 14 deja un margen.")]
    public float mapHalfSize = 14f;

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
        // waitingForNextWave sigue en true mientras salen los enemigos, asi no empieza otra oleada.
        waitingForNextWave = true;
        yield return new WaitForSeconds(delay);
        yield return StartCoroutine(SpawnWave());
        waitingForNextWave = false;
    }

    private IEnumerator SpawnWave()
    {
        currentWave++;
        List<Vector3> positions = PickPositions(enemiesPerWave);

        foreach (Vector3 pos in positions)
        {
            alive.Add(Instantiate(enemyPrefab, pos, Quaternion.identity));
            if (spawnInterval > 0f) yield return new WaitForSeconds(spawnInterval);
        }

        Debug.Log($"[WaveSpawner] Oleada {currentWave}: {positions.Count} enemigos.");
    }

    // Elige N posiciones: reparte los puntos de aparicion y separa a los que comparten punto.
    private List<Vector3> PickPositions(int count)
    {
        List<Vector3> result = new List<Vector3>();
        List<Transform> pool = new List<Transform>();
        bool usePoints = spawnPoints != null && spawnPoints.Length > 0;
        float angleOffset = Random.Range(0f, 360f);

        for (int i = 0; i < count; i++)
        {
            Vector3 basePos;

            if (usePoints)
            {
                if (pool.Count == 0) pool.AddRange(spawnPoints);   // menos puntos que enemigos: se reutilizan
                int idx = Random.Range(0, pool.Count);
                basePos = pool[idx].position;
                pool.RemoveAt(idx);
            }
            else
            {
                float angle = (angleOffset + i * 360f / count) * Mathf.Deg2Rad;
                basePos = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * fallbackRadius;
            }

            result.Add(FindSpot(basePos, result));
        }
        return result;
    }

    // Busca un hueco cerca de basePos que quede lejos de los enemigos ya colocados.
    private Vector3 FindSpot(Vector3 basePos, List<Vector3> placed)
    {
        Vector3 start = Clamp(basePos);
        if (MinDistance(start, placed) >= minEnemySeparation) return start;

        Vector3 best = start;
        float bestDistance = MinDistance(start, placed);

        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnSpread;
            Vector3 candidate = Clamp(basePos + new Vector3(offset.x, 0f, offset.y));

            float d = MinDistance(candidate, placed);
            if (d >= minEnemySeparation) return candidate;

            if (d > bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }
        return best;   // no habia hueco ideal: se usa el mas separado que se encontro
    }

    private static float MinDistance(Vector3 p, List<Vector3> others)
    {
        float min = float.MaxValue;
        foreach (Vector3 o in others)
        {
            Vector3 diff = p - o;
            diff.y = 0f;
            min = Mathf.Min(min, diff.magnitude);
        }
        return min;
    }

    private Vector3 Clamp(Vector3 p)
    {
        if (!clampToMap) return p;
        p.x = Mathf.Clamp(p.x, mapCenter.x - mapHalfSize, mapCenter.x + mapHalfSize);
        p.z = Mathf.Clamp(p.z, mapCenter.y - mapHalfSize, mapCenter.y + mapHalfSize);
        return p;
    }
}