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

    public void MagnetizeNear(Vector2 pos, float radius)
    {
        float r2 = radius * radius;
        foreach (XPGem gem in activeGems)
            if (gem != null && ((Vector2)gem.transform.position - pos).sqrMagnitude <= r2) gem.Magnetize();
    }

    // Closest gem that's just sitting there (not flying to the player or mid-burst).
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
