using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Nyx's main attack instead of bombs: a swarm of little zapper drones orbiting her.
// Each drone darts at a cat, zaps it (small electric blast) and flies back. "+1 bomb" upgrades add drones,
// and the bomb skills/combos have drone versions here (Chain Zap, Packet Split, Static Field, ...).
[RequireComponent(typeof(PlayerStats))]
public class NyxDrones : MonoBehaviour
{
    public const string Source = "Drones";
    public static NyxDrones Active { get; private set; }

    public float fireInterval = 1.8f; // per drone at 1x attack speed
    public float range = 6.5f;
    public float orbitRadius = 1.3f;
    public float damageMultiplier = 0.55f;
    public float zapRadius = 1.2f; // same as a bomb blast

    public static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
    private static readonly List<Collider2D> hits = new List<Collider2D>(64), near = new List<Collider2D>(64);
    private static Sprite[] droneSprites;

    private enum State { Orbit, Out, Back }

    private class Drone
    {
        public SpriteRenderer sr, halo;
        public TrailRenderer thruster;
        public State state;
        public Vector2 pos, from, aim;
        public EnemyHealth target;
        public float timer, t;
    }

    private readonly List<Drone> drones = new List<Drone>();
    private PlayerStats stats;
    private PlayerUltimate ult;
    private PlayerSkills skills;
    private PlayerAnimator anim;
    private float orbit, volleyTimer, nextLaunch, barrageUntil;

    private float Fury => PlayerCollabs.Has("furyfuse") && stats.maxHealth > 0f ? 1f - Mathf.Clamp01(stats.currentHealth / stats.maxHealth) : 0f;
    // Attack speed scales her fire rate like everyone else (it used to count extra and snowballed late).
    private float Interval => fireInterval / Mathf.Max(0.1f, stats.AttackSpeed * (1f + Fury));
    // Swarm size: 1 base + 1 per bomb upgrade (2, 3, 4, 5).
    public int Count { get { int n = Mathf.Max(1, stats.ProjectileCount); return n + 1 + (ult != null ? ult.ExtraBombs : 0); } }
    public float ZapRadius => zapRadius * stats.areaMultiplier * (ult != null ? ult.RadiusMultiplier : 1f) * (1f + 0.5f * Fury);
    public float ZapDamage => stats.AttackDamage * damageMultiplier * (ult != null ? ult.DamageMultiplier : 1f);

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        ult = GetComponent<PlayerUltimate>();
        skills = GetComponent<PlayerSkills>();
        anim = GetComponent<PlayerAnimator>();
    }

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
        foreach (Drone d in drones) if (d.sr != null) Destroy(d.sr.gameObject);
        drones.Clear();
        StopAllCoroutines();
    }

    private void Update()
    {
        if (stats.currentHealth <= 0f) return;
        float dt = Time.deltaTime;
        Vector2 me = transform.position;

        while (drones.Count < Count) AddDrone(me);
        while (drones.Count > Count) { Destroy(drones[drones.Count - 1].sr.gameObject); drones.RemoveAt(drones.Count - 1); }

        orbit += dt * 110f;
        float interval = Interval;
        for (int i = 0; i < drones.Count; i++)
        {
            Drone d = drones[i];
            d.timer += dt; // reloads while flying, too
            float a = (orbit + i * 360f / drones.Count) * Mathf.Deg2Rad;
            Vector2 home = me + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f) * orbitRadius + Vector2.up * (0.35f + 0.08f * Mathf.Sin(Time.time * 4f + i));

            switch (d.state)
            {
                case State.Orbit:
                    d.pos = Vector2.Lerp(d.pos, home, dt * 10f);

                    // Launches are spaced out so the swarm fires as a steady stream, not all at once.
                    if (d.timer >= interval && (Time.time >= nextLaunch || Time.time < barrageUntil) && FindTarget(d, me))
                    {
                        Launch(d);
                        nextLaunch = Time.time + interval / drones.Count * 0.6f;
                    }
                    break;
                case State.Out:
                    if (d.target != null && !d.target.IsDead) d.aim = d.target.transform.position;
                    d.t += dt / 0.14f;
                    d.pos = Vector2.Lerp(d.from, d.aim + (d.from - d.aim).normalized * 0.4f, Mathf.SmoothStep(0f, 1f, d.t));
                    if (d.t >= 1f) { Zap(d.pos, d.aim, 1f, true); d.state = State.Back; d.from = d.pos; d.t = 0f; }
                    break;
                case State.Back:
                    d.t += dt / 0.25f;
                    d.pos = Vector2.Lerp(d.from, home, Mathf.SmoothStep(0f, 1f, d.t));
                    if (d.t >= 1f) d.state = State.Orbit;
                    break;
            }
            Vector2 moved = d.pos - (Vector2)d.sr.transform.position;
            d.sr.transform.position = d.pos;
            d.sr.sprite = DroneSprites[(int)(Time.time * 20f + i) % 2]; // rotors
            d.sr.sortingOrder = d.pos.y < me.y ? 72 : 68;
            // Lean into the direction of travel; thruster streak only while darting.
            float lean = dt > 0f ? Mathf.Clamp(-moved.x / dt * 2.5f, -30f, 30f) : 0f;
            d.sr.transform.rotation = Quaternion.Lerp(d.sr.transform.rotation, Quaternion.Euler(0f, 0f, lean), dt * 12f);
            d.thruster.emitting = d.state != State.Orbit;
            d.halo.color = new Color(Cyan.r, Cyan.g, Cyan.b, (d.state == State.Out ? 0.3f : 0.12f) * (0.8f + 0.2f * Mathf.Sin(Time.time * 6f + i)));
        }

        // Volley hooks (once per fire interval, not per drone).
        volleyTimer += dt;
        if (volleyTimer >= interval)
        {
            volleyTimer = 0f;
            Collider2D first = Nearest(me, range);
            if (first != null)
            {
                if (anim != null) anim.PlayThrow(first.transform.position - transform.position); // her gadget pose
                if (SuperWeapon.Active != null) SuperWeapon.Active.OnThrow(first.transform.position);
                if (PlayerCollabs.Has("carpet")) StartCoroutine(DroneStrike(((Vector2)first.transform.position - me).normalized));
            }
        }
    }

    private void AddDrone(Vector2 at)
    {
        var sr = new GameObject("Drone").AddComponent<SpriteRenderer>();
        sr.sprite = DroneSprites[0];
        sr.transform.position = at;
        var halo = new GameObject("Glow").AddComponent<SpriteRenderer>(); // soft light under it
        halo.transform.SetParent(sr.transform, false);
        halo.transform.localScale = Vector3.one * 0.5f;
        halo.sprite = Shapes.Circle;
        halo.sortingOrder = 66;
        Lighting.Glow(halo);
        TrailRenderer thruster = HeroFx.Trail(sr.transform, Vector3.zero, 0.14f, Cyan, 0.14f, 67);
        Lighting.MakeLight("DroneLight", sr.transform, 1.3f, 0.55f, Cyan); // each drone lights the ground under it
        drones.Add(new Drone { sr = sr, halo = halo, thruster = thruster, pos = at, timer = Random.value * Interval });
        HeroFx.Bits(at, Cyan, Color.white, 6, 3f); // boots up in a burst of pixels
        Sfx.Play(Sfx.Id.Blink, 0.25f, 1.6f);
    }

    // Prefer a cat no other drone is already chasing.
    private bool FindTarget(Drone d, Vector2 me)
    {
        Bomb.OverlapEnemies(me, range, near);
        EnemyHealth best = null;
        float bestDist = float.MaxValue;
        foreach (Collider2D c in near)
        {
            if (!c.TryGetComponent(out EnemyHealth e) || e.IsDead) continue;
            float dist = ((Vector2)c.transform.position - me).sqrMagnitude + (drones.Exists(o => o.state == State.Out && o.target == e) ? 25f : 0f);
            if (dist < bestDist) { bestDist = dist; best = e; }
        }
        d.target = best;
        return best != null;
    }

    private void Launch(Drone d)
    {
        d.state = State.Out;
        d.from = d.pos;
        d.aim = d.target.transform.position;
        d.t = 0f;
        d.timer = 0f;
        if (PlayerCollabs.Has("magnetbomb")) // Tractor Drones
            foreach (Collider2D c in Bomb.OverlapEnemies(d.aim, ZapRadius * 2.2f, hits))
                if (c.TryGetComponent(out IEnemyBrain brain)) brain.Knockback((d.aim - (Vector2)c.transform.position) * 3f);
    }

    // A zap: lightning from the drone, a small electric blast at the target.
    private void Zap(Vector2 from, Vector2 at, float damageMult, bool primary, bool exploitChain = false)
    {
        NyxPassives nyx = NyxPassives.Active;
        bool exploit = primary && nyx != null && nyx.NextZapIsExploit(); // Backdoor: a forced crit with bonus damage
        float r = ZapRadius * (exploit ? 1.4f : 1f), damage = ZapDamage * damageMult * (exploit ? 1.6f : 1f);
        bool stunGun = PlayerCollabs.Has("concussion");
        DamageStats.Source = exploit || exploitChain ? "Backdoor" : primary ? Source : "Chain Zap";
        Color color = exploit || exploitChain ? NyxPassives.Magenta : Cyan;
        foreach (Collider2D c in Bomb.OverlapEnemies(at, r, hits))
        {
            if (!c.TryGetComponent(out EnemyHealth e) || e.IsDead) continue;
            Hit(e, at, damage, stunGun ? 0.6f : 0f, exploit || exploitChain);
            if (nyx != null) nyx.TryHack(e);
        }
        // Bolt from the drone, a white-hot core, a crisp electric ring, and data bits scattering.
        LightningFx.SpawnBetween(from, at, color, exploit ? 1f : 0.6f);
        HeroFx.Pop(at, color, r * 0.9f);
        ExplosionFx.Ring(at, r, color, 0.18f);
        HeroFx.Bits(at, color, Color.white, exploit ? 12 : 5, exploit ? 5f : 3.5f);
        Lighting.Flash(at, r * 2f, color, 0.12f);
        Sfx.Play(Sfx.Id.Zap, exploit ? 0.5f : 0.25f, exploit ? 0.9f : Random.Range(1.2f, 1.5f));
        if (exploit)
        {
            DamagePopup.SpawnText(at + Vector2.up * 0.5f, "EXPLOIT!", NyxPassives.Magenta, 4f);
            ExplosionFx.Ring(at, r * 1.7f, Color.white, 0.25f);
            Juice.Flash(new Color(1f, 0.3f, 0.85f, 0.08f), 0.08f); // a screen glitch blink
            CameraFollow.Shake(0.06f, 0.08f);
            if (nyx.ExploitChains) // jumps to the 3 nearest other cats
            {
                var others = new List<Vector2>();
                foreach (Collider2D c in Bomb.OverlapEnemies(at, 4f, near))
                    if ((Vector2)c.transform.position != at && c.TryGetComponent(out EnemyHealth e) && !e.IsDead) others.Add(c.transform.position);
                others.Sort((a, b) => (a - at).sqrMagnitude.CompareTo((b - at).sqrMagnitude));
                for (int i = 0; i < Mathf.Min(3, others.Count); i++) Zap(at, others[i], damageMult, false, true);
            }
        }
        if (primary) DroneSkills(at, r, damage);
    }

    public void Hit(EnemyHealth e, Vector2 from, float damage, float stun, bool forceCrit = false)
    {
        bool crit = forceCrit || Random.value < stats.critChance;
        e.TakeDamage(damage * (crit ? stats.critDamage : 1f) * Random.Range(0.9f, 1.1f), from, crit);
        if (e.IsDead) return;
        if (stun > 0f && e.TryGetComponent(out EnemyAI cat)) cat.Stun(stun);
    }

    // Drone versions of the bomb skills and the bomb-hooked supers.
    private void DroneSkills(Vector2 at, float r, float damage)
    {
        if (skills != null)
        {
            int chain = skills.LevelOf("chain");
            if (chain > 0 && Random.value < new[] { 0.15f, 0.25f, 0.35f }[chain - 1])
            {
                Collider2D next = Nearest(at, 3.5f, r * 0.5f);
                if (next != null) Zap(at, next.transform.position, new[] { 0.5f, 0.6f, 0.75f }[chain - 1], false);
            }

            int cluster = skills.LevelOf("cluster");
            if (cluster > 0) Packets(at, cluster + 1, damage * new[] { 0.25f, 0.3f, 0.35f }[cluster - 1]);

            int napalm = skills.LevelOf("napalm");
            if (napalm > 0)
            {
                bool overload = PlayerCollabs.Has("firebomb");
                DamageStats.Source = "Static Field";
                BurningGround.Spawn(at, r * (overload ? 1.3f : 0.9f), overload ? 5f : new[] { 2f, 2.5f, 3f }[napalm - 1],
                                    damage * (overload ? 0.22f : new[] { 0.08f, 0.12f, 0.16f }[napalm - 1]), Cyan);
            }
        }
        if (SuperWeapon.Active != null) SuperWeapon.Active.OnBombBlast(at, r, damage);
    }

    // Packet Split: little data packets fly to nearby cats.
    private void Packets(Vector2 from, int count, float damage)
    {
        Bomb.OverlapEnemies(from, 4f, near);
        DamageStats.Source = "Packet Split";
        for (int i = 0; i < count; i++)
        {
            Vector2 at = near.Count > 0 ? (Vector2)near[Random.Range(0, near.Count)].transform.position : from + Random.insideUnitCircle.normalized * 2f;
            // A spinning "+" data packet with a ghost trail.
            Shot s = Shot.Spawn(Shapes.Plus, Color.white, 0.24f, from, 60);
            s.life = 0.15f;
            s.velocity = (at - from) / s.life;
            s.spin = 720f;
            s.ghost = new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f);
            s.trail = Cyan;
            s.trailSize = 0.06f;
            s.onExpire = shot =>
            {
                Vector2 p = shot.Position;
                foreach (Collider2D c in Bomb.OverlapEnemies(p, 0.6f * stats.areaMultiplier, hits))
                    if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead) Hit(e, p, damage, 0f);
                HeroFx.Pop(p, Cyan, 0.45f);
                HeroFx.Bits(p, Cyan, Color.white, 3, 2.5f, 0.07f);
            };
        }
    }

    // Drone Strike: a line of zaps from the sky ahead of her.
    private IEnumerator DroneStrike(Vector2 dir)
    {
        Vector2 origin = transform.position;
        float damage = stats.AttackDamage * 0.45f * (ult != null ? ult.DamageMultiplier : 1f);
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(0.07f);
            Vector2 at = origin + dir * (1.6f + i) * stats.areaMultiplier;
            DamageStats.Source = "Drone Strike";
            LightningFx.Spawn(at, Cyan, 0.8f);
            foreach (Collider2D c in Bomb.OverlapEnemies(at, 0.8f * stats.areaMultiplier, hits))
                if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead) Hit(e, at, damage, 0f);
            HeroFx.Pop(at, Cyan, 0.8f);
            ExplosionFx.Ring(at, 0.8f * stats.areaMultiplier, Cyan, 0.2f);
            HeroFx.Bits(at, Cyan, Color.white, 4, 3f);
        }
    }

    // EMP: a stunning electric burst (Blink's parting gift; the ultimate opens with one).
    public void Emp(Vector2 at, float damageMult)
    {
        float r = 3.2f * stats.areaMultiplier;
        DamageStats.Source = "EMP";
        int arcs = 0;
        foreach (Collider2D c in Bomb.OverlapEnemies(at, r, hits))
        {
            if (!c.TryGetComponent(out EnemyHealth e) || e.IsDead) continue;
            if (arcs++ < 6) LightningFx.SpawnBetween(at, c.transform.position, Cyan, 0.5f); // arcs out to the first few cats
            Hit(e, at, ZapDamage * damageMult, 0.8f);
            if (c.TryGetComponent(out IEnemyBrain brain)) brain.Knockback(((Vector2)c.transform.position - at).normalized * 6f);
        }
        HeroFx.Pop(at, Cyan, 1.6f);
        ExplosionFx.Ring(at, r, Cyan, 0.35f);
        ExplosionFx.Ring(at, r * 0.6f, Color.white, 0.25f);
        ExplosionFx.Ring(at, r * 0.3f, Cyan, 0.15f);
        Lighting.Flash(at, r * 1.5f, Cyan, 0.3f);
        HeroFx.Bits(at, Cyan, Color.white, 16, 7f, 0.11f);
        Juice.Flash(new Color(0.3f, 0.9f, 1f, 0.1f), 0.12f);
        CameraFollow.Shake(0.12f, 0.15f);
        Sfx.Play(Sfx.Id.Zap, 0.7f, 0.7f);
    }

    // Ultimate opener: an EMP, then every drone launches at once.
    public void Barrage()
    {
        Emp(transform.position, 2.5f);
        foreach (Drone d in drones) d.timer = float.MaxValue;
        barrageUntil = Time.time + 0.3f; // the whole swarm goes at once
    }

    private static Collider2D Nearest(Vector2 from, float range, float minDistance = 0f)
    {
        Collider2D best = null;
        float bestDist = float.MaxValue;
        foreach (Collider2D c in Bomb.OverlapEnemies(from, range, near))
        {
            float d = ((Vector2)c.transform.position - from).sqrMagnitude;
            if (d >= minDistance * minDistance && d < bestDist && c.TryGetComponent(out EnemyHealth e) && !e.IsDead) { bestDist = d; best = c; }
        }
        return best;
    }

    // Little quadcopter, two rotor frames.
    public static Sprite[] DroneSprites
    {
        get
        {
            if (droneSprites != null) return droneSprites;
            string[] body = {
                "..KKKK..KKKK..",
                "......KK......",
                "...KKKKKKKK...",
                "..KDDDDDDDDK..",
                "..KDDCCCCDDK..",
                "..KDDCWWCDDK..",
                "...KDDDDDDK...",
                "....K....K....",
            };
            droneSprites = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                var tex = new Texture2D(14, 8) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 14; x++)
                    {
                        char ch = body[7 - y][x];
                        if (y == 7 && ch == 'K' && f == 1 && (x == 2 || x == 5 || x == 8 || x == 11)) ch = '.'; // rotor blur
                        tex.SetPixel(x, y, ch switch
                        {
                            'K' => new Color(0.08f, 0.08f, 0.1f),
                            'D' => new Color(0.32f, 0.34f, 0.4f),
                            'C' => Cyan,
                            'W' => Color.white,
                            _ => Color.clear,
                        });
                    }
                tex.Apply();
                droneSprites[f] = Sprite.Create(tex, new Rect(0, 0, 14, 8), new Vector2(0.5f, 0.5f), 20f);
            }
            return droneSprites;
        }
    }
}
