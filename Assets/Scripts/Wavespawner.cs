using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Oleadas con flujo continuo: los enemigos van saliendo a goteo desde puntos
// distintos mientras quede presupuesto en la oleada. La oleada termina cuando
// han salido todos Y todos han muerto.
public class WaveSpawner : MonoBehaviour
{
    [Header("Enemigos")]
    public GameObject enemyPrefab;

    [Tooltip("Enemigos totales en la primera oleada.")]
    public int enemiesPerWave = 8;

    [Tooltip("Cuantos enemigos mas trae cada oleada respecto a la anterior.")]
    public int enemiesAddedPerWave = 4;

    [Tooltip("Tope de enemigos vivos a la vez. Mientras se llega al tope, el goteo se pausa.")]
    public int maxAlive = 8;

    [Header("Ritmo de aparicion")]
    [Tooltip("Segundos entre un enemigo y el siguiente, en la primera oleada.")]
    public float spawnInterval = 1.2f;

    [Tooltip("Cuanto se acorta ese intervalo en cada oleada.")]
    public float intervalReductionPerWave = 0.1f;

    [Tooltip("Suelo del intervalo: por rapido que vaya, nunca baja de aqui.")]
    public float minSpawnInterval = 0.25f;

    public float delayBeforeFirstWave = 2f;
    public float delayBetweenWaves = 4f;

    [Tooltip("Cartel en pantalla al empezar y al terminar cada oleada.")]
    public bool showBanner = true;

    [Header("Puntos de aparicion")]
    [Tooltip("Empties colocados a mano. Opcional: se suman a los del anillo.")]
    public Transform[] spawnPoints;

    [Tooltip("Puntos generados automaticamente en circulo alrededor de este objeto. 0 = ninguno.")]
    public int ringPointCount = 8;
    public float ringRadius = 13f;

    [Tooltip("Dispersion alrededor del punto elegido, para que no salgan siempre en el mismo pixel.")]
    public float spawnSpread = 2.5f;

    [Tooltip("Separacion minima que se intenta mantener con los enemigos ya vivos.")]
    public float minEnemySeparation = 2.5f;

    [Tooltip("Ningun enemigo aparece mas cerca del jugador que esto. Evita que te salgan en la cara.")]
    public float minDistanceFromPlayer = 10f;

    [Header("Limites del mapa")]
    public bool clampToMap = true;
    public Vector2 mapCenter = Vector2.zero;
    [Tooltip("Mitad del lado del suelo. Un Plane con escala 3 mide 30, asi que 14 deja margen.")]
    public float mapHalfSize = 14f;

    [Header("Diagnostico")]
    public int currentWave = 0;
    public int spawnedThisWave = 0;
    public int totalThisWave = 0;

    public int EnemiesAlive => alive.Count;
    public int RemainingToSpawn => Mathf.Max(0, totalThisWave - spawnedThisWave);

    private readonly List<GameObject> alive = new List<GameObject>();
    private readonly List<Vector3> pool = new List<Vector3>();
    private readonly List<int> bag = new List<int>();
    private Transform player;

    private void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[WaveSpawner] Falta asignar el Enemy Prefab.", this);
            enabled = false;
            return;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        BuildPool();

        if (pool.Count == 0)
        {
            Debug.LogError("[WaveSpawner] No hay ningun punto de aparicion. " +
                           "Pon Ring Point Count a 8 o arrastra empties a Spawn Points.", this);
            enabled = false;
            return;
        }

        StartCoroutine(RunWaves());
    }

    // Junta los puntos manuales con los del anillo generado.
    private void BuildPool()
    {
        pool.Clear();

        if (spawnPoints != null)
            foreach (Transform t in spawnPoints)
                if (t != null) pool.Add(t.position);

        for (int i = 0; i < ringPointCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ringPointCount;
            pool.Add(transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius);
        }
    }

    private IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(delayBeforeFirstWave);

        while (true)
        {
            currentWave++;
            yield return StartCoroutine(RunWave(currentWave));

            Debug.Log($"[WaveSpawner] Oleada {currentWave} limpiada.");

            if (showBanner)
                WaveBanner.Show($"OLEADA {currentWave} SUPERADA", "Preparate...");

            yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    private IEnumerator RunWave(int wave)
    {
        totalThisWave = enemiesPerWave + enemiesAddedPerWave * (wave - 1);
        spawnedThisWave = 0;

        float interval = Mathf.Max(minSpawnInterval,
                                   spawnInterval - intervalReductionPerWave * (wave - 1));

        Debug.Log($"[WaveSpawner] Oleada {wave}: {totalThisWave} enemigos, uno cada {interval:0.00}s.");

        if (showBanner)
            WaveBanner.Show($"OLEADA {wave}", $"{totalThisWave} enemigos en camino");

        // Fase 1: goteo continuo hasta agotar el presupuesto de la oleada.
        while (spawnedThisWave < totalThisWave)
        {
            Prune();

            // Con el cupo lleno se espera: el flujo lo marca lo rapido que mate el jugador.
            if (alive.Count >= maxAlive)
            {
                yield return null;
                continue;
            }

            SpawnOne();
            spawnedThisWave++;

            yield return new WaitForSeconds(interval);
        }

        // Fase 2: la oleada no acaba hasta que cae el ultimo.
        while (true)
        {
            Prune();
            if (alive.Count == 0) yield break;
            yield return null;
        }
    }

    private void Prune() => alive.RemoveAll(e => e == null);

    private void SpawnOne()
    {
        Vector3 position = PickPosition();
        alive.Add(Instantiate(enemyPrefab, position, Quaternion.identity));
    }

    // Elige un punto de la bolsa barajada y lo dispersa un poco.
    private Vector3 PickPosition()
    {
        Vector3 chosen = transform.position;
        float bestPlayerDistance = -1f;
        bool found = false;

        // Se recorre la bolsa buscando un punto que no este encima del jugador.
        for (int attempt = 0; attempt < pool.Count; attempt++)
        {
            if (bag.Count == 0) RefillBag();

            int index = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            Vector3 candidate = pool[index];

            float d = player == null ? float.MaxValue : Flat(candidate - player.position);

            if (d >= minDistanceFromPlayer)
            {
                chosen = candidate;
                found = true;
                break;
            }

            // Se guarda el mas lejano por si ninguno cumple.
            if (d > bestPlayerDistance)
            {
                bestPlayerDistance = d;
                chosen = candidate;
            }
        }

        if (!found && bestPlayerDistance < 0f) chosen = transform.position;

        return Scatter(chosen);
    }

    private void RefillBag()
    {
        bag.Clear();
        for (int i = 0; i < pool.Count; i++) bag.Add(i);

        // Barajado Fisher-Yates: se usan todos los puntos antes de repetir ninguno.
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }
    }

    // Busca un hueco cerca del punto que no pise a un enemigo ya vivo.
    private Vector3 Scatter(Vector3 basePosition)
    {
        Vector3 best = Clamp(basePosition);
        float bestDistance = NearestEnemyDistance(best);

        if (bestDistance >= minEnemySeparation) return best;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnSpread;
            Vector3 candidate = Clamp(basePosition + new Vector3(offset.x, 0f, offset.y));

            float d = NearestEnemyDistance(candidate);
            if (d >= minEnemySeparation) return candidate;

            if (d > bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }
        return best;
    }

    private float NearestEnemyDistance(Vector3 p)
    {
        float min = float.MaxValue;
        foreach (GameObject e in alive)
        {
            if (e == null) continue;
            min = Mathf.Min(min, Flat(p - e.transform.position));
        }
        return min;
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    private Vector3 Clamp(Vector3 p)
    {
        if (!clampToMap) return p;
        p.x = Mathf.Clamp(p.x, mapCenter.x - mapHalfSize, mapCenter.x + mapHalfSize);
        p.z = Mathf.Clamp(p.z, mapCenter.y - mapHalfSize, mapCenter.y + mapHalfSize);
        return p;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < ringPointCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ringPointCount;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
            Gizmos.DrawWireSphere(p, 0.6f);
        }

        if (spawnPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (Transform t in spawnPoints)
                if (t != null) Gizmos.DrawWireSphere(t.position, 0.8f);
        }

        if (clampToMap)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(mapCenter.x, transform.position.y, mapCenter.y),
                                new Vector3(mapHalfSize * 2f, 0.1f, mapHalfSize * 2f));
        }
    }
}