using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Achievements : MonoBehaviour
{
    public class Def
    {
        public string id, name, goal;
        public string reward;
        public System.Func<bool> done;
        public System.Func<string> progress;
    }

    private static HUD hud;
    private static PlayerStats stats;
    private static PlayerCollabs collabs;
    private static PlayerWeapons weapons;
    private static EnemySpawner spawner;
    private static float RunTime => hud != null ? hud.RunTime : 0f;
    private static int Level => stats != null ? stats.level : 0;

    private static int seenHits;
    private static float lastHitAt;
    private const float NoHitFrom = 600f, NoHitFor = 180f;

    private static int MaxedWeapons()
    {
        if (weapons == null) return 0;
        int n = 0;
        foreach (string id in weapons.OwnedInOrder)
            if (!PlayerWeapons.IsCollab(id) && weapons.LevelOf(id) >= Weapon.MaxLevel) n++;
        return n;
    }

    public static void MarkTried(string kind, string id)
    {
        string key = $"tried_{kind}_{id}";
        if (PlayerPrefs.GetInt(key, 0) == 1) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    private static int TriedCount()
    {
        int n = 0;
        foreach (var d in PlayerWeapons.All) if (PlayerPrefs.GetInt("tried_w_" + d.id, 0) == 1) n++;
        foreach (var s in PlayerSkills.All) if (PlayerPrefs.GetInt("tried_s_" + s.id, 0) == 1) n++;
        foreach (var c in PlayerCollabs.All) if (PlayerPrefs.GetInt("tried_c_" + c.id, 0) == 1) n++;
        foreach (var x in PlayerCollabs.Supers) if (PlayerPrefs.GetInt("tried_x_" + x.id, 0) == 1) n++;
        return n;
    }

    private static int TryTotal => PlayerWeapons.All.Length + PlayerSkills.All.Length + PlayerCollabs.All.Length + PlayerCollabs.Supers.Length;

    private static void ResetTried()
    {
        foreach (var d in PlayerWeapons.All) PlayerPrefs.DeleteKey("tried_w_" + d.id);
        foreach (var s in PlayerSkills.All) PlayerPrefs.DeleteKey("tried_s_" + s.id);
        foreach (var c in PlayerCollabs.All) PlayerPrefs.DeleteKey("tried_c_" + c.id);
        foreach (var x in PlayerCollabs.Supers) PlayerPrefs.DeleteKey("tried_x_" + x.id);
    }

    public const string MeowlReward = "companion:meowl";
    public static bool MeowlUnlocked => System.Array.Exists(All, d => d.reward == MeowlReward && IsUnlocked(d));

    private static int runKillsSeen;
    private static float runTimeSeen;
    public static int LifetimeKills => PlayerPrefs.GetInt("life_kills", 0) + runKillsSeen;
    public static float LifetimeSeconds => PlayerPrefs.GetFloat("life_seconds", 0f) + runTimeSeen;

    private static void BankRun()
    {
        if (runKillsSeen == 0 && runTimeSeen <= 0f) return;
        PlayerPrefs.SetInt("life_kills", LifetimeKills);
        PlayerPrefs.SetFloat("life_seconds", LifetimeSeconds);
        PlayerPrefs.Save();
        runKillsSeen = 0;
        runTimeSeen = 0f;
    }

    public static readonly Def[] All =
    {
        new Def { id = "survive5", name = "Nine Lives", goal = "Survive 10 minutes", reward = "weapon:laser", done = () => RunTime >= 600f },
        new Def { id = "kills1000", name = "Cat Wrangler", goal = "Defeat 3,000 cats in one run", reward = "weapon:mines", done = () => EnemyHealth.Kills >= 3000 },
        new Def { id = "boss", name = "Eye Doctor", goal = "Defeat 3 bosses in one run", reward = "weapon:blackhole", done = () => EnemyHealth.BossKills >= 3 },
        new Def { id = "collab", name = "Mix & Match", goal = "Make 2 combo weapons in one run", reward = "skill:overdrive", done = () => collabs != null && collabs.Taken.Count >= 2 },
        new Def { id = "elites25", name = "Fancy Feast", goal = "Defeat 60 elite cats in one run", reward = "skill:blacksmith", done = () => EnemyHealth.EliteKills >= 60 },
        new Def { id = "level30", name = "Old Cat, New Tricks", goal = "Reach level 45", reward = "skill:secondwind", done = () => Level >= 45 },
        new Def { id = "super", name = "Purrfect Combo", goal = "Make a super combine", reward = "reroll", done = () => collabs != null && collabs.SuperTaken != null },
        new Def { id = "endless", name = "Final Meow", goal = "Beat the final boss", reward = "eliminate", done = () => EnemySpawner.Endless },
        new Def { id = "endless10", name = "Still Herding", goal = "Survive 15 minutes of endless", reward = "reroll", done = () => spawner != null && EnemySpawner.Endless && spawner.EndlessMinutes >= 15f },

        new Def { id = "nohit", name = "Nimble Paws", goal = "No hits for 3 minutes after 10:00", reward = "skill:dodge",
                  done = () => RunTime >= NoHitFrom + NoHitFor && RunTime - Mathf.Max(lastHitAt, NoHitFrom) >= NoHitFor },
        new Def { id = "chests", name = "Box Inspector", goal = "Open 12 chests in one run", reward = "skill:thickskin", done = () => RewardMenu.ChestsOpened >= 12 },
        new Def { id = "golden", name = "Fat Cats", goal = "Catch 3 golden cats in one run", reward = "weapon:glue", done = () => EnemyHealth.GoldenKills >= 3 },
        new Def { id = "arsenal", name = "Toy Box", goal = "Have 5 weapons at max level at once", reward = "eliminate", done = () => MaxedWeapons() >= 5 },
        new Def { id = "speedrun", name = "Zoomies", goal = "Reach level 40 before 10:00", reward = "reroll", done = () => Level >= 40 && RunTime < 600f },
        new Def { id = "score", name = "Top Cat", goal = "Score 150,000 in one run", reward = "reroll", done = () => Leaderboard.Score(RunTime, Level) >= 150000 },
        new Def { id = "herder", name = "Cat Herder", goal = "25,000 cats in total", reward = "eliminate",
                  done = () => LifetimeKills >= 25000, progress = () => $"{Mathf.Min(LifetimeKills, 25000):N0} / 25,000" },
        new Def { id = "marathon", name = "Couch Cat", goal = "Play 3 hours in total", reward = "reroll",
                  done = () => LifetimeSeconds >= 10800f, progress = () => $"{(int)(LifetimeSeconds / 3600f)}h {(int)(LifetimeSeconds % 3600f / 60f):00}m / 3h" },
        new Def { id = "tryall", name = "Seen It All", goal = "Try every weapon, skill, combo and super", reward = MeowlReward,
                  done = () => TriedCount() >= TryTotal, progress = () => $"{TriedCount()} / {TryTotal}" },
    };

    public static bool IsUnlocked(Def d) => PlayerPrefs.GetInt("ach_" + d.id, 0) == 1;

    public static bool IsLocked(string reward) => System.Array.Exists(All, d => d.reward == reward && !IsUnlocked(d));

    public static int Count(string reward) => System.Array.FindAll(All, d => d.reward == reward && IsUnlocked(d)).Length;

    public static string RewardText(string reward)
    {
        if (reward == "reroll") return "+1 Reroll every run";
        if (reward == "eliminate") return "+1 Eliminate every run";
        if (reward == MeowlReward) return "a secret companion: MEOWL!";
        string id = reward.Substring(reward.IndexOf(':') + 1);
        return reward.StartsWith("weapon:") ? "Weapon: " + PlayerWeapons.Find(id).name : "Skill: " + PlayerSkills.Find(id).name;
    }

    public static void ResetAll()
    {
        foreach (Def d in All) PlayerPrefs.DeleteKey("ach_" + d.id);
        PlayerPrefs.DeleteKey("life_kills");
        PlayerPrefs.DeleteKey("life_seconds");
        ResetTried();
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("Achievements");
        DontDestroyOnLoad(go);
        go.AddComponent<Achievements>();
        SceneManager.sceneUnloaded += _ => { BankRun(); seenHits = 0; lastHitAt = 0f; };
    }

    private void OnApplicationQuit() => BankRun();

    private float nextCheck;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck || !MainMenu.Started || MainMenu.IsOpen) return;
        nextCheck = Time.unscaledTime + 0.5f;
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (stats == null) stats = FindFirstObjectByType<PlayerStats>();
        if (collabs == null) collabs = FindFirstObjectByType<PlayerCollabs>();
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeapons>();
        if (spawner == null) spawner = FindFirstObjectByType<EnemySpawner>();
        runKillsSeen = EnemyHealth.Kills;
        runTimeSeen = RunTime;
        if (PlayerHealth.Hits != seenHits) { seenHits = PlayerHealth.Hits; lastHitAt = RunTime; }
        if (stats == null || stats.currentHealth <= 0f) return;
        if (Meowl.Instance == null && MeowlUnlocked) Meowl.Join(stats.gameObject);

        foreach (Def d in All)
        {
            if (IsUnlocked(d) || !d.done()) continue;
            PlayerPrefs.SetInt("ach_" + d.id, 1);
            PlayerPrefs.Save();
            StartCoroutine(Toast(d));
            Sfx.Play(Sfx.Id.LevelUp, 0.6f, 1.2f);
            nextCheck += 3f;
            return;
        }
    }

    private Canvas toastCanvas;
    private readonly System.Collections.Generic.List<RectTransform> toasts = new System.Collections.Generic.List<RectTransform>();
    private const float ToastW = 360f, ToastH = 88f, ToastGap = 8f, ToastMargin = 16f;

    private System.Collections.IEnumerator Toast(Def d)
    {
        if (toastCanvas == null)
        {
            toastCanvas = RuntimeUI.Canvas("AchievementToasts", 500);
            toastCanvas.transform.SetParent(transform, false);
        }
        Vector2 corner = new Vector2(1f, 0f);
        RectTransform box = CatTheme.Panel(toastCanvas.transform, "Toast", CatTheme.Style.PanelGold, Vector2.zero, new Vector2(ToastW, ToastH)).rectTransform;
        box.anchorMin = box.anchorMax = box.pivot = corner;
        Image slot = CatTheme.Panel(box, "Icon", CatTheme.Style.SlotGold, new Vector2(14f + 32f, 0f), new Vector2(64f, 64f));
        slot.rectTransform.anchorMin = slot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        CatTheme.Img(slot.transform, "Cat", CatTheme.Load("cat_white"), Vector2.zero, new Vector2(48f, 48f));
        TMPro.TextMeshProUGUI Line(string text, float size, Color color, float y)
        {
            TMPro.TextMeshProUGUI t = RuntimeUI.Outline(RuntimeUI.Text(box, text, size, color, new Vector2(48f, y), new Vector2(ToastW - 120f, size + 6f), TMPro.TextAlignmentOptions.Left));
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            return t;
        }
        Line("ACHIEVEMENT UNLOCKED", 13f, CatTheme.Faint, 24f);
        Line(d.name, 22f, CatTheme.Gold, 2f);
        Line(RewardText(d.reward), 14f, CatTheme.Cream, -24f);
        toasts.Add(box);

        float Slot() => ToastMargin + toasts.IndexOf(box) * (ToastH + ToastGap);
        float t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 4.5f)
        {
            float t = Time.unscaledTime - t0;
            float inK = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.3f), 3f), outK = Mathf.Clamp01((t - 4.2f) / 0.3f);
            box.anchoredPosition = new Vector2(-ToastMargin, Mathf.Lerp(-ToastH, Slot(), inK * (1f - outK * outK)));
            yield return null;
        }
        toasts.Remove(box);
        Destroy(box.gameObject);
    }
}
