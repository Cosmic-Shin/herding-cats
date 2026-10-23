using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

public static class Leaderboard
{
    public const int Size = 50;

    [Serializable] public class Item { public string icon, level; }

    [Serializable]
    public class Entry
    {
        public string name;
        public string device;
        public int score, seconds, level, kills, bossKills;
        public bool endless;
        public string hero, map; // empty on runs from before heroes and maps existed (those were Mira on Meadow)
        public List<Item> build = new List<Item>();
    }

    [Serializable] private class Data { public List<Entry> entries = new List<Entry>(); }

    private static string FilePath => Path.Combine(Application.persistentDataPath, "leaderboard.json");

    private static string deviceId;
    public static string DeviceId
    {
        get
        {
            if (deviceId != null) return deviceId;
            string id = SystemInfo.deviceUniqueIdentifier;
            if (!string.IsNullOrEmpty(id) && id != SystemInfo.unsupportedIdentifier) return deviceId = id;
            id = PlayerPrefs.GetString("deviceId", "");
            if (id == "") { id = Guid.NewGuid().ToString(); PlayerPrefs.SetString("deviceId", id); PlayerPrefs.Save(); }
            return deviceId = id;
        }
    }

    public static bool IsMine(Entry e) => e != null && (string.IsNullOrEmpty(e.device) || e.device == DeviceId);

    public static int Score(float seconds, int level) => (int)seconds * 10 + EnemyHealth.KillScore + level * 100;

    public static string HeroOf(Entry e) => string.IsNullOrEmpty(e.hero) ? "Mira" : e.hero;
    public static string MapOf(Entry e) => string.IsNullOrEmpty(e.map) ? "Meadow" : e.map;

    // The shared online board when we have it, otherwise this PC's local file.
    public static List<Entry> Load() => Load(null, null);

    // Filtered by hero and/or map (null = any). The online "all" board only keeps each player's single best run,
    // so filtered views merge the per hero+map online boards, matching runs on the all board, and this PC's runs.
    public static List<Entry> Load(string hero, string map)
    {
        if (hero == null && map == null && Online != null) return new List<Entry>(Online);
        bool Match(Entry e) => (hero == null || HeroOf(e) == hero) && (map == null || MapOf(e) == map);
        var list = new List<Entry>();
        if (hero != null || map != null)
        {
            if (Online != null) list.AddRange(Online.FindAll(Match));
            foreach (var board in OnlineCombos) list.AddRange(board.Value.FindAll(Match));
        }
        list.AddRange(LoadLocal().FindAll(Match));
        list.Sort((a, b) => b.score.CompareTo(a.score));
        KeepBestPerName(list);
        if (list.Count > Size) list.RemoveRange(Size, list.Count - Size);
        return list;
    }

    // One row per player name (their best). The same person on two PCs or installs gets two online
    // accounts, and the local file keeps every run, so without this a name shows up more than once.
    // Expects the list sorted best-first.
    // 1-based rank of this name's best score on the board shown in menus, 0 if not on it.
    public static int RankOf(string name)
    {
        string key = (name ?? "").Trim().ToLowerInvariant();
        return Load().FindIndex(e => (e.name ?? "").Trim().ToLowerInvariant() == key) + 1;
    }

    private static void KeepBestPerName(List<Entry> list)
    {
        var seen = new HashSet<string>();
        list.RemoveAll(e => !seen.Add((e.name ?? "").Trim().ToLowerInvariant()));
    }

    private static List<Entry> LoadLocal()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonUtility.FromJson<Data>(File.ReadAllText(FilePath))?.entries ?? new List<Entry>();
        }
        catch (Exception e) { Debug.LogWarning("Leaderboard unreadable, starting fresh: " + e.Message); }
        return new List<Entry>();
    }

    public static int Submit(Entry entry)
    {
        List<Entry> list = LoadLocal();
        list.Add(entry);
        int rank = Insert(list, entry);
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(new Data { entries = list })); }
        catch (Exception e) { Debug.LogWarning("Leaderboard not saved: " + e.Message); }

        _ = Upload(entry);
        if (Online == null) return rank;
        // Online keeps one entry per player (their best), so only a new personal best gets a rank.
        Entry best = Online.Find(IsMine);
        if (best != null && best.score >= entry.score) return 0;
        Online.Remove(best);
        Online.Add(entry);
        return Insert(Online, entry);
    }

    // Sorts, keeps one row per name, trims to Size, and returns the entry's 1-based rank
    // (0 if it fell off, or if that name already has a better score).
    private static int Insert(List<Entry> list, Entry entry)
    {
        list.Sort((a, b) => b.score.CompareTo(a.score));
        KeepBestPerName(list);
        int rank = list.IndexOf(entry) + 1;
        if (list.Count > Size) list.RemoveRange(Size, list.Count - Size);
        return rank <= Size ? rank : 0;
    }

    // ---- Online board (Unity Leaderboards, anonymous sign-in) ----
    // ponytail: client-submitted scores, so a determined cheater can post fakes; route through Cloud Code if that ever matters.
    private const string BoardId = "highscores";
    // One extra board per hero + map ("hs_kai_volcano" ...), so a player's best on each shows up in the filters.
    // These have to exist in the Unity dashboard; until they do, uploads/fetches to them just fail quietly.
    private static string ComboId(string hero, string map) => $"hs_{hero}_{map}".ToLowerInvariant();
    private static readonly Dictionary<string, List<Entry>> OnlineCombos = new Dictionary<string, List<Entry>>();
    private static bool comboWarned;
    public static List<Entry> Online { get; private set; }
    public static event Action Changed;
    private static Task<bool> signIn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Online = null; Changed = null; signIn = null; OnlineCombos.Clear(); comboWarned = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() => _ = Refresh();

    private static Task<bool> SignIn() => signIn ??= SignInAsync();

    private static async Task<bool> SignInAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            return true;
        }
        catch (Exception e) { Debug.LogWarning("Online leaderboard unavailable: " + e.Message); signIn = null; return false; }
    }

    public static async Task Refresh()
    {
        if (!await SignIn()) return;
        try
        {
            Online = await Fetch(BoardId);
            Changed?.Invoke();
        }
        catch (Exception e) { Debug.LogWarning("Online leaderboard fetch failed: " + e.Message); }

        foreach (Hero.Def h in Hero.All)
            foreach (string m in MapGenerator.Names)
            {
                string id = ComboId(h.id, m);
                try { OnlineCombos[id] = await Fetch(id); }
                catch (Exception e) { ComboWarn(e); }
            }
        Changed?.Invoke();
    }

    private static async Task<List<Entry>> Fetch(string boardId)
    {
        var page = await LeaderboardsService.Instance.GetScoresAsync(boardId, new GetScoresOptions { Limit = Size, IncludeMetadata = true });
        string me = AuthenticationService.Instance.PlayerId;
        var list = new List<Entry>();
        foreach (var r in page.Results)
        {
            Entry e = string.IsNullOrEmpty(r.Metadata) ? null : JsonUtility.FromJson<Entry>(r.Metadata);
            if (e == null) continue;
            e.score = (int)r.Score;
            e.device = r.PlayerId == me ? DeviceId : r.PlayerId; // keeps IsMine working
            list.Add(e);
        }
        KeepBestPerName(list); // server order is already best-first
        return list;
    }

    private static void ComboWarn(Exception e)
    {
        if (comboWarned) return;
        comboWarned = true;
        Debug.LogWarning("Per hero/map online boards unavailable (create hs_<hero>_<map> boards in the Unity dashboard): " + e.Message);
    }

    private static async Task Upload(Entry entry)
    {
        if (!await SignIn()) return;
        try
        {
            Entry copy = JsonUtility.FromJson<Entry>(JsonUtility.ToJson(entry));
            copy.device = null; // don't publish the hardware id
            await LeaderboardsService.Instance.AddPlayerScoreAsync(BoardId, entry.score, new AddPlayerScoreOptions { Metadata = copy });
            try { await LeaderboardsService.Instance.AddPlayerScoreAsync(ComboId(HeroOf(entry), MapOf(entry)), entry.score, new AddPlayerScoreOptions { Metadata = copy }); }
            catch (Exception e) { ComboWarn(e); }
            await Refresh();
        }
        catch (Exception e) { Debug.LogWarning("Score upload failed: " + e.Message); }
    }

    public static Entry FromRun(PlayerStats stats, float seconds)
    {
        var entry = new Entry
        {
            name = MainMenu.PlayerName,
            device = DeviceId,
            score = Score(seconds, stats.level),
            seconds = (int)seconds,
            level = stats.level,
            kills = EnemyHealth.Kills,
            bossKills = EnemyHealth.BossKills,
            endless = EnemySpawner.Endless,
            hero = Hero.Current.id,
            map = MapGenerator.Names[MapGenerator.Biome],
        };

        if (stats.TryGetComponent(out PlayerWeapons weapons))
            foreach (string id in weapons.OwnedInOrder)
            {
                int level = weapons.LevelOf(id);
                entry.build.Add(new Item { icon = id, level = PlayerWeapons.IsCollab(id) ? "" : level >= Weapon.MaxLevel ? "MAX" : level.ToString() });
            }
        if (stats.TryGetComponent(out PlayerSkills skills))
            foreach (string id in skills.OwnedInOrder)
            {
                int level = skills.LevelOf(id);
                entry.build.Add(new Item { icon = "skill:" + id, level = level >= PlayerSkills.Find(id).levels.Length ? "MAX" : level.ToString() });
            }
        return entry;
    }
}
