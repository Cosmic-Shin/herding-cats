using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public class MiraPassives : HeroPassives
{
    // The active Mira, or null when another hero is picked.
    public static MiraPassives Active => Instance as MiraPassives;

    [Header("Blade Dancer")]
    public float slashCooldown = 3f;
    public float slashTriggerRange = 1.8f;
    public float slashRange = 2.4f;
    public float slashArc = 140f;
    public float slashDamage = 1.5f;
    public float slashPush = 9f;

    [Header("Weak Spot")]
    public bool weakSpot = true;

    [Header("Bubble Shield")]
    public float shieldCooldown = 20f;

    private PlayerStats stats;
    private PlayerAnimator anim;
    private PlayerUltimate ult;
    private float slashTimer;
    private float shieldTimer, shieldPop;
    private SpriteRenderer shieldFill, shieldEdge, slashFx;
    private float slashFxTime = -1f;
    private static readonly System.Collections.Generic.List<Collider2D> hits = new System.Collections.Generic.List<Collider2D>(64);

    public bool ShieldUp => shieldTimer <= 0f;
    public void RechargeShield() => shieldTimer = 0f;

    public static readonly Passive[] All =
    {
        new Passive { id = "blade", name = "Blade Dancer", levels = new[] {
            "Slash every 2.5s for 200% damage",
            "Wider slash (200°) with 25% more reach",
            "Slash every 1.8s for 300% damage, and behind you too" } },
        new Passive { id = "weakspot", name = "Weak Spot", levels = new[] {
            "Crits on slowed or stunned cats deal +25% more",
            "+50% more, and Blade Dancer slows what it hits",
            "+80% more" } },
        new Passive { id = "shield", name = "Bubble Shield", levels = new[] {
            "Recharges in 15s",
            "Recharges in 11s; popping it shoves cats away",
            "Recharges in 8s; the pop blasts for 300% damage" } },
    };

    private float weakSpotBonus = 1f;
    private bool backSlash, slashSlows;
    private float baseSlashRange;

    protected override Passive[] Passives => All;

    protected override void Apply(string id, int level)
    {
        switch (id)
        {
            case "blade":
                slashCooldown = level >= 4 ? 1.8f : level >= 2 ? 2.5f : 3f;
                slashDamage = level >= 4 ? 3f : level >= 2 ? 2f : 1.5f;
                slashArc = level >= 3 ? 200f : 140f;
                slashRange = baseSlashRange * (level >= 3 ? 1.25f : 1f);
                backSlash = level >= 4;
                break;
            case "weakspot":
                weakSpotBonus = new[] { 1f, 1.25f, 1.5f, 1.8f }[level - 1];
                slashSlows = level >= 3;
                break;
            case "shield":
                shieldCooldown = new[] { 20f, 15f, 11f, 8f }[level - 1];
                break;
        }
    }

    public static float CritBonus(EnemyHealth enemy) => AutoCrit(enemy) ? Active.weakSpotBonus : 1f;

    protected override void Awake()
    {
        base.Awake();
        stats = GetComponent<PlayerStats>();
        anim = GetComponent<PlayerAnimator>();
        ult = GetComponent<PlayerUltimate>();
        shieldFill = MakeFx("Shield", Shapes.Bubble, 6);
        shieldEdge = MakeFx("ShieldCharge", Shapes.Ring, 7);
        slashFx = MakeFx("Slash", Shapes.Crescent, 30);
        slashFx.gameObject.SetActive(false);
        baseSlashRange = slashRange;
    }

    private SpriteRenderer MakeFx(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(sr);
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    public static bool AutoCrit(EnemyHealth enemy) => Active != null && Active.weakSpot && enemy.IsImpaired;

    public bool TryBlock()
    {
        if (!ShieldUp || !enabled) return false;
        shieldTimer = shieldCooldown;
        DamagePopup.SpawnText((Vector2)transform.position + Vector2.up * 0.6f, "BLOCKED!", new Color(0.55f, 0.85f, 1f), 4.5f);
        int shield = LevelOf("shield");
        if (shield >= 3)
        {
            Vector2 pos = transform.position;
            DamageStats.Source = "Bubble Shield";
            foreach (Collider2D c in Bomb.OverlapEnemies(pos, 2.8f * stats.areaMultiplier, hits))
            {
                if (shield >= 4 && c.TryGetComponent(out EnemyHealth e) && !e.IsDead) e.TakeDamage(stats.AttackDamage * 3f, pos, false);
                if (c.TryGetComponent(out IEnemyBrain brain)) brain.Knockback(((Vector2)c.transform.position - pos).normalized * 12f);
            }
            ExplosionFx.Ring(pos, 2.8f * stats.areaMultiplier, new Color(0.6f, 0.9f, 1f), 0.3f);
        }
        // Pop: the bubble bursts into droplets.
        Vector2 at = transform.position;
        ExplosionFx.Ring(at, 1.3f, new Color(0.7f, 0.95f, 1f), 0.25f);
        ExplosionFx.Spawn(at, 1.2f, new Color(0.55f, 0.85f, 1f));
        Juice.Sparks(at, new Color(0.6f, 0.9f, 1f), 16, 9f, 0.14f);
        Sfx.Play(Sfx.Id.Pop, 0.6f, 0.7f);
        Sfx.Play(Sfx.Id.Shatter, 0.25f, 1.4f);
        CameraFollow.Shake(0.08f, 0.1f);
        return true;
    }

    private void Update()
    {
        UpdateShield();
        UpdateSlashFx();

        slashTimer -= Time.deltaTime;
        if (slashTimer > 0f) return;

        Transform near = NearestCat(slashTriggerRange);
        if (near == null) return;
        Vector2 dir = ((Vector2)near.position - (Vector2)transform.position).normalized;
        if (backSlash) Slash(-dir);
        Slash(dir);
        slashTimer = slashCooldown / Mathf.Max(0.1f, stats.attackSpeedMultiplier);
    }

    private void UpdateShield()
    {
        bool wasDown = shieldTimer > 0f;
        if (wasDown) shieldTimer -= Time.deltaTime;
        if (wasDown && ShieldUp)
        {
            shieldPop = 1f;
            ExplosionFx.Ring(transform.position, 1.1f, new Color(0.7f, 0.95f, 1f), 0.3f);
            Juice.Sparks(transform.position, new Color(0.75f, 0.95f, 1f), 8, 4f, 0.1f);
            DamagePopup.SpawnText((Vector2)transform.position + Vector2.up * 0.6f, "SHIELD UP", new Color(0.55f, 0.85f, 1f), 3.5f);
            Sfx.Play(Sfx.Id.Ready, 0.4f, 1.2f);
        }

        float k = ShieldUp ? 1f : 1f - shieldTimer / shieldCooldown;
        shieldPop = Mathf.Max(0f, shieldPop - Time.deltaTime * 3f);
        // Shield up: wobble + slow hue shift, pops in when it comes back. Shield down: ring fills as it recharges.
        float wob = 0.035f * Mathf.Sin(Time.time * 5f);
        float spring = 1f + 0.35f * shieldPop * Mathf.Sin(shieldPop * Mathf.PI * 3f);
        shieldFill.transform.localScale = new Vector3(1f + wob, 1f - wob, 1f) * 2f * spring;
        Color tint = Color.Lerp(new Color(0.55f, 0.9f, 1f), new Color(0.85f, 0.7f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 1.3f));
        shieldFill.color = new Color(tint.r, tint.g, tint.b, ShieldUp ? 0.55f : 0f);
        shieldEdge.transform.localScale = Vector3.one * 2f * Mathf.Lerp(0.4f, 1f, k);
        shieldEdge.color = new Color(0.55f, 0.85f, 1f, ShieldUp ? 0f : 0.25f * k);
    }

    private Transform NearestCat(float range)
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (Collider2D c in Bomb.OverlapEnemies(transform.position, range, hits))
        {
            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c.transform; }
        }
        return best;
    }

    private void Slash(Vector2 dir)
    {
        Vector2 pos = transform.position;
        float damage = stats.AttackDamage * slashDamage * (ult != null ? ult.DamageMultiplier : 1f);
        float range = slashRange * stats.areaMultiplier;

        DamageStats.Source = "Blade Dancer";
        foreach (Collider2D c in Bomb.OverlapEnemies(pos, range, hits))
        {
            Vector2 to = (Vector2)c.transform.position - pos;
            if (Vector2.Angle(dir, to) > slashArc / 2f) continue;
            if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead)
            {
                bool crit = Random.value < stats.critChance || AutoCrit(e);
                e.TakeDamage(damage * (crit ? stats.critDamage * CritBonus(e) : 1f) * Random.Range(0.9f, 1.1f), pos, crit);
            }
            if (c.TryGetComponent(out IEnemyBrain brain))
            {
                brain.Knockback(to.normalized * slashPush);
                if (slashSlows) brain.Slow(0.5f, 1.5f);
            }
        }

        if (anim != null)
        {
            anim.PlayThrow(dir);
            anim.Squash(new Vector2(1.2f, 0.85f), 0.15f);
        }
        slashFx.transform.localPosition = dir * range * 0.45f;
        slashFx.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 211f);
        slashFx.transform.localScale = Vector3.one * range * 1.1f;
        slashFx.gameObject.SetActive(true);
        slashFxTime = 0f;
        CameraFollow.Shake(0.04f, 0.06f);
    }

    private void UpdateSlashFx()
    {
        if (slashFxTime < 0f) return;
        slashFxTime += Time.deltaTime;
        float k = slashFxTime / 0.18f;
        slashFx.color = new Color(1f, 1f, 1f, 0.85f * (1f - k));
        if (k >= 1f) { slashFxTime = -1f; slashFx.gameObject.SetActive(false); }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (shieldEdge != null) shieldEdge.gameObject.SetActive(true);
        if (shieldFill != null) shieldFill.gameObject.SetActive(true);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (shieldEdge != null) shieldEdge.gameObject.SetActive(false);
        if (shieldFill != null) shieldFill.gameObject.SetActive(false);
        if (slashFx != null) slashFx.gameObject.SetActive(false);
    }
}
