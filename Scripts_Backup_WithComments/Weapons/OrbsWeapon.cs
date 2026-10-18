using System.Collections.Generic;
using UnityEngine;

// Orbs that circle the player and hit whatever they touch (each enemy at most every 0.5s).
public class OrbsWeapon : Weapon
{
    private static readonly int[] Count = { 3, 3, 4, 4, 5, 5, 8 };
    private static readonly float[] Damage = { 7, 8, 8, 10, 10, 12, 15 };
    private static readonly float[] OrbitRadius = { 1.8f, 1.8f, 1.8f, 2.1f, 2.1f, 2.4f, 2.8f };
    private static readonly float[] SpinSpeed = { 180, 180, 200, 200, 220, 240, 280 }; // degrees / sec
    private const float HitInterval = 0.5f;
    private const float OrbHitRadius = 0.5f;

    protected readonly List<Transform> orbs = new List<Transform>();
    private readonly Dictionary<EnemyHealth, float> nextHitTime = new Dictionary<EnemyHealth, float>();
    private float angle, ghostTimer;

    protected override void OnLevelChanged()
    {
        while (orbs.Count < Count[L])
        {
            var go = new GameObject("Orb");
            var sr = go.AddComponent<SpriteRenderer>();
            Lighting.Glow(sr);
            sr.sprite = Shapes.Circle;
            sr.color = new Color(0.85f, 0.97f, 1f);
            sr.sortingOrder = 15;
            go.transform.localScale = Vector3.one * 0.5f;
            var glow = new GameObject("Glow").AddComponent<SpriteRenderer>(); // soft halo around the bright core
            Lighting.Glow(glow);
            glow.transform.SetParent(go.transform, false);
            glow.transform.localScale = Vector3.one * 1.6f;
            glow.sprite = Shapes.Circle;
            glow.color = new Color(0.5f, 0.85f, 1f, 0.18f);
            glow.sortingOrder = 14;
            orbs.Add(go.transform);
        }
    }

    protected virtual void OnDisable()
    {
        foreach (Transform orb in orbs) if (orb != null) orb.gameObject.SetActive(false);
    }

    protected virtual void OnEnable()
    {
        foreach (Transform orb in orbs) if (orb != null) orb.gameObject.SetActive(true);
    }

    protected virtual void OnDestroy()
    {
        foreach (Transform orb in orbs) if (orb != null) Destroy(orb.gameObject);
    }

    protected virtual void Update()
    {
        angle += SpinSpeed[L] * Time.deltaTime;
        float radius = Area(OrbitRadius[L]);
        float damage = Dmg(Damage[L]);
        float now = Time.time;
        bool ghostTick = !Settings.LowParticles && (ghostTimer -= Time.deltaTime) <= 0f;
        if (ghostTick) ghostTimer = 0.05f;

        for (int i = 0; i < orbs.Count; i++)
        {
            float a = (angle + i * 360f / orbs.Count) * Mathf.Deg2Rad;
            Vector2 p = Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            orbs[i].position = p;
            if (Random.value < 0.25f) Juice.Sparks(p, new Color(0.6f, 0.9f, 1f, 0.7f), 1, 0.3f, 0.12f); // sparkle trail
            if (ghostTick && orbs[i].TryGetComponent(out SpriteRenderer sr) && sr.enabled) // comet tail
            {
                Color c = sr.color;
                AfterImage.Spawn(sr, new Color(c.r * 0.7f, c.g * 0.9f, c.b, 0.35f));
            }

            foreach (Collider2D c in EnemiesNear(p, OrbHitRadius))
            {
                if (!c.TryGetComponent(out EnemyHealth e) || e.IsDead) continue;
                if (nextHitTime.TryGetValue(e, out float t) && now < t) continue;
                nextHitTime[e] = now + HitInterval;
                Hit(e, damage, Pos);
                if (PlayerCollabs.Has("repulsor")) // boost: hurl the cat away and stun it
                {
                    if (c.TryGetComponent(out IEnemyBrain brain)) brain.Knockback(((Vector2)c.transform.position - Pos).normalized * 12f);
                    if (c.TryGetComponent(out EnemyAI cat)) cat.Stun(0.4f);
                    Juice.Sparks(p, new Color(0.5f, 0.75f, 1f), 3, 4f, 0.12f);
                }
            }
        }

        if (nextHitTime.Count > 400) nextHitTime.Clear(); // pooled enemies get reused; keep the table small
    }
}
