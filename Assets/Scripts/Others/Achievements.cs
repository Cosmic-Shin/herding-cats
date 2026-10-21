using UnityEngine;
using UnityEngine.SceneManagement;

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
    private static BossTimeline timeline;
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

    // Ids are the save keys (PlayerPrefs "ach_<id>"), so they stay as first written even where the goal was retuned later.
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
    };

    public static bool IsUnlocked(Def d) => PlayerPrefs.GetInt("ach_" + d.id, 0) == 1;

    public static bool IsLocked(string reward) => System.Array.Exists(All, d => d.reward == reward && !IsUnlocked(d));

    public static int Count(string reward) => System.Array.FindAll(All, d => d.reward == reward && IsUnlocked(d)).Length;

    public static string RewardText(string reward)
    {
        if (reward == "reroll") return "+1 Reroll every run";
        if (reward == "eliminate") return "+1 Eliminate every run";
        string id = reward.Substring(reward.IndexOf(':') + 1);
        return reward.StartsWith("weapon:") ? "Weapon: " + PlayerWeapons.Find(id).name : "Skill: " + PlayerSkills.Find(id).name;
    }

    public static void ResetAll()
    {
        foreach (Def d in All) PlayerPrefs.DeleteKey("ach_" + d.id);
        PlayerPrefs.DeleteKey("life_kills");
        PlayerPrefs.DeleteKey("life_seconds");
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

        foreach (Def d in All)
        {
            if (IsUnlocked(d) || !d.done()) continue;
            PlayerPrefs.SetInt("ach_" + d.id, 1);
            PlayerPrefs.Save();
            if (timeline == null) timeline = FindFirstObjectByType<BossTimeline>();
            if (timeline != null) timeline.Announce("<color=#FFD24A>ACHIEVEMENT!", $"{d.name} - unlocked {RewardText(d.reward)}");
            Sfx.Play(Sfx.Id.LevelUp, 0.6f, 1.2f);
            nextCheck += 3f;
            return;
        }
    }
}
