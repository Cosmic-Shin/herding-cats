using System.Collections.Generic;
using UnityEngine;

// Signature attacks for the Meadow bosses (Big Black Cat, Big Rabbit, Queen Bee). Winter/Volcano ones: BossMovesWinterVolcano.cs.
// Each is a BossAI.Special: StartMove sets it up, UpdateMove runs it (false = done), EndMove cleans up.
public partial class BossAI
{
    [Header("Big Black Cat")]
    public bool curseOnHit;
    public float curseSeconds = 5f;
    public float swipeReach = 2.2f;
    [Range(10f, 180f)] public float swipeAngle = 75f;
    public float prowlTime = 3.5f;
    public float prowlSpeed = 1.9f;

    [Header("Big Rabbit")]
    public float hopRadius = 1.8f;
    public float hopTime = 0.55f;
    public float hopRange = 6f;
    public int carrotCount = 7;
    public float carrotSpread = 60f;
    public float carrotSpeed = 7f;
    public float burrowTime = 2.5f;
    [Tooltip("Pops out every time it loses this much of its HP (0 = off)")]
    [Range(0f, 1f)] public float babyEvery = 0f;
    public int babyCount = 3;
    public GameObject babyPrefab;

    [Header("Queen Bee (summonPrefab = her bees)")]
    public int guardCount = 4;
    public float guardHealth = 3f;
    public float guardRadius = 2.3f;
    public float honeyRadius = 1.4f;
    public float honeyDuration = 6f;
    [Range(0f, 0.9f)] public float honeySlow = 0.45f;
    public float pollenTime = 3f;
    [Tooltip("Extra dashes per charge while enraged")]
    public int enrageExtraCharges;

    private static readonly Color Shadow = new Color(0.35f, 0.2f, 0.55f), Dirt = new Color(0.55f, 0.4f, 0.25f), HoneyGold = new Color(1f, 0.75f, 0.2f);

    private PlayerStats playerStats;
    private PlayerController playerMover;
    private Vector3 baseScale;
    private float authoredScale = 1f;
    private Color baseColor;
    private Vector2 moveVelocity;
    private bool hidden;
    private float nextBabyAt;

    // Per-move scratch
    private bool stepDone, swipeWarned, swipeDone;
    private float swipeAt, dropTimer;
    private Vector2 swipeDir;
    private int hopIndex, hops, volleysFired;
    private float hopStart;
    private Vector2 hopFrom, hopTo;
    private readonly List<Vector2> spots = new List<Vector2>();
    private SpriteRenderer mound;
    private SpriteRenderer[] eyes;
    private SpriteRenderer shadow;
    private Color shadowColor;
    private readonly List<EnemyHealth> guards = new List<EnemyHealth>();

    // The timeline's per-event scale (1 = as authored in the prefab), so move sizes follow it.
    // The enemy's real colour: sr.color may be mid hit-flash.
    private Color BaseColor => health != null ? health.BaseColor : Color.white;

    private float Size => authoredScale > 0f ? baseScale.x / authoredScale : 1f;
    private const float StepFade = 0.35f, SwipeWindup = 0.45f, BurrowDig = 0.4f, PopWarn = 0.6f;

    private void InitMoves()
    {
        baseScale = transform.localScale;
        baseColor = BaseColor;
        Transform s = transform.Find("Shadow");
        if (s == null || !s.TryGetComponent(out shadow)) shadow = SpriteFx.AddShadow(sr, 0.6f); // bosses without a drawn shadow get the soft one
        shadowColor = shadow.color;
        nextBabyAt = 1f - babyEvery;
    }

    private bool MoveAllowed(Special s) => s != Special.RoyalGuard || AliveGuards() < 2;

    private void StartMove()
    {
        baseColor = BaseColor;
        stepDone = swipeWarned = swipeDone = false;
        volleysFired = 0;
        spots.Clear();
        Vector2 at = rb.position + Vector2.up * 1.5f * Size;

        switch (current)
        {
            case Special.ShadowStep:
                swipeAt = StepFade + 0.15f;
                Juice.Smoke(rb.position, 0.8f * Size, 6);
                break;
            case Special.ClawSwipe:
                swipeAt = 0f;
                break;
            case Special.NightProwl:
                Play(run, 14f, true);
                DamagePopup.SpawnText(at, "NIGHT PROWL", Shadow, 5f);
                break;
            case Special.HopSlam:
            case Special.TripleHop:
                hops = current == Special.TripleHop ? 3 : 1;
                hopIndex = -1;
                NextHop();
                if (hops > 1) DamagePopup.SpawnText(at, "TRIPLE HOP!", Dirt, 5f);
                break;
            case Special.Carrots:
                DamagePopup.SpawnText(at, "CARROTS!", new Color(1f, 0.6f, 0.2f), 5f);
                break;
            case Special.Burrow:
                Play(idle, 10f, true);
                DamagePopup.SpawnText(at, "BURROW!", Dirt, 5f);
                break;
            case Special.BeeSwarm:
                DamagePopup.SpawnText(at, "BEE LINE!", HoneyGold, 5f);
                break;
            case Special.Honey:
                spots.Add((Vector2)player.position + playerVel * 0.5f);
                for (int i = 0; i < (enraged ? 4 : 2); i++) spots.Add((Vector2)player.position + Random.insideUnitCircle * 3.5f);
                foreach (Vector2 s in spots) AoeWarning.Spawn(s, honeyRadius, 0.7f);
                break;
            case Special.RoyalGuard:
                ExplosionFx.Ring(rb.position, 2.5f * Size, HoneyGold, 0.6f);
                DamagePopup.SpawnText(at, "ROYAL GUARD!", HoneyGold, 5f);
                break;
            case Special.Pollen:
                Play(run, 14f, true);
                dropTimer = 0f;
                DamagePopup.SpawnText(at, "POLLEN!", new Color(1f, 0.9f, 0.4f), 5f);
                break;
            default:
                StartMoveWV(at);
                break;
        }
    }

    private bool UpdateMove()
    {
        float t = stateTime;
        Vector2 toPlayer = (Vector2)player.position - rb.position;
        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.down;

        switch (current)
        {
            case Special.ShadowStep:
                if (t < StepFade) { Fade(Mathf.Lerp(1f, 0.15f, t / StepFade), true); return true; }
                if (!stepDone)
                {
                    stepDone = true;
                    Vector2 back = playerVel.sqrMagnitude > 0.5f ? -playerVel.normalized : dir;
                    Juice.Smoke(rb.position, 0.6f * Size, 5);
                    rb.position = (Vector2)player.position + back * (1.4f + 0.3f * Size);
                    Juice.Smoke(rb.position, 0.6f * Size, 5);
                }
                Fade(Mathf.Lerp(0.15f, 1f, (t - StepFade) / 0.15f), true);
                return UpdateSwipe(t, dir);

            case Special.ClawSwipe:
                return UpdateSwipe(t, dir);

            case Special.NightProwl:
                Fade(Mathf.Lerp(1f, 0.12f, t / 0.3f) + Mathf.Clamp01((t - prowlTime + 0.3f) / 0.3f), false);
                ShowEyes(t < prowlTime - 0.15f);
                moveVelocity = dir * moveSpeed * prowlSpeed;
                return t < prowlTime;

            case Special.HopSlam:
            case Special.TripleHop:
                return UpdateHops();

            case Special.Carrots:
                float[] volleyAt = enraged ? new[] { 0.5f, 0.8f, 1.1f } : new[] { 0.5f, 0.9f };
                if (volleysFired < volleyAt.Length && t >= volleyAt[volleysFired])
                {
                    volleysFired++;
                    for (int i = 0; i < carrotCount; i++)
                    {
                        float a = carrotCount > 1 ? -carrotSpread * 0.5f + i * carrotSpread / (carrotCount - 1) : 0f;
                        EnemyProjectile.SpawnCarrot(rb.position, (Vector2)(Quaternion.Euler(0f, 0f, a) * dir) * carrotSpeed, ringDamage, playerHealth);
                    }
                    Sfx.Play(Sfx.Id.Whoosh, 0.5f, 1.4f);
                }
                return t < volleyAt[volleyAt.Length - 1] + 0.3f;

            case Special.Burrow:
                return UpdateBurrow(t, dir);

            case Special.BeeSwarm:
                if (!stepDone && t >= 0.6f)
                {
                    stepDone = true;
                    EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
                    if (spawner != null) for (int i = 0; i < (enraged ? 2 : 1); i++) spawner.SpawnSwarm(summonPrefab);
                    CameraFollow.Shake(0.1f, 0.2f);
                }
                return t < 0.8f;

            case Special.Honey:
                if (!stepDone && t >= 0.7f)
                {
                    stepDone = true;
                    foreach (Vector2 s in spots)
                    {
                        EnemyPuddle.Spawn(s, honeyRadius, honeyDuration, 0f, playerHealth, LavaArt.HoneyPal, honeySlow);
                        ExplosionFx.Spawn(s, honeyRadius * 0.6f, HoneyGold);
                    }
                }
                return t < 0.9f;

            case Special.RoyalGuard:
                if (!stepDone && t >= 0.6f) { stepDone = true; SummonGuards(); }
                return t < 0.9f;

            case Special.Pollen:
                moveVelocity = dir * moveSpeed * 1.3f;
                if ((dropTimer -= Time.fixedDeltaTime) <= 0f)
                {
                    dropTimer = 0.22f;
                    EnemyPuddle.Spawn(rb.position - dir * 0.8f * Size, 0.9f * Size, 3.5f, ringDamage * 0.5f, playerHealth, cloud: true); // trail behind her
                }
                return t < pollenTime;

            default:
                return UpdateMoveWV(t, dir);
        }
        return false;
    }

    private void EndMove()
    {
        if (sr != null) { sr.enabled = true; sr.color = baseColor; }
        if (shadow != null) { shadow.enabled = true; shadow.color = shadowColor; }
        if (baseScale != Vector3.zero) transform.localScale = baseScale;
        ShowEyes(false);
        if (mound != null) Destroy(mound.gameObject);
        if (hidden) SetHidden(false);
        EndMoveWV();
    }

    // Black cat

    private bool UpdateSwipe(float t, Vector2 dir)
    {
        if (t < swipeAt) return true;
        float reach = swipeReach * Size;
        if (!swipeWarned)
        {
            swipeWarned = true;
            swipeDir = dir;
            AoeWarning.Spawn(rb.position + swipeDir * reach * 0.55f, reach * 0.65f, SwipeWindup);
        }
        facing = swipeDir;
        if (!swipeDone && t >= swipeAt + SwipeWindup)
        {
            swipeDone = true;
            float angle = Mathf.Atan2(swipeDir.y, swipeDir.x) * Mathf.Rad2Deg;
            Shot s = Shot.Spawn(Shapes.Art("Fx/Boss/claw_swipe"), Color.white, reach * 1.6f, rb.position + swipeDir * reach * 0.45f, 52);
            s.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            s.life = 0.25f;
            s.onUpdate = shot => shot.Renderer.color = new Color(1f, 1f, 1f, 1f - shot.age / 0.25f);
            Juice.SparksDir(rb.position + swipeDir * reach * 0.6f, swipeDir, swipeAngle, Shadow, 10, 7f, 0.14f);
            Sfx.Play(Sfx.Id.Whoosh, 0.6f, 0.8f);
            CameraFollow.Shake(0.1f, 0.12f);

            Vector2 rel = (Vector2)player.position - rb.position;
            if (rel.magnitude <= reach && Vector2.Angle(swipeDir, rel) <= swipeAngle && playerHealth != null)
            {
                playerHealth.TakeDamage(zapDamage);
                TryCurse();
            }
        }
        return t < swipeAt + SwipeWindup + 0.25f;
    }

    private void TryCurse()
    {
        if (!curseOnHit || playerStats == null || (playerMover != null && playerMover.IsInvulnerable) || AdminTool.GodMode) return;
        playerStats.Curse(curseSeconds);
    }

    private void Fade(float alpha, bool tint)
    {
        Color c = tint ? Color.Lerp(Shadow, baseColor, alpha) : baseColor;
        c.a = Mathf.Clamp01(alpha);
        sr.color = c;
        if (shadow != null) shadow.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * c.a);
    }

    private void ShowEyes(bool on)
    {
        if (eyes == null)
        {
            if (!on) return;
            eyes = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var e = new GameObject("Eye").AddComponent<SpriteRenderer>();
                e.transform.SetParent(transform, false);
                e.transform.localPosition = new Vector2(i == 0 ? -0.09f : 0.09f, -0.07f);
                e.transform.localScale = Vector3.one * 0.05f;
                e.sprite = Shapes.Circle;
                e.color = new Color(0.65f, 1f, 0.3f);
                e.sortingOrder = sr.sortingOrder + 2;
                Lighting.Glow(e);
            }
        }
        foreach (SpriteRenderer e in eyes) if (e != null) e.enabled = on;
    }

    // Rabbit

    private void NextHop()
    {
        hopIndex++;
        hopStart = stateTime;
        stepDone = false; // landed yet?
        hopFrom = rb.position;
        Vector2 aim = (Vector2)player.position + playerVel * HopDuration * 0.5f;
        hopTo = hopFrom + Vector2.ClampMagnitude(aim - hopFrom, hopRange);
        AoeWarning.Spawn(hopTo, HopRadius, HopDuration);
        Play(run, 12f, false);
    }

    private bool LastHop => hopIndex >= hops - 1;
    private float HopDuration => hopTime * (LastHop && hops > 1 ? 1.3f : 1f);
    private float HopRadius => hopRadius * Size * (LastHop && hops > 1 ? 1.6f : 1f);

    private bool UpdateHops()
    {
        float k = (stateTime - hopStart) / HopDuration;
        if (k < 1f)
        {
            rb.MovePosition(Vector2.Lerp(hopFrom, hopTo, k));
            transform.localScale = baseScale * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
            return true;
        }
        if (!stepDone)
        {
            stepDone = true;
            transform.localScale = baseScale;
            Slam(hopTo, HopRadius, zapDamage * (LastHop && hops > 1 ? 1.4f : 1f));
        }
        if (stateTime < hopStart + HopDuration + 0.2f) return true;
        if (LastHop) return false;
        NextHop();
        return true;
    }

    private void Slam(Vector2 at, float radius, float damage)
    {
        StompFx.Spawn(at, radius);
        ExplosionFx.Ring(at, radius, Dirt, 0.3f);
        Juice.Smoke(at, radius * 0.6f, 6);
        CameraFollow.Shake(0.15f, 0.2f);
        Sfx.Play(Sfx.Id.Boom, 0.6f, 1.2f);
        if (playerHealth != null && Vector2.Distance(player.position, at) <= radius) playerHealth.TakeDamage(damage);
    }

    private bool UpdateBurrow(float t, Vector2 dir)
    {
        float popAt = BurrowDig + burrowTime;
        if (t < BurrowDig)
        {
            transform.localScale = baseScale * Mathf.Lerp(1f, 0.4f, t / BurrowDig);
            if (Random.value < 0.4f) Juice.Smoke(rb.position, 0.5f * Size, 1);
            return true;
        }
        if (!hidden && t < popAt)
        {
            SetHidden(true);
            mound = new GameObject("BurrowMound").AddComponent<SpriteRenderer>();
            mound.sprite = Shapes.Art("Fx/Boss/dirt_mound");
            mound.color = Color.white;
            mound.sortingOrder = 3;
            mound.transform.localScale = Vector3.one * 1.4f * Size;
        }
        if (t < popAt - PopWarn)
        {
            moveVelocity = dir * Mathf.Max(moveSpeed * 2.2f, 3.5f);
            if (Random.value < 0.5f) Juice.Smoke(rb.position, 0.35f * Size, 1);
        }
        else if (!swipeWarned)
        {
            swipeWarned = true;
            AoeWarning.Spawn(rb.position, hopRadius * Size, PopWarn);
        }
        if (mound != null) mound.transform.position = rb.position + Vector2.down * 0.2f * Size;

        if (t >= popAt && hidden)
        {
            SetHidden(false);
            transform.localScale = baseScale;
            if (mound != null) Destroy(mound.gameObject);
            Slam(rb.position, hopRadius * Size, zapDamage * 1.2f);
            Play(attack, zapFps, false);
        }
        return t < popAt + 0.4f;
    }

    private void SetHidden(bool on)
    {
        hidden = on;
        sr.enabled = !on;
        if (shadow != null) shadow.enabled = !on;
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = !on;
    }

    private void CheckBabies()
    {
        if (babyEvery <= 0f || babyPrefab == null || health == null || health.IsDead) return;
        if (health.CurrentHealth > health.maxHealth * nextBabyAt) return;
        nextBabyAt -= babyEvery;
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null) spawner.SpawnAround(rb.position, babyCount, 1.2f * Size, babyPrefab);
        Juice.Sparks(rb.position, new Color(1f, 0.8f, 0.9f), 8, 4f, 0.12f);
    }

    // Queen Bee

    private int AliveGuards()
    {
        guards.RemoveAll(g => g == null || g.IsDead || !g.gameObject.activeInHierarchy);
        return guards.Count;
    }

    private void SummonGuards()
    {
        if (summonPrefab == null) return;
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        int need = guardCount - AliveGuards();
        for (int i = 0; i < need; i++)
        {
            GameObject bee = Pool.Spawn(summonPrefab, rb.position + Random.insideUnitCircle * 0.5f, Quaternion.identity);
            if (!bee.TryGetComponent(out EnemyHealth h) || !bee.TryGetComponent(out EnemyAI ai)) continue;
            h.ScaleHealth((spawner != null ? spawner.HealthMultiplier : 1f) * guardHealth);
            ai.WakeUp();
            guards.Add(h);
            int life = h.Life;
            float offset = i * 360f / guardCount;
            ai.steer = d =>
            {
                // Let go once the queen is gone or this bee died and came back from the pool as someone else.
                if (this == null || state == State.Dead || h.Life != life || h.IsDead) { ai.steer = null; return d; }
                float a = (Time.time * 110f + offset) * Mathf.Deg2Rad;
                Vector2 slot = rb.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * guardRadius * Size;
                return Vector2.ClampMagnitude((slot - (Vector2)ai.transform.position) * 4f / Mathf.Max(0.5f, ai.moveSpeed), 4f);
            };
        }
    }
}
