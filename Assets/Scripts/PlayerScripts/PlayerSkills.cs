using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSkills : MonoBehaviour
{
    public const int MaxSkills = 6;

    public class Skill
    {
        public string id;
        public string name;
        public string[] levels;
    }

    public static readonly Skill[] All =
    {
        new Skill { id = "blink", name = "Blink", levels = new[] {
            "Dash becomes a teleport to a safe spot nearby (4s cooldown)",
            "Blink cooldown 4s -> 3s, 0.6s invulnerability after landing",
            "Leave a bomb blast where you blinked from (200% damage)" } },
        new Skill { id = "chain", name = "Chain Reaction", levels = new[] {
            "Explosions have a 15% chance to set off a second blast (50% damage)",
            "25% chance, second blast deals 60% damage",
            "35% chance, second blast deals 75% damage" } },
        new Skill { id = "cluster", name = "Cluster Bomb", levels = new[] {
            "Explosions scatter 2 mini bombs (25% damage)",
            "3 mini bombs (30% damage)",
            "4 mini bombs (35% damage)" } },
        new Skill { id = "napalm", name = "Napalm", levels = new[] {
            "Explosions leave fire for 2s (8% damage every 0.5s)",
            "Fire lasts 2.5s, 12% damage per tick",
            "Fire lasts 3s, 16% damage per tick" } },
        new Skill { id = "sharpshooter", name = "Sharpshooter", levels = new[] {
            "+5% crit chance, crits deal 1.75x",
            "+5% crit chance, crits deal 2x",
            "+5% crit chance, crits deal 2.25x" } },
        new Skill { id = "berserk", name = "Berserk", levels = new[] {
            "Up to +30% damage the lower your HP",
            "Up to +45% damage at low HP",
            "Up to +60% damage at low HP" } },
        new Skill { id = "thickskin", name = "Thick Skin", levels = new[] {
            "Take 10% less damage",
            "Take 15% less damage",
            "Take 20% less damage" } },
        new Skill { id = "regen", name = "Regeneration", levels = new[] {
            "Regenerate 0.3 HP per second",
            "Regenerate 0.6 HP per second",
            "Regenerate 1 HP per second" } },
        new Skill { id = "dodge", name = "Dodge", levels = new[] {
            "7% chance to dodge a hit completely",
            "12% dodge chance",
            "18% dodge chance" } },
        new Skill { id = "shockwave", name = "Shockwave", levels = new[] {
            "When hit, push nearby enemies away and stun them for 0.5s",
            "Bigger shockwave, 0.8s stun",
            "Shockwave also deals 100% damage, 1s stun" } },
        new Skill { id = "secondwind", name = "Second Wind", levels = new[] {
            "Once per run, survive a lethal hit and revive at 30% HP",
            "Revive at 50% HP",
            "Revive at 70% HP with a huge stunning blast" } },
        new Skill { id = "magnet", name = "Magnet", levels = new[] {
            "+30% pickup range",
            "+60% pickup range",
            "+100% pickup range" } },
        new Skill { id = "scholar", name = "Scholar", levels = new[] {
            "+10% XP gained",
            "+20% XP gained",
            "+30% XP gained" } },
        new Skill { id = "gourmet", name = "Gourmet", levels = new[] {
            "Burgers heal 25% more and drop 1.5x as often",
            "Burgers heal 50% more and drop 2x as often",
            "Burgers heal 75% more and drop 2.5x as often" } },
        new Skill { id = "overdrive", name = "Overdrive", levels = new[] {
            "Ultimate cooldown -15%, lasts +2s",
            "Ultimate cooldown -25%, lasts +3s",
            "Ultimate cooldown -35%, lasts +5s" } },
        new Skill { id = "blacksmith", name = "Blacksmith", levels = new[] {
            "Anvils drop 1.75x as often from cats, and crates can drop them (4%)",
            "Anvils drop 2.5x as often, 7.5% from crates",
            "Each anvil can be used twice" } },
    };

    public static PlayerSkills Instance { get; private set; }

    private readonly Dictionary<string, int> owned = new Dictionary<string, int>();
    private readonly List<string> order = new List<string>();
    private static readonly List<Collider2D> scratch = new List<Collider2D>(64);

    private PlayerStats stats;
    private PlayerController controller;
    private PlayerAutoAttack attack;
    private PlayerUltimate ult;

    private float basePickupRange, baseUltCooldown, baseUltDuration;
    private float regenPerSecond;
    private bool reviveUsed;
    private float regenFx;

    public IReadOnlyList<string> OwnedInOrder => order;
    public bool ReviveUsed => reviveUsed;

    private void Awake()
    {
        Instance = this;
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        attack = GetComponent<PlayerAutoAttack>();
        ult = GetComponent<PlayerUltimate>();
    }

    private void Start()
    {
        basePickupRange = stats.pickupRange;
        if (ult != null) { baseUltCooldown = ult.cooldown; baseUltDuration = ult.duration; }
    }

    public static Skill Find(string id) => System.Array.Find(All, s => s.id == id);

    public int LevelOf(string id) => owned.TryGetValue(id, out int level) ? level : 0;

    public List<Skill> Offerable()
    {
        var result = new List<Skill>();
        foreach (Skill s in All)
        {
            int level = LevelOf(s.id);
            if (s.id == "secondwind" && reviveUsed) continue;
            if (level == 0 && Achievements.IsLocked("skill:" + s.id)) continue;
            if (level > 0 ? level < s.levels.Length : order.Count < MaxSkills) result.Add(s);
        }
        return result;
    }

    public void LevelUp(Skill skill)
    {
        Achievements.MarkTried("s", skill.id);
        int level = LevelOf(skill.id) + 1;
        if (level == 1) order.Add(skill.id);
        owned[skill.id] = level;
        Apply(skill.id, level);
        if (level == skill.levels.Length) PlayerCollabs.MaxHint(skill.id, transform.position + Vector3.up * 0.8f);
    }

    private void Apply(string id, int level)
    {
        int i = level - 1;
        switch (id)
        {
            case "blink": controller.blinkLevel = level; break;
            case "sharpshooter": stats.critChance += 0.05f; stats.critDamage = 1.5f + 0.25f * level; break;
            case "berserk": stats.lowHealthDamageBonus = new[] { 0.3f, 0.45f, 0.6f }[i]; break;
            case "thickskin": stats.damageReduction = new[] { 0.1f, 0.15f, 0.2f }[i]; break;
            case "regen": regenPerSecond = new[] { 0.3f, 0.6f, 1f }[i]; break;
            case "dodge": stats.dodgeChance = new[] { 0.07f, 0.12f, 0.18f }[i]; break;
            case "magnet": stats.pickupRange = basePickupRange * new[] { 1.3f, 1.6f, 2f }[i]; break;
            case "scholar": stats.expMultiplier = 1f + 0.1f * level; break;
            case "gourmet":
                stats.burgerHealMultiplier = 1f + 0.25f * level;
                Burger.DropChanceMultiplier = 1f + 0.5f * level;
                break;
            case "overdrive":
                if (ult == null) break;
                ult.cooldown = baseUltCooldown * (1f - new[] { 0.15f, 0.25f, 0.35f }[i]);
                ult.duration = baseUltDuration + new[] { 2f, 3f, 5f }[i];
                break;
            case "blacksmith": Pickup.AnvilChanceMultiplier = level >= 2 ? 2.5f : 1.75f; break;
        }
    }

    public void Absorb(string id) => order.Remove(id);
    public void Unabsorb(string id) { if (owned.ContainsKey(id) && !order.Contains(id)) order.Add(id); }

    public void Remove(string id)
    {
        int level = LevelOf(id);
        if (level == 0) return;
        owned.Remove(id);
        order.Remove(id);
        switch (id)
        {
            case "blink": controller.blinkLevel = 0; break;
            case "sharpshooter": stats.critChance -= 0.05f * level; stats.critDamage = 1.5f; break;
            case "berserk": stats.lowHealthDamageBonus = 0f; break;
            case "thickskin": stats.damageReduction = 0f; break;
            case "regen": regenPerSecond = 0f; break;
            case "dodge": stats.dodgeChance = 0f; break;
            case "magnet": stats.pickupRange = basePickupRange; break;
            case "scholar": stats.expMultiplier = 1f; break;
            case "gourmet": stats.burgerHealMultiplier = 1f; Burger.DropChanceMultiplier = 1f; break;
            case "overdrive": if (ult != null) { ult.cooldown = baseUltCooldown; ult.duration = baseUltDuration; } break;
            case "blacksmith": Pickup.AnvilChanceMultiplier = 1f; break;
        }
    }

    private void Update()
    {
        if (regenPerSecond > 0f && stats.currentHealth > 0f && stats.currentHealth < stats.maxHealth)
        {
            stats.Heal(regenPerSecond * Time.deltaTime);
            if ((regenFx -= Time.deltaTime) <= 0f) { regenFx = 0.45f; HealPlus(); }
        }

        if (stats.lowHealthDamageBonus > 0f && stats.currentHealth > 0f && stats.currentHealth < stats.maxHealth * 0.5f
            && !Settings.LowParticles && Random.value < Time.deltaTime * 20f * (1f - stats.currentHealth / stats.maxHealth))
            Juice.SparksDir((Vector2)transform.position + Random.insideUnitCircle * 0.4f, Vector2.up, 25f, new Color(1f, 0.25f, 0.2f), 2, 2.4f, 0.15f);
    }

    private void HealPlus()
    {
        const float life = 0.7f;
        Shot s = Shot.Spawn(Shapes.Plus, new Color(0.45f, 1f, 0.5f), 0.36f, (Vector2)transform.position + new Vector2(Random.Range(-0.4f, 0.4f), 0.3f), 30);
        s.velocity = Vector2.up * 1.2f;
        s.life = life;
        s.onUpdate = shot => shot.Renderer.color = new Color(0.45f, 1f, 0.5f, 1f - shot.age / life);
    }

    public void OnExplosion(Vector2 pos, float radius, float damage)
    {
        if (!Hero.UsesBombs) return;
        int chain = LevelOf("chain");
        if (chain > 0 && Random.value < new[] { 0.15f, 0.25f, 0.35f }[chain - 1]) Chain(pos, radius, damage);

        int cluster = LevelOf("cluster");
        if (cluster > 0) ScatterMinis(pos, cluster + 1, damage * new[] { 0.25f, 0.3f, 0.35f }[cluster - 1], radius * 0.5f);

        int napalm = LevelOf("napalm");
        if (napalm > 0)
        {
            bool firebomb = PlayerCollabs.Has("firebomb");
            BurningGround.Spawn(pos, radius * (firebomb ? 1.2f : 0.8f),
                                firebomb ? 5f : new[] { 2f, 2.5f, 3f }[napalm - 1],
                                damage * (firebomb ? 0.22f : new[] { 0.08f, 0.12f, 0.16f }[napalm - 1]));
        }
    }

    public void ScatterMinis(Vector2 pos, int count, float damage, float radius)
    {
        if (attack == null || attack.bombPrefab == null) return;
        float offset = Random.value * Mathf.PI * 2f;
        for (int n = 0; n < count; n++)
        {
            float a = offset + n * Mathf.PI * 2f / count;
            GameObject go = Pool.Spawn(attack.bombPrefab, pos, Quaternion.identity);
            go.transform.localScale *= 0.6f;
            if (!go.TryGetComponent(out Bomb mini)) continue;
            mini.Initialize(null, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), damage, radius, stats.critChance, stats.critDamage);
            mini.isSecondary = true;
            mini.maxFlightTime = 0.12f;
            mini.fuseTime = 0.25f;
        }
    }

    public void Chain(Vector2 pos, float radius, float damage)
    {
        int chain = Mathf.Max(1, LevelOf("chain"));
        List<Collider2D> near = Bomb.OverlapEnemies(pos, radius * 2.5f, scratch);
        Vector2 at = near.Count > 0 ? (Vector2)near[Random.Range(0, near.Count)].transform.position
                                     : pos + Random.insideUnitCircle.normalized * radius;
        float chainDamage = damage * new[] { 0.5f, 0.6f, 0.75f }[chain - 1];
        string source = DamageStats.Source;
        StartCoroutine(Delayed(0.12f, () => { DamageStats.Source = source; Bomb.Blast(at, radius * 0.75f, chainDamage, stats.critChance, stats.critDamage, true); }));
    }

    public void OnPlayerHit()
    {
        int level = LevelOf("shockwave");
        if (level == 0) return;

        int i = level - 1;
        Shockwave(new[] { 2f, 2.5f, 3f }[i], new[] { 0.5f, 0.8f, 1f }[i], level >= 3 ? stats.AttackDamage : 0f);
    }

    public bool TryRevive()
    {
        int level = LevelOf("secondwind");
        if (level == 0 || reviveUsed) return false;

        reviveUsed = true;
        stats.currentHealth = stats.maxHealth * new[] { 0.3f, 0.5f, 0.7f }[level - 1];
        stats.Heal(0f);
        DamagePopup.SpawnText(transform.position + Vector3.up * 0.5f, "SECOND WIND!", new Color(0.5f, 1f, 0.6f), 6f);
        Color life = new Color(0.45f, 1f, 0.55f);
        Juice.Flash(new Color(0.45f, 1f, 0.55f, 0.35f), 0.4f);
        Lighting.Flash(transform.position, 6f, life, 0.5f);
        ExplosionFx.Ring(transform.position, 3f, life, 0.5f);
        ExplosionFx.Crosses(transform.position, 1.5f, life, 8);
        for (int i = 0; i < 4; i++) HealPlus();
        Shockwave(4f, 1.5f, level >= 3 ? stats.AttackDamage * 3f : 0f);
        return true;
    }

    private void Shockwave(float radius, float stun, float damage)
    {
        DamageStats.Source = "Shockwave";
        Vector2 pos = transform.position;
        foreach (Collider2D hit in Bomb.OverlapEnemies(pos, radius, scratch))
        {
            if (damage > 0f && hit.TryGetComponent(out EnemyHealth health)) health.TakeDamage(damage, pos);
            if (hit.TryGetComponent(out IEnemyBrain brain)) brain.Knockback(((Vector2)hit.transform.position - pos).normalized * 7f);
            if (hit.TryGetComponent(out EnemyAI cat)) cat.Stun(stun);
        }
        StompFx.Spawn(pos, radius);
        ExplosionFx.Ring(pos, radius, new Color(0.5f, 0.85f, 1f), 0.3f);
        CameraFollow.Shake(0.1f, 0.15f);
    }

    private static IEnumerator Delayed(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
    }
}
