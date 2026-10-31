using UnityEngine;

public class Music : MonoBehaviour
{
    public static float Volume = 0.6f;
    private const float FadeTime = 1.2f, LateTime = 600f;

    private AudioSource a, b;
    private string current;
    private bool stingPlayed;
    private float reached; // furthest point heard in the current clip
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
        s.loop = false;
        s.priority = 0;
        s.playOnAwake = false;
        s.ignoreListenerPause = true;
        s.volume = 0f;
        return s;
    }

    // Tracks are grouped by name: "run", "run_2", "run_3"... A random one plays, then a different one when it ends.
    private static AudioClip[] tracks;

    private static AudioClip Pick(string group, AudioClip avoid)
    {
        tracks ??= Resources.LoadAll<AudioClip>("Music");
        AudioClip[] list = System.Array.FindAll(tracks, c => c.name == group || c.name.StartsWith(group + "_"));
        if (list.Length > 1) list = System.Array.FindAll(list, c => c != avoid);
        return list.Length > 0 ? list[Random.Range(0, list.Length)] : null;
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
            reached = 0f;
            AudioClip clip = want != null ? Pick(want, null) : null;
            if (clip != null && (a.clip != clip || !a.isPlaying)) { a.clip = clip; a.Play(); }
            else if (clip == null) a.Stop();
        }
        else if (current != null && a.clip != null && !a.isPlaying)
        {
            // Not playing: either the song ended, or it is still loading / paused. Only move on if it got near the end.
            if (reached > a.clip.length - 1f) { a.clip = Pick(current, a.clip); reached = 0f; }
            a.Play();
        }
        if (a.isPlaying) reached = Mathf.Max(reached, a.time);

        float step = Time.unscaledDeltaTime / FadeTime;
        a.volume = a.clip != null && a.isPlaying ? Mathf.MoveTowards(a.volume, Volume, step) : 0f;
        b.volume = Mathf.MoveTowards(b.volume, 0f, step);
        if (b.volume <= 0f && b.isPlaying) b.Stop();
    }
}
