using System.Collections.Generic;
using UnityEngine;

// Nyx's passives. Hack and Backdoor hook into her drone zaps (NyxDrones calls in); Mark Target runs here.
[RequireComponent(typeof(PlayerStats))]
public class NyxPassives : HeroPassives
{
    public static readonly Passive[] All =
    {
        new Passive { id = "hack", name = "Hack", levels = new[] {
            "15% hack chance",
            "20% chance, hacks last 4s",
            "Hacked cats explode when the hack ends" } },
        new Passive { id = "mark", name = "Mark Target", levels = new[] {
            "Marks 2 cats at once",
            "Marked cats take +80% damage",
            "Marked cats drop 3x XP" } },
        new Passive { id = "backdoor", name = "Backdoor", levels = new[] {
            "Every 10th zap is an exploit",
            "Every 8th zap is an exploit",
            "Exploits chain to 3 more cats" } },
    };

    protected override Passive[] Passives => All;
    public static NyxPassives Active => Instance as NyxPassives;

    public static readonly Color Magenta = new Color(1f, 0.3f, 0.85f);
    private static readonly List<Collider2D> hits = new List<Collider2D>(64);

    // Hack
    private float hackChance = 0.1f, hackTime = 3f;
    private bool hackExplodes;
    private class Hacked { public EnemyHealth e; public SpriteRenderer sr; public int life; public float until, tick, glitch; }
    private readonly List<Hacked> hacked = new List<Hacked>();

    // Mark Target
    private int marks = 1;
    private float markTimer;
    private bool markXp;
    private class Mark { public EnemyHealth e; public int life; public SpriteRenderer reticle; public float born; }
    private readonly List<Mark> marked = new List<Mark>();
    private static readonly Color MarkRed = new Color(1f, 0.25f, 0.3f);

    // Backdoor
    private int exploitEvery = 12, zapCount;
    public bool ExploitChains { get; private set; }

    private PlayerStats stats;
    private NyxDrones drones;

    protected override void Awake()
    {
        base.Awake();
        stats = GetComponent<PlayerStats>();
        EnemyHealth.MarkMultiplier = 1.5f;
        controller = GetComponent<PlayerController>();
        body = GetComponent<PlayerAnimator>().spriteRenderer;
        dashTrail = HeroFx.Trail(transform, new Vector3(0f, 0.1f, 0f), 0.32f, NyxDrones.Cyan, 0.18f, 69);
    }

    // ---- Dash: a cyan data-stream trail, RGB-split glitch ghosts and flickering bits, a burst where she lands ----
    private PlayerController controller;
    private SpriteRenderer body;
    private TrailRenderer dashTrail;
    private bool wasDashing;
    private float glitchTimer;

    private void UpdateDashFx()
    {
        bool dashing = controller.IsDashing;
        if (dashing && !wasDashing) { dashTrail.Clear(); HeroFx.Bits(transform.position, NyxDrones.Cyan, Magenta, 6, 3f); }
        dashTrail.emitting = dashing;
        if (dashing && (glitchTimer -= Time.deltaTime) <= 0f)
        {
            glitchTimer = 0.05f;
            HeroFx.Glitch(body, 0.1f);
            HeroFx.Bits(transform.position, NyxDrones.Cyan, Magenta, 2, 1.5f, 0.07f);
        }
        if (!dashing && wasDashing)
        {
            HeroFx.Bits(transform.position, NyxDrones.Cyan, Magenta, 10, 4f);
            ExplosionFx.Ring(transform.position, 0.7f, NyxDrones.Cyan, 0.18f);
        }
        wasDashing = dashing;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (dashTrail != null) dashTrail.emitting = false;
        foreach (var m in marked) if (m.reticle != null) Destroy(m.reticle.gameObject);
        marked.Clear();
        hacked.Clear();
    }

    protected override void Apply(string id, int level)
    {
        switch (id)
        {
            case "hack":
                hackChance = new[] { 0.1f, 0.15f, 0.2f, 0.2f }[level - 1];
                hackTime = level >= 3 ? 4f : 3f;
                hackExplodes = level >= 4;
                break;
            case "mark":
                marks = level >= 2 ? 2 : 1;
                EnemyHealth.MarkMultiplier = level >= 3 ? 1.8f : 1.5f;
                markXp = level >= 4;
                break;
            case "backdoor":
                exploitEvery = new[] { 12, 10, 8, 8 }[level - 1];
                ExploitChains = level >= 4;
                break;
        }
    }

    private float ZapDamage => drones != null ? drones.ZapDamage : stats.AttackDamage;

    // Backdoor: counts primary zaps; true when this one is an exploit.
    public bool NextZapIsExploit() => ++zapCount % exploitEvery == 0;

    // Hack: zapped cats sometimes stop and turn on their neighbours.
    public void TryHack(EnemyHealth e) => Hack(e, Random.value < hackChance, hackTime);

    // System Override (her ultimate): hack every cat on screen at once.
    public void HackAll(float seconds)
    {
        foreach (Collider2D c in Bomb.OverlapEnemies(transform.position, 12f, new List<Collider2D>(128)))
            if (c.TryGetComponent(out EnemyHealth e)) Hack(e, true, seconds);
    }

    private void Hack(EnemyHealth e, bool roll, float seconds)
    {
        if (!roll || e.IsDead || e.IsBoss || hacked.Exists(h => h.e == e && h.life == e.Life)) return;
        if (!e.TryGetComponent(out EnemyAI cat)) return;
        cat.Stun(seconds);
        var sr = e.GetComponentInChildren<SpriteRenderer>();
        hacked.Add(new Hacked { e = e, sr = sr, life = e.Life, until = Time.time + seconds, tick = Random.Range(0.1f, 0.5f) });
        Vector2 at = e.transform.position;
        DamagePopup.SpawnText(at + Vector2.up * 0.4f, "HACKED", Magenta, 3f);
        HeroFx.Bits(at, Magenta, NyxDrones.Cyan, 10, 4f);
        HeroFx.Glitch(sr, 0.12f);
        ExplosionFx.Ring(at, 0.6f, Magenta, 0.2f);
    }

    private void Update()
    {
        if (drones == null) drones = GetComponent<NyxDrones>();
        UpdateHacked();
        UpdateMarks();
        UpdateDashFx();
    }

    private void UpdateHacked()
    {
        for (int i = hacked.Count - 1; i >= 0; i--)
        {
            Hacked h = hacked[i];
            bool alive = h.e != null && !h.e.IsDead && h.e.Life == h.life;
            if (!alive || Time.time >= h.until)
            {
                if (alive && hackExplodes) Crash(h.e.transform.position);
                hacked.RemoveAt(i);
                continue;
            }
            Vector2 at = h.e.transform.position;
            // Glitching: RGB-split flickers and the odd stray pixel.
            if ((h.glitch -= Time.deltaTime) <= 0f) { h.glitch = Random.Range(0.08f, 0.22f); HeroFx.Glitch(h.sr, Random.Range(0.05f, 0.12f)); }
            if (Random.value < Time.deltaTime * 6f) HeroFx.Bits(at + Random.insideUnitCircle * 0.3f, Magenta, NyxDrones.Cyan, 1, 1.5f, 0.07f);
            if ((h.tick -= Time.deltaTime) > 0f) continue;
            h.tick = 0.5f;
            // Zap the nearest other cat.
            EnemyHealth victim = null;
            float best = float.MaxValue;
            foreach (Collider2D c in Bomb.OverlapEnemies(at, 2.5f, hits))
            {
                if (!c.TryGetComponent(out EnemyHealth o) || o == h.e || o.IsDead) continue;
                float d = ((Vector2)c.transform.position - at).sqrMagnitude;
                if (d < best) { best = d; victim = o; }
            }
            if (victim == null) continue;
            DamageStats.Source = "Hack";
            victim.TakeDamage(ZapDamage * 0.4f, at);
            LightningFx.SpawnBetween(at, victim.transform.position, Magenta, 0.5f);
            HeroFx.Pop(victim.transform.position, Magenta, 0.5f);
        }
    }

    // A hacked cat crashing: small glitch explosion.
    private void Crash(Vector2 at)
    {
        float r = 1.5f * stats.areaMultiplier;
        DamageStats.Source = "Hack";
        foreach (Collider2D c in Bomb.OverlapEnemies(at, r, hits))
            if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead) e.TakeDamage(ZapDamage * 1.5f, at);
        HeroFx.Pop(at, Magenta, r);
        ExplosionFx.Ring(at, r, NyxDrones.Cyan, 0.25f);
        ExplosionFx.Ring(at, r * 0.6f, Magenta, 0.2f);
        HeroFx.Bits(at, Magenta, NyxDrones.Cyan, 14, 6f, 0.1f);
        Lighting.Flash(at, r * 2f, Magenta, 0.2f);
        Sfx.Play(Sfx.Id.Zap, 0.4f, 0.8f);
    }

    // Mark Target: every few seconds, the toughest cats nearby get a reticle and take extra damage.
    private void UpdateMarks()
    {
        for (int i = marked.Count - 1; i >= 0; i--)
        {
            Mark m = marked[i];
            bool same = m.e != null && m.e.Life == m.life;
            if (!same || m.e.IsDead || !m.e.IsMarked)
            {
                if (same && m.e.IsDead) // target down: the reticle bursts
                {
                    Vector2 p = m.reticle.transform.position;
                    ExplosionFx.Ring(p, 1f, MarkRed, 0.25f);
                    HeroFx.Bits(p, MarkRed, Color.white, 8, 4f);
                }
                if (m.reticle != null) Destroy(m.reticle.gameObject);
                marked.RemoveAt(i);
                continue;
            }
            // Lock-on: swoops in big and turned, snaps tight, then breathes.
            float k = Mathf.Clamp01((Time.time - m.born) / 0.25f), ease = 1f - (1f - k) * (1f - k);
            float pulse = k < 1f ? 1f : 1f + 0.08f * Mathf.Sin(Time.time * 8f);
            m.reticle.transform.position = (Vector2)m.e.transform.position + Vector2.up * 0.05f;
            m.reticle.transform.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, ease) * pulse;
            m.reticle.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(90f, 0f, ease));
            m.reticle.color = new Color(MarkRed.r, MarkRed.g, MarkRed.b, Mathf.Lerp(0.3f, 0.95f, ease) * (k < 1f || Mathf.Repeat(Time.time * 2f, 1f) > 0.1f ? 1f : 0.4f));
        }

        if ((markTimer -= Time.deltaTime) > 0f) return;
        markTimer = 1f;
        for (int n = marked.Count; n < marks; n++)
        {
            EnemyHealth best = null;
            foreach (Collider2D c in Bomb.OverlapEnemies(transform.position, 8f, hits))
                if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead && !e.IsMarked && (best == null || e.CurrentHealth > best.CurrentHealth)) best = e;
            if (best == null) return;
            MarkCat(best);
        }
    }

    private void MarkCat(EnemyHealth e)
    {
        e.markedUntil = Time.time + 6f;
        if (markXp && !e.markedXp) { e.markedXp = true; e.xpValue *= 3; }
        var sr = new GameObject("Mark").AddComponent<SpriteRenderer>();
        sr.sprite = HeroFx.Reticle;
        sr.color = MarkRed;
        sr.sortingOrder = 75;
        sr.transform.position = e.transform.position;
        Lighting.Glow(sr);
        marked.Add(new Mark { e = e, life = e.Life, reticle = sr, born = Time.time });
        DamagePopup.SpawnText((Vector2)e.transform.position + Vector2.up * 0.6f, "MARKED", new Color(1f, 0.35f, 0.35f), 3f);
        Sfx.Play(Sfx.Id.Tick, 0.4f, 1.4f);
    }
}
