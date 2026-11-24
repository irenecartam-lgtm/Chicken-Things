using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// Al darle a JUGAR: el menu se funde a negro, se reproduce el video de intro a pantalla completa,
// se carga la escena del juego y esta aparece con un fundido desde negro.
//   IntroVideo.PlayAndLoad("SampleScene");          // con video
//   IntroVideo.PlayAndLoad("SampleScene", false);   // sin video (solo el fundido, p. ej. para "Continuar")
// El video es Assets/StreamingAssets/intro.mp4. Se puede saltar con cualquier tecla o clic.
public class IntroVideo : MonoBehaviour
{
    public static string fileName = "intro.mp4";
    public static float fadeInTime = 1.2f;     // duracion del fundido al entrar en el juego
    public static float fadeOutTime = 0.5f;    // el video se funde a negro al terminar

    private static IntroVideo instance;

    private Image black;
    private RawImage videoImage;
    private VideoPlayer player;
    private RenderTexture target;
    private bool finished, failed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static void PlayAndLoad(string sceneName, bool withVideo = true)
    {
        if (instance != null) return;   // ya en marcha (doble clic)

        GameObject go = new GameObject("IntroVideo");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<IntroVideo>();
        instance.Build();
        instance.StartCoroutine(instance.Run(sceneName, withVideo));
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        black = NewImage<Image>("Negro");
        black.color = new Color(0f, 0f, 0f, 0f);

        videoImage = NewImage<RawImage>("Video");
        videoImage.color = new Color(1f, 1f, 1f, 0f);
        videoImage.gameObject.SetActive(false);
    }

    private T NewImage<T>(string objName) where T : Graphic
    {
        GameObject go = new GameObject(objName, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go.AddComponent<T>();
    }

    private IEnumerator Run(string sceneName, bool withVideo)
    {
        // 1) El menu se funde a negro.
        yield return FadeBlack(0f, 1f, 0.3f);

        // 2) Video.
        if (withVideo) yield return PlayVideo();

        // 3) Carga del juego (la pantalla sigue en negro).
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone) yield return null;

        GameAudio.musicDuck = 1f;   // la musica de combate entra con su propio fundido

        // 4) El juego aparece desde negro.
        yield return null;
        yield return FadeBlack(1f, 0f, fadeInTime);

        instance = null;
        Destroy(gameObject);
    }

    private IEnumerator PlayVideo()
    {
        GameAudio.musicDuck = 0f;   // la musica del menu se apaga mientras suena el video

        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        target = new RenderTexture(1920, 1080, 0);
        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        player.url = path;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = target;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = false;
        player.loopPointReached += _ => finished = true;
        player.errorReceived += (_, message) =>
        {
            Debug.LogWarning("[IntroVideo] No se pudo reproducir " + path + ": " + message);
            failed = true;
        };

        player.Prepare();
        float wait = 0f;
        while (!player.isPrepared && !failed && wait < 8f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!player.isPrepared || failed)
        {
            Cleanup();
            yield break;
        }

        videoImage.texture = target;
        videoImage.gameObject.SetActive(true);
        videoImage.color = Color.white;
        player.Play();
        GameAudio.Play(Sfx.Intro, 1f);   // sonido de inicio de pelicula

        float elapsed = 0f;
        float maxTime = (float)player.length + 3f;
        while (!finished && !failed && elapsed < maxTime)
        {
            elapsed += Time.unscaledDeltaTime;

            // Se puede saltar, pero no con el mismo clic que ha pulsado JUGAR.
            if (elapsed > 0.5f && SkipPressed()) break;
            yield return null;
        }

        // El video se funde a negro.
        player.Pause();
        float t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            videoImage.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(t / fadeOutTime));
            yield return null;
        }

        Cleanup();
    }

    private static bool SkipPressed()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (kb != null && kb.anyKey.wasPressedThisFrame)
            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }

    private IEnumerator FadeBlack(float from, float to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / time)));
            yield return null;
        }

        black.color = new Color(0f, 0f, 0f, to);
    }

    private void Cleanup()
    {
        videoImage.gameObject.SetActive(false);

        if (player != null)
        {
            player.Stop();
            Destroy(player);
            player = null;
        }

        if (target != null)
        {
            target.Release();
            Destroy(target);
            target = null;
        }
    }
}
