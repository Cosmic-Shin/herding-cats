using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DamageStats
{
    public const string Bombs = "Bombs";

    public static string Source;
    private static readonly Dictionary<string, float> totals = new Dictionary<string, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        totals.Clear();
        Source = null;
    }

    public static void Add(float amount)
    {
        string key = Source ?? "Other";
        totals.TryGetValue(key, out float v);
        totals[key] = v + amount;
    }

    public static float Total => totals.Values.Sum();
    public static float Of(string source) => totals.TryGetValue(source, out float v) ? v : 0f;
    public static List<KeyValuePair<string, float>> Top(int n) => totals.OrderByDescending(kv => kv.Value).Take(n).ToList();

    public static string Short(float v) => v >= 1e6f ? $"{v / 1e6f:0.0}M" : v >= 1e4f ? $"{v / 1e3f:0.0}K" : Mathf.RoundToInt(v).ToString();
}
