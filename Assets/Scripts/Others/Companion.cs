using System.Collections.Generic;
using UnityEngine;

public class Companion : MonoBehaviour
{
    public const int MaxLevel = 5;

    public static readonly string[] LevelText =
    {
        "A gold cat buddy throws bombs at 25% of your damage",
        "Buddy damage 25% -> 30%",
        "Buddy throws 2 bombs at once, 35% damage",
        "Buddy throws 15% faster, 40% damage",
        "Buddy deals 50% damage and throws 30% faster",
    };

    private static readonly float[] DamagePercent = { 0.25f, 0.3f, 0.35f, 0.4f, 0.5f };
    private static readonly int[] BombsPerThrow = { 1, 1, 2, 2, 2 };
    private static readonly float[] ThrowRate = { 1f, 1f, 1f, 1.15f, 1.3f };

    public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
    private const float OrbitRadius = 1.4f;
    private const float AttackRange = 6f;

    public static Companion Instance { get; private set; }
    public int Level { get; private set; } = 1;

    private Sprite[] walk, idle;
    private SpriteRenderer sr;
    private Transform player;
    private PlayerStats stats;
    private PlayerAutoAttack attack;
    private PlayerUltimate ult;
    private float orbitAngle;
    private Vector2 velocity;
    private float animTime;
    private float throwTimer;

    private static readonly List<Collider2D> hits = new List<Collider2D>(64);

    public static Companion Join(Vector2 at, Sprite[] walk, Sprite[] idle, GameObject player)
    {
        var go = new GameObject("CatBuddy");
        go.transform.position = at;
        var c = go.AddComponent<Companion>();
        c.sr = go.AddComponent<SpriteRenderer>();
        c.sr.color = Gold;
        c.sr.sortingOrder = 9;
        c.walk = walk;
        c.idle = idle;
        c.player = player.transform;
        c.stats = player.GetComponent<PlayerStats>();
        c.attack = player.GetComponent<PlayerAutoAttack>();
        c.ult = player.GetComponent<PlayerUltimate>();
        c.AddFriendMarkers();
        Instance = c;

        ExplosionFx.Spawn(at, 1.2f, Gold);
        DamagePopup.SpawnText(at + Vector2.up * 0.5f, "CAT BUDDY JOINED!", Gold, 5f);
        return c;
    }

    public void LevelUp() => Level = Mathf.Min(Level + 1, MaxLevel);

    private SpriteRenderer heart, ring;

    private void AddFriendMarkers()
    {
        heart = new GameObject("Heart").AddComponent<SpriteRenderer>();
        heart.transform.SetParent(transform, false);
        heart.sprite = Shapes.Heart;
        heart.sortingOrder = 75;
        ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
        ring.transform.SetParent(transform, false);
        ring.sprite = Shapes.Ring;
        ring.sortingOrder = 3;
        Lighting.Glow(ring);
    }

    private void UpdateFriendMarkers()
    {
        float t = Time.time;
        float top = sr.sprite != null ? sr.bounds.max.y - transform.position.y : 0.5f;
        heart.transform.localPosition = new Vector3(0f, top + 0.12f + 0.05f * Mathf.Sin(t * 4f), 0f);
        heart.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 8f));
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
        ring.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        ring.transform.localScale = new Vector3(0.8f, 0.4f, 1f) * (1f + 0.06f * pulse);
        ring.color = new Color(Gold.r, Gold.g, Gold.b, 0.4f + 0.25f * pulse);
    }

    private void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;

        orbitAngle += 60f * dt * Mathf.Deg2Rad;
        Vector2 target = (Vector2)player.position + new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle)) * OrbitRadius;
        transform.position = Vector2.SmoothDamp(transform.position, target, ref velocity, 0.3f, 12f);

        if (Mathf.Abs(velocity.x) > 0.1f) sr.flipX = velocity.x < 0f;
        animTime += dt;
        Sprite[] frames = velocity.sqrMagnitude > 0.25f ? walk : idle;
        if (frames != null && frames.Length > 0) sr.sprite = frames[(int)(animTime * 8f) % frames.Length];
        UpdateFriendMarkers();

        if (stats.currentHealth > 0f) TryThrow(dt);
    }

    private void TryThrow(float dt)
    {
        int l = Level - 1;
        throwTimer += dt;
        if (throwTimer < 1f / (stats.AttackSpeed * ThrowRate[l])) return;

        Transform target = NearestEnemy();
        if (target == null) return;
        throwTimer = 0f;

        Vector2 from = transform.position;
        Vector2 dir = ((Vector2)target.position - from).normalized;
        float damage = stats.AttackDamage * DamagePercent[l] * (ult != null ? ult.DamageMultiplier : 1f);

        for (int i = 0; i < BombsPerThrow[l]; i++)
        {
            DamageStats.Source = "Cat Buddy";
            GameObject go = Pool.Spawn(attack.bombPrefab, from, Quaternion.identity);
            go.transform.localScale *= 0.75f;
            if (go.TryGetComponent(out Bomb bomb))
            {
                Vector2 d = Quaternion.Euler(0f, 0f, (i - (BombsPerThrow[l] - 1) / 2f) * 20f) * dir;
                bomb.Initialize(target, d, damage, stats.BombRadius * 0.8f, stats.critChance, stats.critDamage);
            }
        }
    }

    private Transform NearestEnemy()
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        Vector2 from = transform.position;
        foreach (Collider2D c in Bomb.OverlapEnemies(from, AttackRange, hits))
        {
            float d = ((Vector2)c.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c.transform; }
        }
        return best;
    }
}
