using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Todos los sonidos del juego, SINTETIZADOS por codigo (no hace falta ningun archivo de audio).
//   - Musica de fondo en bucle (~17 s: bajo, bombo, caja, arpegio y una melodia).
//   - Efectos: disparo, impacto, muerte, explosion, cura, bolitas, oleadas, daño, salsas, menu...
// Se crea solo y sobrevive a los cambios de escena. Uso desde cualquier script:
//     GameAudio.Play(Sfx.Explosion);
//     GameAudio.Play(Sfx.Hit, transform.position, 0.7f);   // con posicion: baja de volumen con la distancia y suena a izquierda/derecha
// Menu: musica tranquila. Juego: musica rapida de combate. Tecla M: silenciar / activar la musica.
public enum Sfx
{
    Shoot, Hit, EnemyDie, Explosion, Heal, Pickup, WaveStart, WaveClear, Hurt, GameOver,
    MenuOpen, MenuTick, MenuDeny, SauceOn, SauceOff, Ignite, Splat, Pepper
}

public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    public static float sfxVolume = 0.8f;
    public static float musicVolume = 0.3f;
    public static Key muteMusicKey = Key.M;

    private const int SR = 32000;       // frecuencia de muestreo (mono)
    private const int VoiceCount = 14;  // efectos que pueden sonar a la vez

    private AudioSource music;
    private AudioSource[] voices;
    private int nextVoice;
    private bool musicMuted;
    private float musicFade;
    private AudioClip menuClip, battleClip, wantedClip;

    private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    private readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        Ensure();
    }

    private static void Ensure()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("GameAudio");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameAudio>();
    }

    // ------------------------------------------------------------------ API

    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
    {
        Ensure();
        Instance.PlayInternal(sfx, volume, pitch, 0f);
    }

    // Con posicion: el sonido baja con la distancia y se reparte entre altavoz izquierdo y derecho.
    public static void Play(Sfx sfx, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        Ensure();

        Camera cam = Camera.main;
        float pan = 0f;
        if (cam != null)
        {
            Vector3 local = cam.transform.InverseTransformPoint(position);
            float dist = local.magnitude;
            volume *= Mathf.Clamp(1f / (1f + dist * 0.06f), 0.25f, 1f);
            pan = Mathf.Clamp(local.x / Mathf.Max(1f, dist), -1f, 1f) * 0.7f;
        }

        Instance.PlayInternal(sfx, volume, pitch, pan);
    }

    // ------------------------------------------------------------- Ciclo de vida

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.volume = 0f;

        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            voices[i] = s;
        }
    }

    private void Start()
    {
        // Dos pistas: calmada para el menu y rapida y agresiva para jugar.
        menuClip = BuildMenuMusic();
        battleClip = BuildBattleMusic();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ChooseTrack(SceneManager.GetActiveScene());

        music.clip = wantedClip;
        music.Play();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ChooseTrack(scene);

    // La escena del menu (su nombre contiene "menu") lleva la musica tranquila; el resto, la de combate.
    private void ChooseTrack(Scene scene)
    {
        bool isMenu = scene.name.ToLowerInvariant().Contains("menu");
        wantedClip = isMenu ? menuClip : battleClip;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[muteMusicKey].wasPressedThisFrame) musicMuted = !musicMuted;

        // Entrada suave y cambio de volumen sin cortes (usa tiempo real: tambien suena con el juego en pausa).
        // Si hay que cambiar de pista: primero baja el volumen a 0 y entonces se cambia (fundido).
        bool wrongTrack = music.clip != wantedClip && wantedClip != null;
        float target = (musicMuted || wrongTrack) ? 0f : musicVolume;
        float speed = wrongTrack ? 1.5f : 0.4f;
        musicFade = Mathf.MoveTowards(musicFade, target, speed * Time.unscaledDeltaTime);

        if (wrongTrack && musicFade <= 0.001f)
        {
            music.clip = wantedClip;
            music.Play();
        }

        music.volume = musicFade;
    }

    // ------------------------------------------------------------ Reproduccion

    private void PlayInternal(Sfx sfx, float volume, float pitch, float pan)
    {
        // Evita apilar el mismo sonido muchas veces en el mismo instante (rafagas de bolitas, etc.).
        float minGap = sfx == Sfx.Pickup ? 0.05f : (sfx == Sfx.Hit || sfx == Sfx.Splat) ? 0.04f : 0.02f;
        if (lastPlayed.TryGetValue(sfx, out float last) && Time.unscaledTime - last < minGap) return;
        lastPlayed[sfx] = Time.unscaledTime;

        AudioClip clip = GetClip(sfx);
        if (clip == null) return;

        // Un poco de variacion de tono para que no suene siempre igual.
        bool varied = sfx == Sfx.Shoot || sfx == Sfx.Hit || sfx == Sfx.Pickup || sfx == Sfx.Splat || sfx == Sfx.EnemyDie;
        if (varied) pitch *= Random.Range(0.93f, 1.07f);

        AudioSource s = FreeVoice();
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume * sfxVolume);
        s.pitch = pitch;
        s.panStereo = pan;
        s.Play();
    }

    private AudioSource FreeVoice()
    {
        for (int i = 0; i < voices.Length; i++)
            if (!voices[i].isPlaying) return voices[i];

        // Todas ocupadas: se reutiliza la que lleva mas tiempo sonando.
        AudioSource s = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        return s;
    }

    // ------------------------------------------------------------- Sintesis base

    private static readonly System.Random rng = new System.Random(1234);
    private static bool wrap;   // true al generar la musica: las colas de las notas dan la vuelta al principio (bucle sin saltos)

    private static float[] Buf(float seconds)
    {
        return new float[Mathf.CeilToInt(seconds * SR)];
    }

    private static AudioClip ToClip(string name, float[] data, float peak = 0.9f)
    {
        float max = 0.0001f;
        for (int i = 0; i < data.Length; i++)
        {
            float a = Mathf.Abs(data[i]);
            if (a > max) max = a;
        }

        float g = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= g;

        AudioClip clip = AudioClip.Create(name, data.Length, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    // wave: 0 seno, 1 triangulo, 2 cuadrada suave, 3 sierra suave
    private static float Osc(float ph, int wave)
    {
        switch (wave)
        {
            case 1: return 4f * Mathf.Abs(ph - 0.5f) - 1f;
            case 2:
                {
                    float x = ph * 2f * Mathf.PI;
                    return 0.7f * (Mathf.Sin(x) + 0.33f * Mathf.Sin(3f * x) + 0.2f * Mathf.Sin(5f * x));
                }
            case 3: return (2f * ph - 1f) * 0.6f;
            default: return Mathf.Sin(ph * 2f * Mathf.PI);
        }
    }

    private static void Add(float[] b, int index, float value)
    {
        if (wrap) index = ((index % b.Length) + b.Length) % b.Length;
        else if (index < 0 || index >= b.Length) return;
        b[index] += value;
    }

    // Nota o barrido de tono: de f0 a f1 Hz, con ataque corto y caida exponencial (decay mayor = mas seco).
    private static void Tone(float[] b, float start, float dur, float f0, float f1, float amp, float decay,
                             int wave = 0, float attack = 0.004f)
    {
        int s = Mathf.RoundToInt(start * SR);
        int n = Mathf.Max(1, Mathf.RoundToInt(dur * SR));
        int att = Mathf.Max(1, Mathf.RoundToInt(attack * SR));
        int rel = Mathf.Max(1, Mathf.RoundToInt(0.01f * SR));
        float ph = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = Mathf.Lerp(f0, f1, t);
            ph += f / SR;
            ph -= Mathf.Floor(ph);

            float env = Mathf.Exp(-decay * i / SR);
            if (i < att) env *= i / (float)att;
            if (n - i < rel) env *= (n - i) / (float)rel;

            Add(b, s + i, Osc(ph, wave) * env * amp);
        }
    }

    // Ruido filtrado (filtro de paso bajo de un polo): cut0 -> cut1 Hz a lo largo del sonido.
    private static void Noise(float[] b, float start, float dur, float amp, float decay, float cut0, float cut1)
    {
        int s = Mathf.RoundToInt(start * SR);
        int n = Mathf.Max(1, Mathf.RoundToInt(dur * SR));
        int rel = Mathf.Max(1, Mathf.RoundToInt(0.01f * SR));
        float y = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float fc = Mathf.Lerp(cut0, cut1, t);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc / SR);
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            y += a * (x - y);

            float env = Mathf.Exp(-decay * i / SR);
            if (n - i < rel) env *= (n - i) / (float)rel;

            Add(b, s + i, y * env * amp);
        }
    }

    // ------------------------------------------------------------------- Efectos

    private AudioClip GetClip(Sfx sfx)
    {
        if (clips.TryGetValue(sfx, out AudioClip c)) return c;

        wrap = false;
        c = BuildSfx(sfx);
        clips[sfx] = c;
        return c;
    }

    private static AudioClip BuildSfx(Sfx sfx)
    {
        float[] b;

        switch (sfx)
        {
            case Sfx.Shoot:
                b = Buf(0.14f);
                Tone(b, 0f, 0.12f, 900f, 180f, 0.7f, 22f, 2);
                Noise(b, 0f, 0.05f, 0.5f, 40f, 9000f, 3000f);
                break;

            case Sfx.Hit:
                b = Buf(0.12f);
                Tone(b, 0f, 0.1f, 260f, 90f, 0.8f, 28f, 0);
                Noise(b, 0f, 0.04f, 0.6f, 60f, 6000f, 1500f);
                break;

            case Sfx.EnemyDie:
                b = Buf(0.4f);
                Tone(b, 0f, 0.35f, 420f, 55f, 0.8f, 9f, 1);
                Noise(b, 0f, 0.2f, 0.5f, 14f, 5000f, 600f);
                break;

            case Sfx.Explosion:
                b = Buf(1.3f);
                Noise(b, 0f, 1.25f, 1f, 3.2f, 5000f, 250f);
                Noise(b, 0f, 0.12f, 0.8f, 30f, 9000f, 9000f);
                Tone(b, 0f, 0.9f, 85f, 28f, 1f, 4f, 0);
                break;

            case Sfx.Heal:
                b = Buf(1f);
                float[] heal = { 659.25f, 830.6f, 987.77f, 1318.5f };
                for (int i = 0; i < heal.Length; i++)
                {
                    Tone(b, i * 0.07f, 0.6f, heal[i], heal[i] * 1.004f, 0.4f, 5f, 0);
                    Tone(b, i * 0.07f, 0.4f, heal[i] * 2f, heal[i] * 2f, 0.12f, 8f, 0);   // brillo
                }
                break;

            case Sfx.Pickup:
                b = Buf(0.2f);
                Tone(b, 0f, 0.07f, 880f, 880f, 0.5f, 12f, 1);
                Tone(b, 0.06f, 0.12f, 1320f, 1320f, 0.5f, 14f, 1);
                break;

            case Sfx.WaveStart:
                b = Buf(1.6f);
                Tone(b, 0f, 0.3f, 220f, 220f, 0.5f, 2f, 2);
                Tone(b, 0.3f, 0.3f, 277.2f, 277.2f, 0.5f, 2f, 2);
                Tone(b, 0.6f, 0.95f, 329.6f, 329.6f, 0.55f, 2f, 2);
                Tone(b, 0.6f, 0.95f, 220f, 220f, 0.3f, 2f, 2);
                Tone(b, 0f, 0.25f, 120f, 45f, 0.9f, 10f, 0);       // bombo de apertura
                Noise(b, 0.6f, 0.5f, 0.25f, 5f, 8000f, 8000f);      // platillo
                break;

            case Sfx.WaveClear:
                b = Buf(1.4f);
                float[] clear = { 523.25f, 659.25f, 783.99f, 1046.5f };
                for (int i = 0; i < clear.Length; i++)
                {
                    Tone(b, i * 0.12f, 0.9f, clear[i], clear[i], 0.45f, 4f, 0);
                    Tone(b, i * 0.12f, 0.6f, clear[i] * 2f, clear[i] * 2f, 0.15f, 6f, 0);
                }
                break;

            case Sfx.Hurt:
                b = Buf(0.4f);
                Noise(b, 0f, 0.3f, 0.7f, 9f, 3500f, 400f);
                Tone(b, 0f, 0.35f, 210f, 60f, 0.8f, 8f, 3);
                break;

            case Sfx.GameOver:
                b = Buf(2.4f);
                float[] over = { 392f, 349.2f, 311.1f, 261.6f };
                for (int i = 0; i < over.Length; i++)
                {
                    float len = i == over.Length - 1 ? 1.4f : 0.45f;
                    Tone(b, i * 0.45f, len, over[i], over[i] * 0.99f, 0.5f, i == over.Length - 1 ? 1.6f : 3f, 1);
                }
                Tone(b, 1.35f, 1.0f, 98f, 70f, 0.6f, 2f, 0);
                break;

            case Sfx.MenuOpen:
                b = Buf(0.2f);
                Tone(b, 0f, 0.15f, 500f, 760f, 0.5f, 12f, 1);
                break;

            case Sfx.MenuTick:
                b = Buf(0.07f);
                Tone(b, 0f, 0.05f, 950f, 900f, 0.5f, 40f, 1);
                break;

            case Sfx.MenuDeny:
                b = Buf(0.25f);
                Tone(b, 0f, 0.09f, 150f, 140f, 0.7f, 8f, 2);
                Tone(b, 0.11f, 0.12f, 120f, 110f, 0.7f, 8f, 2);
                break;

            case Sfx.SauceOn:
                b = Buf(0.9f);
                Noise(b, 0f, 0.5f, 0.5f, 3f, 600f, 7000f);          // "fiuuu" ascendente
                Tone(b, 0f, 0.5f, 200f, 1200f, 0.35f, 2f, 1);
                float[] on = { 784f, 1046.5f, 1318.5f };
                for (int i = 0; i < on.Length; i++)
                    Tone(b, 0.35f + i * 0.08f, 0.4f, on[i], on[i], 0.35f, 7f, 0);
                break;

            case Sfx.SauceOff:
                b = Buf(0.5f);
                Tone(b, 0f, 0.4f, 700f, 160f, 0.5f, 5f, 1);
                Noise(b, 0f, 0.3f, 0.3f, 8f, 5000f, 400f);
                break;

            case Sfx.Ignite:
                b = Buf(0.7f);
                Noise(b, 0f, 0.6f, 0.9f, 4f, 400f, 5000f);
                Noise(b, 0.05f, 0.5f, 0.5f, 6f, 2500f, 900f);
                Tone(b, 0f, 0.3f, 90f, 160f, 0.4f, 6f, 0);
                break;

            case Sfx.Splat:
                b = Buf(0.2f);
                Tone(b, 0f, 0.14f, 420f, 130f, 0.7f, 18f, 0);
                Noise(b, 0f, 0.1f, 0.5f, 25f, 2500f, 800f);
                break;

            default: // Pepper
                b = Buf(0.18f);
                Noise(b, 0f, 0.1f, 0.6f, 35f, 10000f, 10000f);
                Tone(b, 0f, 0.15f, 2400f, 1800f, 0.35f, 20f, 0);
                Tone(b, 0.03f, 0.12f, 3100f, 2600f, 0.25f, 25f, 0);
                break;
        }

        return ToClip(sfx.ToString(), b);
    }

    // -------------------------------------------------------------------- Musica

    private static float Note(float semitonesFromA4)
    {
        return 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);
    }

    private static AudioClip BuildMenuMusic()
    {
        const float bpm = 112f;
        float beat = 60f / bpm;
        float bar = beat * 4f;
        int bars = 8;

        wrap = true;
        float[] drums = Buf(bar * bars);
        float[] melodic = Buf(bar * bars);   // arpegio + melodia + acordes (lleva eco)

        // Progresion Am - F - C - G (dos vueltas). Cada acorde: nota del bajo y 3 notas del acorde.
        float[] bass = { 110f, 87.31f, 130.81f, 98f };
        float[][] chords =
        {
            new[] { 220f, 261.63f, 329.63f },
            new[] { 174.61f, 220f, 261.63f },
            new[] { 261.63f, 329.63f, 392f },
            new[] { 196f, 246.94f, 293.66f }
        };

        const int R = int.MinValue;   // silencio
        int[][] lead =
        {
            new[] { 12, R, 10, R, 7, R, 5, 7 },
            new[] { 5, R, 7, R, 10, 7, 5, R },
            new[] { 12, R, 10, 12, 15, R, 12, R },
            new[] { 10, R, 7, R, 5, R, 3, 0 }
        };

        int[] arpOrder = { 0, 1, 2, 1, 0, 1, 2, 1 };

        for (int k = 0; k < bars; k++)
        {
            float t0 = k * bar;
            int ch = k % 4;
            bool second = k >= 4;

            // Bombo (1 y 3, y un golpe extra en la segunda vuelta) y caja (2 y 4).
            Tone(drums, t0, 0.2f, 130f, 42f, 0.95f, 11f, 0);
            Tone(drums, t0 + beat * 2f, 0.2f, 130f, 42f, 0.95f, 11f, 0);
            if (second) Tone(drums, t0 + beat * 3.5f, 0.2f, 130f, 42f, 0.7f, 11f, 0);

            for (int sn = 1; sn <= 3; sn += 2)
            {
                Noise(drums, t0 + beat * sn, 0.15f, 0.4f, 16f, 6500f, 2500f);
                Tone(drums, t0 + beat * sn, 0.1f, 190f, 120f, 0.35f, 22f, 0);
            }

            for (int e = 0; e < 8; e++)
            {
                float te = t0 + e * beat * 0.5f;

                // Hi-hat en cada corchea (la de contratiempo, mas fuerte).
                Noise(drums, te, 0.04f, (e % 2 == 1) ? 0.14f : 0.08f, 70f, 15000f, 15000f);

                // Bajo: raiz en las corcheas pares, octava arriba cada cuarta.
                float bf = bass[ch] * ((e % 4 == 3) ? 2f : 1f);
                Tone(drums, te, beat * 0.45f, bf, bf, 0.55f, 6f, 1);

                // Arpegio suave.
                float af = chords[ch][arpOrder[e]];
                Tone(melodic, te, beat * 0.42f, af, af, 0.16f, 9f, 2);

                // Melodia (solo en la segunda vuelta).
                if (second)
                {
                    int semis = lead[ch][e];
                    if (semis != R)
                    {
                        float lf = Note(semis);
                        Tone(melodic, te, beat * 0.5f, lf, lf, 0.2f, 6f, 1);
                    }
                }
            }

            // Colchon de acordes largo.
            for (int n = 0; n < 3; n++)
                Tone(melodic, t0, bar, chords[ch][n] * 0.5f, chords[ch][n] * 0.5f, 0.07f, 0.4f, 1, 0.35f);
        }

        // Mezcla con eco (3/8 de pulso) sobre la parte melodica.
        float[] mix = new float[drums.Length];
        int delay = Mathf.RoundToInt(beat * 0.75f * SR);
        for (int i = 0; i < mix.Length; i++)
        {
            int j = (i - delay + mix.Length) % mix.Length;
            mix[i] = drums[i] + melodic[i] + 0.35f * melodic[j];
        }

        wrap = false;
        return ToClip("Musica", mix, 0.9f);
    }

    // Pista de combate: 150 bpm, bombo en todos los tiempos, bajo en semicorcheas, arpegio rapido,
    // melodia y acordes potentes, redobles y subida al final. Mucha energia constante.
    private static AudioClip BuildBattleMusic()
    {
        const float bpm = 150f;
        float beat = 60f / bpm;
        float sixteenth = beat * 0.25f;
        float bar = beat * 4f;
        int bars = 8;

        wrap = true;
        float[] drums = Buf(bar * bars);
        float[] melodic = Buf(bar * bars);

        // Am - F - G - Am (dos vueltas; la segunda acaba en E para tirar hacia el principio).
        float[] roots = { 110f, 87.31f, 98f, 110f, 110f, 87.31f, 98f, 82.41f };
        float[][] chords =
        {
            new[] { 220f, 261.63f, 329.63f, 440f },
            new[] { 174.61f, 220f, 261.63f, 349.23f },
            new[] { 196f, 246.94f, 293.66f, 392f },
            new[] { 220f, 261.63f, 329.63f, 440f },
            new[] { 220f, 261.63f, 329.63f, 440f },
            new[] { 174.61f, 220f, 261.63f, 349.23f },
            new[] { 196f, 246.94f, 293.66f, 392f },
            new[] { 164.81f, 207.65f, 246.94f, 329.63f }
        };

        const int R = int.MinValue;
        // Semitonos sobre La4. Dos frases de melodia (8 corcheas por compas) para la segunda vuelta.
        int[][] lead =
        {
            new[] { 12, R, 12, 15, 17, R, 15, 12 },
            new[] { 12, R, 10, 12, 17, R, 12, 10 },
            new[] { 14, R, 12, 14, 19, R, 17, 14 },
            new[] { 12, 14, 15, 17, 19, 17, 15, 14 }
        };
        int[] arpOrder = { 0, 1, 2, 3, 2, 1, 2, 3, 0, 1, 2, 3, 2, 3, 2, 1 };

        for (int k = 0; k < bars; k++)
        {
            float t0 = k * bar;
            bool second = k >= 4;
            bool fillBar = k == 3 || k == 7;

            for (int b4 = 0; b4 < 4; b4++)
            {
                float tb = t0 + b4 * beat;

                // Bombo en todos los tiempos.
                Tone(drums, tb, 0.17f, 140f, 40f, 1f, 12f, 0);

                // Palmada/caja en el 2 y el 4.
                if (b4 == 1 || b4 == 3)
                {
                    Noise(drums, tb, 0.16f, 0.55f, 14f, 8000f, 3000f);
                    Tone(drums, tb, 0.12f, 220f, 130f, 0.4f, 20f, 0);
                }

                // Hi-hat abierto a contratiempo.
                Noise(drums, tb + beat * 0.5f, 0.1f, 0.16f, 22f, 15000f, 15000f);
            }

            // Redoble de caja en la ultima negra de los compases 4 y 8, subiendo de volumen.
            if (fillBar)
            {
                for (int f = 0; f < 4; f++)
                {
                    float amp = 0.3f + f * 0.15f;
                    Noise(drums, t0 + beat * 3f + f * sixteenth, 0.09f, amp, 22f, 7000f, 3000f);
                }
            }

            // Subida de ruido en los dos ultimos tiempos del compas 8, para volver a empezar con fuerza.
            if (k == bars - 1)
                Noise(melodic, t0 + beat * 2f, beat * 2f, 0.45f, 0f, 500f, 9000f);

            // 16 semicorcheas por compas.
            for (int i = 0; i < 16; i++)
            {
                float ts = t0 + i * sixteenth;

                // Hi-hat cerrado.
                if (i % 2 == 1) Noise(drums, ts, 0.03f, 0.1f, 90f, 15000f, 15000f);

                // Bajo en semicorcheas, con salto de octava y de quinta para que "ruede".
                float bf = roots[k];
                if (i % 8 == 6) bf *= 2f;
                else if (i % 4 == 3) bf *= 1.5f;
                if (i % 4 != 0) Tone(drums, ts, sixteenth * 0.9f, bf, bf, 0.38f, 14f, 3);

                // Arpegio rapido.
                float af = chords[k][arpOrder[i]] * 1.0f;
                Tone(melodic, ts, sixteenth * 0.8f, af, af, 0.13f, 11f, 2);
            }

            // Golpes de acorde (power chord) en el 1 y en el "y" del 2, con melodia en la segunda vuelta.
            float[] stabs = { 0f, beat * 1.5f };
            for (int sIdx = 0; sIdx < stabs.Length; sIdx++)
            {
                for (int n = 0; n < 3; n++)
                    Tone(melodic, t0 + stabs[sIdx], beat * 0.7f, chords[k][n], chords[k][n], 0.12f, 6f, 3);
            }

            if (second)
            {
                for (int e = 0; e < 8; e++)
                {
                    int semis = lead[k % 4][e];
                    if (semis == R) continue;
                    float lf = Note(semis);
                    Tone(melodic, t0 + e * beat * 0.5f, beat * 0.5f, lf, lf, 0.2f, 5f, 2);
                }
            }
        }

        // Mezcla con eco y saturacion suave para que suene mas gruesa y agresiva.
        float[] mix = new float[drums.Length];
        int delay = Mathf.RoundToInt(beat * 0.75f * SR);
        for (int i = 0; i < mix.Length; i++)
        {
            int j = (i - delay + mix.Length) % mix.Length;
            float x = drums[i] + melodic[i] + 0.3f * melodic[j];
            x *= 1.7f;
            mix[i] = x / (1f + Mathf.Abs(x));
        }

        wrap = false;
        return ToClip("MusicaCombate", mix, 0.9f);
    }
}
