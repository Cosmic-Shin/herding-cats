using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public class XPCollectorManager : MonoBehaviour
{
    public static XPCollectorManager Instance { get; private set; }

    private readonly List<XPGem> activeGems = new List<XPGem>();
    private PlayerStats playerStats;
    private Transform playerTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        playerStats = GetComponent<PlayerStats>();
        playerTransform = transform;
    }

    private void Update()
    {
        if (playerTransform == null || playerStats == null || activeGems.Count == 0)
            return;

        if ((mergeTimer -= Time.deltaTime) <= 0f) { mergeTimer = MergeEvery; MergeNearby(); }

        for (int i = activeGems.Count - 1; i >= 0; i--)
        {
            XPGem gem = activeGems[i];
            if (gem == null)
            {
                activeGems.RemoveAt(i);
                continue;
            }

            gem.HandleCollection(playerTransform, playerStats);
        }
    }

    public int Count => activeGems.Count;

    private const float MergeEvery = 0.5f, MergeRadius = 0.55f;
    private float mergeTimer;
    private readonly HashSet<XPGem> merged = new HashSet<XPGem>();

    // Idle gems lying close together fuse into one bigger gem (same total XP), so big waves don't carpet the ground.
    // ponytail: O(n^2) scan every 0.5s, fine at the 250-gem field cap; use a grid if that cap goes way up.
    private void MergeNearby()
    {
        float r2 = MergeRadius * MergeRadius;
        merged.Clear();
        for (int i = 0; i < activeGems.Count; i++)
        {
            XPGem a = activeGems[i];
            if (a == null || !a.IsIdle || merged.Contains(a)) continue;
            Vector2 pa = a.transform.position;
            for (int j = i + 1; j < activeGems.Count; j++)
            {
                XPGem b = activeGems[j];
                if (b == null || !b.IsIdle || merged.Contains(b)) continue;
                if ((pa - (Vector2)b.transform.position).sqrMagnitude > r2) continue;
                a.SetValue(a.expValue + b.expValue);
                merged.Add(b);
            }
        }
        foreach (XPGem g in merged) Pool.Despawn(g.gameObject); // despawn unregisters, so it happens after the scan
    }

    public void MagnetizeNear(Vector2 pos, float radius)
    {
        float r2 = radius * radius;
        foreach (XPGem gem in activeGems)
            if (gem != null && ((Vector2)gem.transform.position - pos).sqrMagnitude <= r2) gem.Magnetize();
    }

    public XPGem NearestIdle(Vector2 pos)
    {
        XPGem best = null;
        float bestDist = float.MaxValue;
        foreach (XPGem gem in activeGems)
        {
            if (gem == null || !gem.IsIdle) continue;
            float d = ((Vector2)gem.transform.position - pos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = gem; }
        }
        return best;
    }

    public void Register(XPGem gem)
    {
        if (gem == null || activeGems.Contains(gem))
            return;

        activeGems.Add(gem);
    }

    public void Unregister(XPGem gem)
    {
        if (gem == null)
            return;

        activeGems.Remove(gem);
    }
}
