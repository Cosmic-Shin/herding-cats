using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Local top-50 leaderboard, saved as JSON in persistentDataPath. Each entry keeps the run's build
// (icon ids + level tags) so the board can show it HoloCure style.
// Score = 10 per second survived + 10 x the XP value of everything defeated + 100 per level.
public static class Leaderboard
{
    public const int Size = 50;

    [Serializable] public class Item { public string icon, level; }

    [Serializable]
    public class Entry
    {
        public string name;
        public string device; // which computer played it (DeviceId); empty on runs saved before this existed
        public int score, seconds, level, kills, bossKills;
        public bool endless;
        public List<Item> build = new List<Item>(); // weapons first, then skills
    }

    [Serializable] private class Data { public List<Entry> entries = new List<Entry>(); }

    private static string FilePath => Path.Combine(Application.persistentDataPath, "leaderboard.json");

    // "Your" runs are matched by device, not name, since the name can change any time. The file is local, so
    // entries saved before devices were recorded can only have come from this computer.
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

    public static List<Entry> Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonUtility.FromJson<Data>(File.ReadAllText(FilePath))?.entries ?? new List<Entry>();
        }
        catch (Exception e) { Debug.LogWarning("Leaderboard unreadable, starting fresh: " + e.Message); }
        return new List<Entry>();
    }

    // Saves the run; returns its rank (1-based), or 0 when it didn't make the top 50.
    public static int Submit(Entry entry)
    {
        List<Entry> list = Load();
        list.Add(entry);
        list.Sort((a, b) => b.score.CompareTo(a.score));
        int rank = list.IndexOf(entry) + 1;
        if (list.Count > Size) list.RemoveRange(Size, list.Count - Size);
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(new Data { entries = list })); }
        catch (Exception e) { Debug.LogWarning("Leaderboard not saved: " + e.Message); }
        return rank <= Size ? rank : 0;
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
