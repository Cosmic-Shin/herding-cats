using System.Collections.Generic;
using UnityEngine;

// Secret companion from the "Seen It All" achievement. A spinning meowl floats behind the player
// and every few seconds lets out a meow blast on the nearest cat.
public class Meowl : MonoBehaviour
{
    private const float Fps = 12f, BlastEvery = 2f, BlastRadius = 2.4f, Range = 8f, DamagePercent = 1.2f;
    private static readonly Vector2 Offset = new(-0.9f, 0.8f);
    private static readonly Color Tint = new(0.75f, 0.55f, 1f);
    private static readonly List<Collider2D> hits = new(64);

    public static Meowl Instance { get; private set; }

    private Sprite[] frames;
    private SpriteRenderer sr;
    private Transform player;
    private PlayerStats stats;
    private Vector2 velocity;
    private float t, blastTimer, pop;

    public static void Join(GameObject playerGo)
    {
        Sprite[] frames = Resources.LoadAll<Sprite>("Meowl");
        if (frames.Length == 0) return;
        System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

        Vector2 at = (Vector2)playerGo.transform.position + Offset;
        var go = new GameObject("Meowl");
        go.transform.position = at;
        var m = go.AddComponent<Meowl>();
        m.frames = frames;
        m.sr = go.AddComponent<SpriteRenderer>();
        m.sr.sortingOrder = 9;
        m.player = playerGo.transform;
        m.stats = playerGo.GetComponent<PlayerStats>();
        Instance = m;

        ExplosionFx.Spawn(at, 1.2f, Tint);
        DamagePopup.SpawnText(at + Vector2.up * 0.6f, "MEOWL JOINED!", Tint, 5f);
        Sfx.Play(Sfx.Id.Meow, 0.8f, 0.7f);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;
        t += dt;

        Vector2 target = (Vector2)player.position + Offset + Vector2.up * Mathf.Sin(t * 2.5f) * 0.12f;
        transform.position = Vector2.SmoothDamp(transform.position, target, ref velocity, 0.35f, 14f);
        sr.sprite = frames[(int)(t * Fps) % frames.Length];
        pop = Mathf.MoveTowards(pop, 0f, dt * 4f);
        transform.localScale = Vector3.one * (1.4f + pop);

        if (stats.currentHealth <= 0f) return;
        blastTimer += dt;
        if (blastTimer >= BlastEvery / stats.AttackSpeed) Blast();
    }

    private void Blast()
    {
        Vector2 from = transform.position;
        Transform target = null;
        float best = float.MaxValue;
        foreach (Collider2D c in Bomb.OverlapEnemies(from, Range, hits))
        {
            float d = ((Vector2)c.transform.position - from).sqrMagnitude;
            if (d < best) { best = d; target = c.transform; }
        }
        if (target == null) return;
        blastTimer = 0f;
        pop = 0.35f;

        Vector2 at = target.position;
        float damage = stats.AttackDamage * DamagePercent;
        bool crit = Random.value < stats.critChance;
        if (crit) damage *= stats.critDamage;
        DamageStats.Source = "Meowl";
        foreach (Collider2D c in Bomb.OverlapEnemies(at, BlastRadius, hits))
            if (c.TryGetComponent(out EnemyHealth h)) h.TakeDamage(damage, at, crit);

        ExplosionFx.Spawn(at, BlastRadius * 0.7f, Tint);
        ExplosionFx.Ring(at, BlastRadius, Tint, 0.35f);
        Sfx.Play(Sfx.Id.Meow, 0.5f, Random.Range(0.6f, 0.75f));
        if (Random.value < 0.25f) DamagePopup.SpawnText(at + Vector2.up * 0.5f, "MEOWL", Tint, 3f);
    }
}
