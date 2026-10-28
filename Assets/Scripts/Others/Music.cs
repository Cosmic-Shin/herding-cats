using UnityEngine;

public class Music : MonoBehaviour
{
    public static float Volume = 0.6f;
    private const float FadeTime = 1.2f, LateTime = 600f;

    private AudioSource a, b;
    private string current;
    private bool stingPlayed;
    private EnemySpawner spawner;
    private BossTimeline bosses;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("Music");
        DontDestroyOnLoad(go);
        go.AddComponent<Music>();
    }

    private void Awake()
    {
        a = Source();
        b = Source();
    }

    private AudioSource Source()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.priority = 0;
        s.playOnAwake = false;
        s.ignoreListenerPause = true;
        s.volume = 0f;
        return s;
    }

    private string Wanted()
    {
        if (!MainMenu.Started) return "menu";
        if (GameOverScreen.IsShowing) return null;
        if (spawner == null) spawner = FindFirstObjectByType<EnemySpawner>();
        if (bosses == null) bosses = FindFirstObjectByType<BossTimeline>();
        if (EnemySpawner.Endless) return "endless";
        if (bosses != null && bosses.BossFight) return "boss";
        return spawner != null && spawner.RunTime >= LateTime ? "late" : "run";
    }

    private void Update()
    {
        string want = Wanted();
        if (want == null && !stingPlayed) { stingPlayed = true; Sfx.Play(Sfx.Id.GameOver, 1f); }
        if (want != null) stingPlayed = false;

        if (want != current)
        {
            current = want;
            (a, b) = (b, a);
            AudioClip clip = want != null ? Resources.Load<AudioClip>("Music/" + want) : null;
            if (clip != null && (a.clip != clip || !a.isPlaying)) { a.clip = clip; a.Play(); }
            else if (clip == null) a.Stop();
        }
        else if (current != null && a.clip != null && !a.isPlaying) a.Play();

        float step = Time.unscaledDeltaTime / FadeTime;
        a.volume = a.clip != null && a.isPlaying ? Mathf.MoveTowards(a.volume, Volume, step) : 0f;
        b.volume = Mathf.MoveTowards(b.volume, 0f, step);
        if (b.volume <= 0f && b.isPlaying) b.Stop();
    }
}
