using System.Collections.Generic;
using UnityEngine;

// Signature attacks for the Winter and Volcano bosses (Emperor Penguin, Yeti, Big Salamander, Magma Slug King).
// Same pattern as BossMoves.cs: StartMoveWV sets a move up, UpdateMoveWV runs it (false = done), EndMoveWV cleans up.
// Effect art lives in Resources/Fx/Boss.
public partial class BossAI
{
    [Header("Emperor Penguin")]
    public int icicleCount = 8;
    public float icicleRadius = 0.9f;
    public float icicleArea = 4f;
    public float slideTime = 1.4f;
    public float slideSpeed = 10f;
    [Range(0f, 0.9f)] public float iceSlow = 0.4f;

    [Header("Yeti")]
    public int snowballCount = 3;
    public float snowballRadius = 1.4f;
    public float snowballFlight = 0.9f;
    public int spikeCount = 9;
    public float spikeSpacing = 0.9f;
    public float spikeRadius = 0.7f;
    public float avalancheRadius = 2.6f;

    [Header("Magma Slug King / Big Salamander")]
    public float lavaTrailTime = 3.5f;
    public float lavaRadius = 1f;
    public float lavaDuration = 5f;
    public int eruptionCount = 6;
    public float eruptionArea = 4.5f;
    public float breathTime = 1.4f;
    public float breathRange = 5f;
    [Range(10f, 120f)] public float breathAngle = 40f;

    private static readonly Color Ice = new Color(0.6f, 0.88f, 1f), Snow = new Color(0.92f, 0.96f, 1f), Magma = new Color(1f, 0.45f, 0.12f);

    private class Drop { public Vector2 at; public float warnAt, hitAt; public bool warned, done; }
    private readonly List<Drop> drops = new List<Drop>();
    private Vector2 wvDir;
    private float wvTimer, wvTick;

    private static Sprite fxIcicle, fxSpike, fxSnowball, fxGlob;
    private static Sprite[] fxFire;
    private static Sprite FxIcicle => fxIcicle != null ? fxIcicle : fxIcicle = Shapes.Art("Fx/Boss/icicle");
    private static Sprite FxSpike => fxSpike != null ? fxSpike : fxSpike = Shapes.Art("Fx/Boss/ice_spike");
    private static Sprite FxSnowball => fxSnowball != null ? fxSnowball : fxSnowball = Shapes.Art("Fx/Boss/snowball");
    private static Sprite FxGlob => fxGlob != null ? fxGlob : fxGlob = Shapes.Art("Fx/Boss/lava_glob");
    private static Sprite[] FxFire
    {
        get
        {
            if (fxFire != null) return fxFire;
            var tex = Resources.Load<Texture2D>("Fx/Boss/fire_puff");
            if (tex == null) return fxFire = new[] { Shapes.Circle };
            tex.filterMode = FilterMode.Point;
            fxFire = new Sprite[4];
            for (int i = 0; i < 4; i++) fxFire[i] = Sprite.Create(tex, new Rect(i * 16, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            return fxFire;
        }
    }

    private void StartMoveWV(Vector2 at)
    {
        drops.Clear();
        wvTimer = wvTick = 0f;
        Vector2 toPlayer = (Vector2)player.position - rb.position;
        wvDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.down;

        switch (current)
        {
            case Special.IcicleRain:
                DamagePopup.SpawnText(at, "ICICLE RAIN!", Ice, 5f);
                int n = enraged ? icicleCount + 4 : icicleCount;
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = i == 0 ? (Vector2)player.position + playerVel * 0.9f : (Vector2)player.position + Random.insideUnitCircle * icicleArea;
                    drops.Add(new Drop { at = p, warnAt = i * 0.1f, hitAt = i * 0.1f + 0.9f });
                }
                break;
            case Special.PenguinParade:
                DamagePopup.SpawnText(at, "PENGUIN PARADE!", Snow, 5f);
                break;
            case Special.IceSlide:
                Play(run, 14f, true);
                AoeWarning.SpawnLine(rb.position, wvDir, slideSpeed * slideTime * 0.6f, 1.2f * Size, 0.4f);
                DamagePopup.SpawnText(at, "BELLY SLIDE!", Ice, 5f);
                break;
            case Special.Snowballs:
                DamagePopup.SpawnText(at, "SNOWBALLS!", Snow, 5f);
                break;
            case Special.IceSpikes:
                DamagePopup.SpawnText(at, "ICE SPIKES!", Ice, 5f);
                float[] fan = enraged ? new[] { -25f, 0f, 25f } : new[] { 0f };
                foreach (float a in fan)
                {
                    Vector2 d = Quaternion.Euler(0f, 0f, a) * wvDir;
                    for (int i = 0; i < spikeCount; i++)
                        drops.Add(new Drop { at = rb.position + d * (1.2f + i * spikeSpacing) * Size, warnAt = 0.1f, hitAt = 0.6f + i * 0.07f });
                }
                break;
            case Special.Avalanche:
                AoeWarning.Spawn(rb.position, avalancheRadius * Size, 0.8f);
                DamagePopup.SpawnText(at, "AVALANCHE!", Snow, 5f);
                break;
            case Special.LavaTrail:
                Play(run, 12f, true);
                DamagePopup.SpawnText(at, "LAVA TRAIL!", Magma, 5f);
                break;
            case Special.Eruption:
                DamagePopup.SpawnText(at, "ERUPTION!", Magma, 5f);
                int m = enraged ? eruptionCount + 3 : eruptionCount;
                for (int i = 0; i < m; i++)
                {
                    Vector2 p = i == 0 ? (Vector2)player.position + playerVel * 1.2f : (Vector2)player.position + Random.insideUnitCircle * eruptionArea;
                    drops.Add(new Drop { at = p, warnAt = 0.3f + i * 0.06f, hitAt = 0.3f + i * 0.06f + 1f });
                }
                break;
            case Special.FireBreath:
                AoeWarning.SpawnLine(rb.position, wvDir, breathRange, breathRange * Mathf.Tan(breathAngle * 0.5f * Mathf.Deg2Rad) * 1.6f, 0.5f);
                DamagePopup.SpawnText(at, "FIRE BREATH!", Magma, 5f);
                break;
        }
    }

    private bool UpdateMoveWV(float t, Vector2 dir)
    {
        switch (current)
        {
            case Special.IcicleRain:
                foreach (Drop d in drops)
                {
                    if (!d.warned && t >= d.warnAt) { d.warned = true; AoeWarning.Spawn(d.at, icicleRadius, d.hitAt - d.warnAt); FallingIcicle(d.at, d.hitAt - t); }
                    if (!d.done && t >= d.hitAt)
                    {
                        d.done = true;
                        Juice.Sparks(d.at, Ice, 8, 4f, 0.12f);
                        ExplosionFx.Ring(d.at, icicleRadius, Ice, 0.25f);
                        HitPlayer(d.at, icicleRadius, zapDamage * 0.8f);
                    }
                }
                return !AllDone(t, 0.2f);

            case Special.PenguinParade:
                if (!stepDone && t >= 0.6f)
                {
                    stepDone = true;
                    EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
                    if (spawner != null) for (int i = 0; i < (enraged ? 2 : 1); i++) spawner.SpawnSwarm(summonPrefab);
                    CameraFollow.Shake(0.1f, 0.2f);
                }
                return t < 0.8f;

            case Special.IceSlide:
                if (t < 0.4f) { moveVelocity = Vector2.zero; facing = wvDir; return true; }
                wvDir = Vector2.Lerp(wvDir, dir, Time.fixedDeltaTime * 1.5f).normalized; // steers a little
                facing = wvDir;
                moveVelocity = wvDir * slideSpeed;
                if ((wvTick -= Time.fixedDeltaTime) <= 0f)
                {
                    wvTick = 0.12f;
                    EnemyPuddle.Spawn(rb.position, 0.8f * Size, 4f, 0f, playerHealth, LavaArt.IcePal, iceSlow);
                    Juice.Sparks(rb.position, Snow, 2, 2f, 0.1f);
                }
                return t < 0.4f + slideTime;

            case Special.Snowballs:
                float[] throwAt = enraged ? new[] { 0.4f, 0.65f, 0.9f, 1.15f } : new[] { 0.4f, 0.7f, 1f };
                int throws = Mathf.Min(throwAt.Length, snowballCount + (enraged ? 1 : 0));
                if (volleysFired < throws && t >= throwAt[volleysFired])
                {
                    volleysFired++;
                    Vector2 target = (Vector2)player.position + playerVel * snowballFlight * Random.Range(0.6f, 1.1f) + Random.insideUnitCircle * 0.8f;
                    Lob(FxSnowball, Color.white, rb.position + Vector2.up * 0.6f * Size, target, snowballFlight, 0.9f, snowballRadius, landed =>
                    {
                        StompFx.Spawn(landed, snowballRadius, true);
                        Juice.Sparks(landed, Snow, 10, 4f, 0.14f);
                        CameraFollow.Shake(0.1f, 0.12f);
                        HitPlayer(landed, snowballRadius, zapDamage);
                    });
                    Sfx.Play(Sfx.Id.Whoosh, 0.5f, 0.8f);
                }
                return t < throwAt[throws - 1] + snowballFlight + 0.1f;

            case Special.IceSpikes:
                foreach (Drop d in drops)
                {
                    if (!d.warned && t >= d.warnAt) { d.warned = true; AoeWarning.Spawn(d.at, spikeRadius, d.hitAt - d.warnAt); }
                    if (!d.done && t >= d.hitAt)
                    {
                        d.done = true;
                        Spike(d.at);
                        HitPlayer(d.at, spikeRadius, zapDamage * 0.9f);
                    }
                }
                return !AllDone(t, 0.3f);

            case Special.Avalanche:
                if (t < 0.8f)
                {
                    transform.localScale = baseScale * (1f + 0.12f * Mathf.Sin(t / 0.8f * Mathf.PI));
                    return true;
                }
                if (!stepDone)
                {
                    stepDone = true;
                    transform.localScale = baseScale;
                    float r = avalancheRadius * Size;
                    StompFx.Spawn(rb.position, r, true);
                    ExplosionFx.Ring(rb.position, r * 1.3f, Snow, 0.4f);
                    CameraFollow.Shake(0.2f, 0.3f);
                    Sfx.Play(Sfx.Id.Boom, 0.7f, 0.8f);
                    HitPlayer(rb.position, r, zapDamage * 1.2f);
                    int shards = enraged ? 16 : 12;
                    float turn = Random.Range(0f, 360f);
                    for (int i = 0; i < shards; i++)
                    {
                        float a = (turn + i * 360f / shards) * Mathf.Deg2Rad;
                        EnemyProjectile.Spawn(rb.position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ringSpeed, ringDamage, playerHealth, 0.35f, Ice);
                    }
                }
                return t < 1.1f;

            case Special.LavaTrail:
                moveVelocity = dir * moveSpeed * 1.7f;
                if ((wvTick -= Time.fixedDeltaTime) <= 0f)
                {
                    wvTick = 0.22f;
                    EnemyPuddle.Spawn(rb.position, lavaRadius * Size, lavaDuration, ringDamage * 0.5f, playerHealth, LavaArt.LavaPal);
                }
                return t < lavaTrailTime;

            case Special.Eruption:
                foreach (Drop d in drops)
                {
                    if (!d.warned && t >= d.warnAt)
                    {
                        d.warned = true;
                        AoeWarning.Spawn(d.at, lavaRadius * 1.2f, d.hitAt - d.warnAt);
                        float r = lavaRadius * 1.2f;
                        Lob(FxGlob, Color.white, rb.position + Vector2.up * 0.5f * Size, d.at, d.hitAt - d.warnAt, 0.7f, r, landed =>
                        {
                            ExplosionFx.Spawn(landed, r * 0.8f, Magma);
                            Juice.Sparks(landed, Magma, 8, 4f, 0.12f);
                            EnemyPuddle.Spawn(landed, r, lavaDuration, ringDamage * 0.4f, playerHealth, LavaArt.LavaPal);
                            HitPlayer(landed, r, zapDamage);
                        });
                    }
                    if (!d.done && t >= d.hitAt) d.done = true;
                }
                if (t < 0.4f) CameraFollow.Shake(0.05f, 0.05f);
                return !AllDone(t, 0.2f);

            case Special.FireBreath:
                if (t < 0.5f) { facing = wvDir; return true; }
                // turn slowly toward the player so you can outrun it
                float turnRate = (enraged ? 70f : 45f) * Time.fixedDeltaTime;
                float cur = Mathf.Atan2(wvDir.y, wvDir.x) * Mathf.Rad2Deg, want = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                cur = Mathf.MoveTowardsAngle(cur, want, turnRate);
                wvDir = new Vector2(Mathf.Cos(cur * Mathf.Deg2Rad), Mathf.Sin(cur * Mathf.Deg2Rad));
                facing = wvDir;
                if ((wvTimer -= Time.fixedDeltaTime) <= 0f)
                {
                    wvTimer = 0.045f;
                    FirePuff(rb.position + wvDir * 0.5f * Size, Quaternion.Euler(0f, 0f, Random.Range(-breathAngle, breathAngle) * 0.5f) * wvDir);
                }
                if ((wvTick -= Time.fixedDeltaTime) <= 0f)
                {
                    Vector2 rel = (Vector2)player.position - rb.position;
                    if (rel.magnitude <= breathRange && Vector2.Angle(wvDir, rel) <= breathAngle * 0.5f && playerHealth != null)
                    {
                        wvTick = 0.25f;
                        playerHealth.TakeDamage(ringDamage * 0.6f);
                    }
                }
                return t < 0.5f + breathTime;
        }
        return false;
    }

    private void EndMoveWV()
    {
        drops.Clear();
        moveVelocity = Vector2.zero;
    }

    private bool AllDone(float t, float tail)
    {
        float last = 0f;
        foreach (Drop d in drops) { if (!d.done) return false; last = Mathf.Max(last, d.hitAt); }
        return t >= last + tail;
    }

    private void HitPlayer(Vector2 at, float radius, float damage)
    {
        if (playerHealth != null && player != null && Vector2.Distance(player.position, at) <= radius) playerHealth.TakeDamage(damage);
    }

    // An icicle that drops from above and lands at 'at' after 'delay' seconds.
    private static void FallingIcicle(Vector2 at, float delay)
    {
        const float Fall = 0.25f, Height = 5f;
        Shot s = Shot.Spawn(FxIcicle, new Color(1f, 1f, 1f, 0f), 1.3f, at + Vector2.up * Height, 40);
        s.life = Mathf.Max(Fall, delay);
        float start = s.life - Fall;
        s.onUpdate = shot =>
        {
            float k = Mathf.Clamp01((shot.age - start) / Fall);
            shot.Renderer.color = new Color(1f, 1f, 1f, shot.age < start ? 0f : 1f);
            shot.transform.position = at + Vector2.up * (Height * (1f - k * k) + 0.55f);
        };
    }

    // An ice spike that bursts out of the ground, then sinks back.
    private void Spike(Vector2 at)
    {
        Shot s = Shot.Spawn(FxSpike, Color.white, 0.1f, at + Vector2.up * 0.4f * Size, 30);
        s.life = 0.7f;
        float size = 1.3f * Size;
        s.onUpdate = shot =>
        {
            float grow = Mathf.Clamp01(shot.age / 0.08f), fade = Mathf.Clamp01((shot.life - shot.age) / 0.25f);
            shot.transform.localScale = Vector3.one * size * grow;
            shot.Renderer.color = new Color(1f, 1f, 1f, fade);
        };
        Juice.Sparks(at, Ice, 4, 3f, 0.1f);
    }

    // A thrown object that arcs from 'from' to 'to' and calls onLand there.
    private static void Lob(Sprite sprite, Color color, Vector2 from, Vector2 to, float flight, float size, float landRadius, System.Action<Vector2> onLand)
    {
        Shot s = Shot.Spawn(sprite, color, size, from, 40);
        s.life = Mathf.Max(0.1f, flight);
        float arc = 1.5f + Vector2.Distance(from, to) * 0.25f;
        s.onUpdate = shot =>
        {
            float k = Mathf.Clamp01(shot.age / shot.life);
            shot.transform.position = Vector2.Lerp(from, to, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * arc;
            shot.transform.localScale = Vector3.one * size * (1f + 0.35f * Mathf.Sin(k * Mathf.PI));
            shot.transform.Rotate(0f, 0f, 360f * Time.deltaTime);
        };
        s.onExpire = _ => onLand(to);
    }

    private void FirePuff(Vector2 from, Vector2 dir)
    {
        Sprite[] frames = FxFire;
        float speed = 7f, life = breathRange / speed;
        Shot s = Shot.Spawn(frames[0], Color.white, 0.5f * Size, from, 35);
        s.velocity = dir * speed;
        s.life = life;
        float size = Size;
        s.onUpdate = shot =>
        {
            float k = shot.age / shot.life;
            shot.Renderer.sprite = frames[Mathf.Min(frames.Length - 1, (int)(k * frames.Length))];
            shot.transform.localScale = Vector3.one * size * Mathf.Lerp(0.5f, 1.6f, k);
        };
    }
}
