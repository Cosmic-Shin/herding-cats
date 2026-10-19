using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyAnimator))]
public class EnemyAI : MonoBehaviour, IEnemyBrain, IPoolable
{
    public enum Behavior { Chase, Leap, Ranged }
    private enum State { Spawning, Sleeping, Waking, Chasing, Leaping, Shooting, Hurt, Dead }

    public Behavior behavior = Behavior.Chase;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float knockbackDecay = 25f;

    [Header("Contact Damage")]
    public float contactDamage = 10f;
    public float contactRange = 0.5f;
    public float damageCooldown = 1f;

    [Header("Leap")]
    public float leapRange = 3.5f;
    public float leapSpeed = 9f;
    public float leapCooldown = 2.5f;
    public float leapFps = 14f;

    [Header("Belly Flop (Leap only; 0 radius = off)")]
    public float slamRadius = 0f;
    public float slamDamage = 20f;
    public float slamArmorTime = 1f;

    [Header("Ranged")]
    public float shootRange = 5f;
    public float shootCooldown = 3f;
    public float projectileSpeed = 5f;
    public float projectileDamage = 8f;
    public int fireFrame = 3;
    public int hairballSplit = 0;
    public float kiteRange = 0f;
    public bool spitPuddles;
    public float attackFps = 20f;

    [Header("Charger (Leap only)")]
    public bool chargeLine;

    [System.NonSerialized] public System.Func<Vector2, Vector2> steer;

    [Header("Shield cat")]
    public bool frontShield;

    [Header("Sleep")]
    [Range(0f, 1f)] public float sleepChance = 0f;
    public float wakeRange = 2.2f;
    public float wakeSpeedMultiplier = 1.6f;
    public float wakeBoostDuration = 2.5f;

    [Header("Recycling")]
    public float recycleDistance = 17f;

    public bool IsAsleep => state == State.Sleeping;
    public bool IsImpaired => slowTimer > 0f || stunTimer > 0f;
    public static int AliveCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AliveCount = 0;

    private void OnEnable() => AliveCount++;
    private void OnDisable()
    {
        AliveCount--;
        if (aimLine != null) aimLine.enabled = false;
    }

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private EnemyAnimator anim;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    private State state;
    private float stateTime;
    private float stateDuration;
    private float abilityTimer;
    private float damageTimer;
    private Vector2 knockback;
    private Vector2 leapDir;
    private bool fired;
    private float boostTimer;
    private float stunTimer;
    private float slowAmount, slowTimer;
    private Vector2 pull;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<EnemyAnimator>();
        health = GetComponent<EnemyHealth>();
        baseScale = transform.localScale;
        if (frontShield) shield = MakeShield();
        if (behavior == Behavior.Ranged) aimLine = MakeAimLine();
    }

    private Vector3 baseScale;
    private EnemyHealth health;
    private float hitSquash;
    private const float HitSquashTime = 0.12f;

    public void HitSquash() => hitSquash = HitSquashTime;

    private float bobPhase;
    private float knockTrail;
    private bool wasWindUp;
    private static readonly Color WindUpRed = new Color(1f, 0.3f, 0.3f);
    private static readonly Color Dust = new Color(0.85f, 0.78f, 0.65f);
    private Color BaseColor => health != null ? health.BaseColor : Color.white;

    private void UpdateLook()
    {
        hitSquash = Mathf.Max(0f, hitSquash - Time.deltaTime);
        float hs = hitSquash / HitSquashTime;
        Vector2 s = new Vector2(1f + 0.25f * hs, 1f - 0.2f * hs);

        if (state == State.Spawning) s *= Mathf.SmoothStep(0.3f, 1f, stateTime / 0.25f);
        else if (state == State.Chasing && rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            float hop = Mathf.Abs(Mathf.Sin((Time.time + bobPhase) * 9f));
            s.x *= 1f - 0.05f * hop;
            s.y *= 1f + 0.08f * hop;
        }

        bool windUp = (state == State.Leaping && stateTime < 0.3f * stateDuration) || (state == State.Shooting && !fired);
        if (windUp)
        {
            s.x *= 1.12f;
            s.y *= 0.88f;
            if (frozenTimer <= 0f) sr.color = Color.Lerp(BaseColor, WindUpRed, 0.5f + 0.5f * Mathf.Sin(stateTime * 40f));
        }
        else if (wasWindUp && frozenTimer <= 0f && armorTimer <= 0f) sr.color = BaseColor;
        wasWindUp = windUp;

        if (frozenTimer > 0f)
        {
            if (frozenTimer < 0.35f) s.x *= 1f + 0.06f * Mathf.Sin(Time.time * 80f);
            if (Random.value < 0.05f) Juice.Sparks((Vector2)transform.position + Random.insideUnitCircle * 0.3f, new Color(0.8f, 0.95f, 1f), 1, 0.5f, 0.1f);
        }

        if (state == State.Waking)
        {
            float j = Mathf.Sin(Mathf.PI * Mathf.Clamp01(stateTime / 0.2f));
            s.x *= 1f - 0.15f * j;
            s.y *= 1f + 0.35f * j;
        }
        else if (state == State.Sleeping && Random.value < Time.deltaTime * 0.6f)
            DamagePopup.SpawnText((Vector2)transform.position + new Vector2(0.25f, 0.35f), "z", new Color(0.8f, 0.8f, 1f), 2.5f);

        float leapP = state == State.Leaping ? stateTime / stateDuration : 0f;
        if (leapP > 0.3f && leapP < 0.8f && (leapTrailTimer -= Time.deltaTime) <= 0f)
        {
            leapTrailTimer = 0.05f;
            AfterImage.Spawn(sr, new Color(1f, 0.5f, 0.45f, 0.5f));
        }

        if (aimLine != null)
        {
            bool aiming = state == State.Shooting && !fired && playerTransform != null;
            aimLine.enabled = aiming;
            if (aiming)
            {
                Vector2 to = (Vector2)playerTransform.position - (Vector2)transform.position;
                aimLine.transform.position = transform.position;
                aimLine.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
                aimLine.transform.localScale = new Vector3(Mathf.Min(to.magnitude, shootRange), 0.06f, 1f);
                aimLine.color = new Color(1f, 0.3f, 0.3f, 0.25f + 0.25f * Mathf.Sin(stateTime * 30f));
            }
        }

        if (knockTrail > 0f)
        {
            knockTrail -= Time.deltaTime;
            if (Random.value < 0.5f) Juice.Sparks(transform.position, new Color(1f, 1f, 1f, 0.45f), 1, 0.2f, 0.22f);
        }

        transform.localScale = Vector3.Scale(baseScale, new Vector3(s.x, s.y, 1f));
    }

    private float leapTrailTimer;
    private SpriteRenderer aimLine;

    public enum EliteMod { None, Swift, Volatile, Splitter, Juggernaut }
    public EliteMod Mod { get; private set; }
    private float speedMul = 1f;
    private const float EliteScale = 1.35f;
    private bool elite;
    private Color AuraColor => BaseColor != Color.white ? BaseColor : new Color(1f, 0.35f, 0.3f);

    public void MakeElite()
    {
        if (elite) return;
        elite = true;
        baseScale *= EliteScale;
        transform.localScale = baseScale;
        Mod = (EliteMod)Random.Range(1, 5);
        if (Mod == EliteMod.Swift) speedMul = 1.6f;
        if (Mod == EliteMod.Juggernaut) { unstoppable = true; health.ScaleHealth(1.5f); }
        anim.SetAura(Mod switch
        {
            EliteMod.Swift => new Color(0.35f, 0.9f, 1f),
            EliteMod.Volatile => new Color(1f, 0.55f, 0.15f),
            EliteMod.Splitter => new Color(0.5f, 1f, 0.4f),
            _ => new Color(0.75f, 0.4f, 1f),
        });
    }

    private SpriteRenderer MakeAimLine()
    {
        var line = new GameObject("AimLine").AddComponent<SpriteRenderer>();
        line.sprite = Shapes.Square;
        line.sortingOrder = sr.sortingOrder - 1;
        line.enabled = false;
        return line;
    }

    private void OnDestroy() { if (aimLine != null) Destroy(aimLine.gameObject); }

    private void BreakIce()
    {
        FrostNovaWeapon.ShardBurst(transform.position, 0.9f, 5);
        Juice.Sparks(transform.position, new Color(0.8f, 0.95f, 1f), 6, 3f, 0.12f);
    }
    private float armorTimer;
    private Vector2 lookDir = Vector2.down;
    private Transform shield;
    private bool slamWarned, slammed;

    public bool IsArmored => armorTimer > 0f;

    private bool swarm;
    private Vector2 swarmDir;
    private float swarmSpeed, swarmAge;
    private const float SwarmMaxAge = 20f;

    public void Swarm(Vector2 dir, float speed)
    {
        swarm = true;
        swarmDir = dir.normalized;
        swarmSpeed = speed;
        swarmAge = 0f;
        SetState(State.Chasing);
    }

    private static GameObject cachedPlayer;
    private bool initialized;

    private void Start()
    {
        if (!initialized)
        {
            baseScale = transform.localScale;
            bool wasElite = elite;
            OnSpawn();
            if (wasElite) MakeElite();
            if (health != null && health.xpGemCount > 1) anim.SetAura(AuraColor);
        }
    }

    public void OnSpawn()
    {
        initialized = true;

        if (cachedPlayer == null) cachedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (cachedPlayer != null)
        {
            playerTransform = cachedPlayer.transform;
            playerHealth = cachedPlayer.GetComponent<PlayerHealth>();

            foreach (Collider2D mine in GetComponents<Collider2D>())
                foreach (Collider2D theirs in cachedPlayer.GetComponents<Collider2D>())
                    Physics2D.IgnoreCollision(mine, theirs);
        }

        rb.simulated = true;
        if (elite)
        {
            elite = false;
            baseScale /= EliteScale;
            if (Mod == EliteMod.Juggernaut) unstoppable = false;
            Mod = EliteMod.None;
            speedMul = 1f;
        }
        transform.localScale = baseScale;
        rb.linearVelocity = Vector2.zero;
        knockback = Vector2.zero;
        stunTimer = boostTimer = damageTimer = slowAmount = slowTimer = frozenTimer = armorTimer = hitSquash = knockTrail = 0f;
        wasWindUp = false;
        bobPhase = Random.value * 10f;
        anim.SetAura(health != null && health.BaseGemCount > 1 ? AuraColor : (Color?)null);
        swarm = false;
        pull = Vector2.zero;
        fired = false;
        sr.flipX = false;
        Color c = sr.color;
        c.a = 1f;
        sr.color = c;

        abilityTimer = Random.Range(0.5f, 1.5f);
        SetState(Random.value < sleepChance ? State.Sleeping : State.Spawning);
    }

    public bool unstoppable;

    public void Stun(float seconds)
    {
        if (state == State.Dead) return;
        if (unstoppable) seconds *= 0.5f;
        stunTimer = Mathf.Max(stunTimer, seconds);
        if (state == State.Sleeping) SetState(State.Waking);
    }

    public void Slow(float amount, float seconds)
    {
        if (unstoppable) amount *= 0.5f;
        slowAmount = Mathf.Max(slowAmount, Mathf.Clamp01(amount));
        slowTimer = Mathf.Max(slowTimer, seconds);
    }

    public void Freeze(float seconds)
    {
        Stun(seconds);
        frozenTimer = Mathf.Max(frozenTimer, seconds);
    }

    public bool IsFrozen => frozenTimer > 0f;
    private float frozenTimer;

    public void WakeUp()
    {
        if (state == State.Sleeping) SetState(State.Waking);
    }

    private const float MaxPull = 7f;
    public void Pull(Vector2 velocity)
    {
        if (Time.deltaTime <= 0f) return;
        if (unstoppable) velocity *= 0.5f;
        pull = Vector2.ClampMagnitude(pull + velocity, MaxPull);
    }

    public void Knockback(Vector2 impulse)
    {
        if (unstoppable) return;
        knockback = impulse;
        knockTrail = 0.15f;
        if (state == State.Sleeping) SetState(State.Waking);
        else if (state == State.Chasing) SetState(State.Hurt);
    }

    public void Die()
    {
        SetState(State.Dead);
        if (aimLine != null) aimLine.enabled = false;
        rb.simulated = false;
    }

    public bool Blocks(Vector2 source)
    {
        if (!frontShield || state == State.Dead) return false;
        Vector2 to = source - rb.position;
        return to.sqrMagnitude > 0.36f && Vector2.Angle(lookDir, to) < 60f;
    }

    private Transform MakeShield()
    {
        var go = new GameObject("Shield");
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = Shapes.Square;
        s.color = new Color(0.75f, 0.85f, 1f);
        s.sortingOrder = sr.sortingOrder + 1;
        return go.transform;
    }

    private void BellyFlop()
    {
        ExplosionFx.Spawn(rb.position, slamRadius, new Color(0.85f, 0.65f, 0.45f));
        CameraFollow.Shake(0.15f, 0.2f);
        if (playerHealth != null && Vector2.Distance(playerTransform.position, rb.position) <= slamRadius)
            playerHealth.TakeDamage(slamDamage);
        armorTimer = slamArmorTime;
        DamagePopup.SpawnText(rb.position + Vector2.up, "BOING!", new Color(1f, 0.8f, 0.5f), 5f);
    }

    private const float DeathFade = 0.3f;

    private void SetState(State next)
    {
        state = next;
        stateTime = 0f;
        switch (next)
        {
            case State.Spawning:
                stateDuration = anim.Play(anim.born, 12f);
                Juice.CatPuff(transform.position, Dust);
                break;
            case State.Sleeping: anim.Play(anim.sleep, 4f, true); break;
            case State.Waking:
                stateDuration = anim.Play(anim.wake, 16f);
                boostTimer = wakeBoostDuration;
                DamagePopup.SpawnText(transform.position, "!", new Color(1f, 0.3f, 0.2f), 5f);
                break;
            case State.Chasing: anim.Play(anim.walk, 8f, true); break;
            case State.Leaping:
                stateDuration = anim.Play(anim.jump, leapFps);
                slamWarned = slammed = false;
                Juice.CatTalk(transform.position, Juice.Talk.Leap, 0.3f);
                if (chargeLine && playerTransform != null)
                {
                    leapDir = ((Vector2)playerTransform.position - rb.position).normalized;
                    AoeWarning.SpawnLine(rb.position, leapDir, leapSpeed * 0.5f * stateDuration, 0.9f, 0.3f * stateDuration);
                }
                break;
            case State.Shooting: stateDuration = anim.Play(anim.attack, attackFps); fired = false; break;
            case State.Hurt: stateDuration = anim.Play(anim.hurt, 30f); break;
            case State.Dead: stateDuration = anim.Play(anim.death, 15f); break;
        }
    }

    private void Update()
    {
        if (damageTimer > 0) damageTimer -= Time.deltaTime;

        if (shield != null)
        {
            Vector2 across = Vector2.Perpendicular(lookDir);
            shield.localPosition = lookDir * 0.42f - across * 0.36f;
            shield.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg);
            shield.localScale = new Vector3(0.72f, 0.13f, 1f);
            shield.gameObject.SetActive(state != State.Dead);
        }

        if ((frozenTimer > 0f || armorTimer > 0f) && state != State.Dead)
        {
            if (frozenTimer > 0f && (frozenTimer -= Time.deltaTime) <= 0f) BreakIce();
            if (armorTimer > 0f) armorTimer -= Time.deltaTime;
            sr.color = frozenTimer > 0f ? new Color(0.55f, 0.85f, 1f)
                     : armorTimer > 0f ? new Color(1f, 0.75f, 0.45f)
                     : (health != null ? health.BaseColor : Color.white);
        }

        if (state != State.Dead) UpdateLook();

        if (state == State.Dead)
        {
            stateTime += Time.deltaTime;
            float k = Mathf.Clamp01(stateTime / 0.15f);
            transform.localScale = Vector3.Scale(baseScale, new Vector3(Mathf.Lerp(1.35f, 1f, k), Mathf.Lerp(0.65f, 1f, k), 1f));
            Color c = sr.color;
            c.a = 1f - Mathf.Clamp01((stateTime - stateDuration) / DeathFade);
            sr.color = c;
            if (stateTime >= stateDuration + DeathFade) Pool.Despawn(gameObject);
        }
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || playerTransform == null) return;

        float dt = Time.fixedDeltaTime;
        stateTime += dt;
        abilityTimer -= dt;
        if (state != State.Waking) boostTimer -= dt;

        Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
        float dist = toPlayer.magnitude;
        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.zero;
        Vector2 velocity = Vector2.zero;

        if (swarm)
        {
            swarmAge += dt;
            bool pastPlayer = Vector2.Dot(swarmDir, rb.position - (Vector2)playerTransform.position) > 0f;
            if ((pastPlayer && dist > recycleDistance) || swarmAge > SwarmMaxAge) { Pool.Despawn(gameObject); return; }
        }
        else if (dist > recycleDistance)
        {
            Vector2 ahead = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f)) * dir;
            rb.position = (Vector2)playerTransform.position + ahead * 12f;
            rb.linearVelocity = knockback = pull = Vector2.zero;
            return;
        }

        if (stunTimer > 0f)
        {
            stunTimer -= dt;
            rb.linearVelocity = knockback + pull;
            pull = Vector2.zero;
            knockback = Vector2.MoveTowards(knockback, Vector2.zero, knockbackDecay * dt);
            return;
        }

        switch (state)
        {
            case State.Sleeping:
                if (dist < wakeRange) SetState(State.Waking);
                break;

            case State.Spawning:
            case State.Waking:
            case State.Hurt:
                if (stateTime >= stateDuration) SetState(State.Chasing);
                break;

            case State.Chasing:
                if (swarm)
                {
                    anim.Play(anim.walk, 8f, true);
                    velocity = swarmDir * swarmSpeed;
                }
                else if (kiteRange > 0f && dist < kiteRange)
                {
                    anim.Play(anim.walk, 8f, true);
                    velocity = -dir * moveSpeed * speedMul;
                }
                else if (behavior == Behavior.Leap && abilityTimer <= 0f && dist < leapRange)
                {
                    SetState(State.Leaping);
                }
                else if (behavior == Behavior.Ranged && dist < shootRange)
                {
                    if (abilityTimer <= 0f) SetState(State.Shooting);
                    else anim.Play(anim.idle, 8f, true);
                }
                else
                {
                    anim.Play(anim.walk, 8f, true);
                    Vector2 move = steer != null ? steer(dir) : dir;
                    velocity = move * moveSpeed * speedMul * EnemySpawner.SpeedScale * (boostTimer > 0f ? wakeSpeedMultiplier : 1f);
                }
                break;

            case State.Leaping:
                float p = stateTime / stateDuration;
                if (p < 0.3f) { if (!chargeLine) leapDir = dir; }
                else if (p < 0.8f)
                {
                    velocity = leapDir * leapSpeed;
                    if (slamRadius > 0f && !slamWarned)
                    {
                        slamWarned = true;
                        float lunge = 0.5f * stateDuration;
                        AoeWarning.Spawn(rb.position + leapDir * leapSpeed * lunge, slamRadius, lunge);
                    }
                }
                else if (slamRadius > 0f && !slammed) { slammed = true; BellyFlop(); }
                if (p >= 1f) { abilityTimer = leapCooldown; SetState(State.Chasing); }
                break;

            case State.Shooting:
                if (!fired && anim.Frame >= fireFrame)
                {
                    fired = true;
                    Vector2 mouth = rb.position + dir * 0.3f;
                    if (hairballSplit > 0) EnemyProjectile.SpawnHairball(mouth, playerTransform.position, 0.9f, projectileDamage * 1.5f, hairballSplit, projectileDamage, playerHealth);
                    else if (spitPuddles) EnemyProjectile.SpawnToxic(mouth, dir * projectileSpeed, projectileDamage, playerHealth);
                    else EnemyProjectile.Spawn(mouth, dir * projectileSpeed, projectileDamage, playerHealth);
                }
                if (stateTime >= stateDuration) { abilityTimer = shootCooldown; SetState(State.Chasing); }
                break;
        }

        Vector2 facing = state == State.Leaping ? leapDir : swarm ? swarmDir : steer != null && velocity.sqrMagnitude > 0.01f ? velocity.normalized : dir;
        if (facing.sqrMagnitude > 0.01f) lookDir = facing;
        if (state != State.Sleeping && Mathf.Abs(facing.x) > 0.1f) sr.flipX = facing.x < 0;

        if (slowTimer > 0f)
        {
            slowTimer -= dt;
            velocity *= 1f - slowAmount;
            if (slowTimer <= 0f) slowAmount = 0f;
        }

        rb.linearVelocity = velocity + knockback + pull;
        pull = Vector2.zero;
        knockback = Vector2.MoveTowards(knockback, Vector2.zero, knockbackDecay * dt);

        if (contactDamage > 0f && state != State.Spawning && state != State.Sleeping && damageTimer <= 0f && playerHealth != null && dist <= contactRange)
        {
            playerHealth.TakeDamage(contactDamage * AuraWeapon.ContactScale);
            damageTimer = damageCooldown;
        }
    }
}
