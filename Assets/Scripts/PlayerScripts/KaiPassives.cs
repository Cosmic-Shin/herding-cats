using System.Collections.Generic;
using UnityEngine;

// Kai's passives. Heavy Blade and Execute are read by KaiSword on every sword hit; Dash Slash runs here.
[RequireComponent(typeof(PlayerStats))]
public class KaiPassives : HeroPassives
{
    public static readonly Passive[] All =
    {
        new Passive { id = "heavy", name = "Heavy Blade", levels = new[] {
            "Stuns last 0.5s",
            "Stuns last 0.7s",
            "Stunned or slowed cats take 40% more sword damage" } },
        new Passive { id = "execute", name = "Execute", levels = new[] {
            "Executes cats under 20% HP",
            "Executes cats under 25% HP",
            "Each execute heals 1 HP" } },
        new Passive { id = "dashslash", name = "Dash Slash", levels = new[] {
            "Dash cuts deal 150% damage",
            "Dash cuts deal 200% and reach further",
            "A kill during a dash resets the dash" } },
    };

    protected override Passive[] Passives => All;

    public static KaiPassives Active => Instance as KaiPassives;

    // Read by the sword.
    public float StunTime { get; private set; } = 0.3f;
    public float ImpairedBonus { get; private set; } = 1f;
    public float ExecuteBelow { get; private set; } = 0.15f;
    public float ExecuteHeal { get; private set; }

    private float dashDamage = 1f, dashRadius = 0.9f;
    private bool dashResets;

    private static readonly List<Collider2D> hits = new List<Collider2D>(64);
    private readonly HashSet<EnemyHealth> cut = new HashSet<EnemyHealth>();
    private PlayerStats stats;
    private PlayerController controller;
    private KaiSword sword;
    private bool wasDashing;
    private TrailRenderer dashTrail; // a white wind streak while dash-slashing

    protected override void Awake()
    {
        base.Awake();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        dashTrail = HeroFx.Trail(transform, new Vector3(0f, 0.1f, 0f), 0.5f, new Color(0.85f, 0.9f, 1f), 0.2f, 69);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (dashTrail != null) dashTrail.emitting = false;
    }

    protected override void Apply(string id, int level)
    {
        switch (id)
        {
            case "heavy":
                StunTime = new[] { 0.3f, 0.5f, 0.7f, 0.7f }[level - 1];
                ImpairedBonus = level >= 4 ? 1.4f : 1f;
                break;
            case "execute":
                ExecuteBelow = new[] { 0.15f, 0.2f, 0.25f, 0.25f }[level - 1];
                ExecuteHeal = level >= 4 ? 1f : 0f;
                break;
            case "dashslash":
                dashDamage = new[] { 1f, 1.5f, 2f, 2f }[level - 1];
                dashRadius = level >= 3 ? 1.3f : 0.9f;
                dashResets = level >= 4;
                break;
        }
    }

    private void Update()
    {
        if (controller == null) return;
        bool dashing = controller.IsDashing;
        if (dashing && !wasDashing) { cut.Clear(); dashTrail.Clear(); }
        dashTrail.emitting = dashing;
        if (dashing) Cut(transform.position);
        wasDashing = dashing;
    }

    // Blink replaces the dash, so it cuts along the jump instead.
    public void BlinkCut(Vector2 from, Vector2 to)
    {
        if (!enabled) return;
        cut.Clear();
        float length = Vector2.Distance(from, to);
        for (float d = 0f; d <= length; d += 0.5f) Cut(Vector2.Lerp(from, to, length > 0f ? d / length : 0f));
    }

    private void Cut(Vector2 at)
    {
        if (sword == null) sword = GetComponent<KaiSword>();
        if (sword == null) return;
        bool killed = false;
        foreach (Collider2D c in Bomb.OverlapEnemies(at, dashRadius * stats.areaMultiplier, hits))
        {
            if (!c.TryGetComponent(out EnemyHealth e) || e.IsDead || !cut.Add(e)) continue;
            DamageStats.Source = "Dash Slash";
            sword.Hit(e, at, sword.SwingDamage * dashDamage, Vector2.zero, 0f);
            killed |= e.IsDead;
            Juice.SparksDir(e.transform.position, controller.DashDirection, 40f, Color.white, 5, 7f, 0.1f);
        }
        if (killed && dashResets) controller.ResetDashCooldown();
    }
}
