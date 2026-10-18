using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossAI : MonoBehaviour, IEnemyBrain
{
    [System.Serializable]
    public class Dir4
    {
        public Sprite[] down, up, left, right;

        public Sprite[] Get(Vector2 d) =>
            Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x < 0f ? left : right) : (d.y < 0f ? down : up);
    }

    private enum State { Walk, Zap, Charge, Recover, Dead }

    public string bossName = "Beholder";
    public Dir4 idle, walk, run, attack, hurt, death;

    [Header("Movement")]
    public float moveSpeed = 1.6f;
    public float knockbackDecay = 30f;
    public float recycleDistance = 17f;

    [Header("Contact Damage")]
    public float contactDamage = 15f;
    public float contactRange = 0.9f;
    public float damageCooldown = 1f;

    [Header("Zap")]
    public float zapRange = 7f;
    public float zapDamage = 20f;
    public float zapRadius = 1.4f;
    public float zapCooldown = 4f;
    public int zapStrikeFrame = 6;
    public float zapFps = 10f;
    public int zapStrikes = 1;
    public float zapSpread = 2.5f;

    [Header("Charge")]
    public float chargeSpeed = 8f;
    public float chargeWindup = 0.5f;
    public float chargeDuration = 0.9f;
    public float chargeCooldown = 7f;
    public int chargeRepeats = 1;

    [Header("Bullet Ring (fired as each charge ends; 0 bullets = off)")]
    public int ringBullets = 0;
    public int ringWaves = 1;
    public float ringSpeed = 5f;
    public float ringDamage = 15f;

    [Header("Enrage (below this HP fraction: faster attacks, extra ring wave; 0 = off)")]
    [Range(0f, 1f)] public float enrageBelow = 0f;
    public float enrageSpeedup = 1.4f;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Transform player;
    private PlayerHealth playerHealth;

    private State state;
    private float stateTime;
    private float zapTimer = 2f;
    private float chargeTimer = 5f;
    private float damageTimer;
    private float hurtTimer;
    private Vector2 knockback;
    private Vector2 facing = Vector2.down;
    private Vector2 chargeDir;
    private bool chargeLocked;
    private int chargesLeft;
    private readonly System.Collections.Generic.List<Vector2> zapTargets = new System.Collections.Generic.List<Vector2>();
    private bool struck;
    private EnemyHealth health;
    private bool enraged;
    private Vector2 playerVel, lastPlayerPos;
    private float slowAmount, slowTimer;

    private Dir4 clip;
    private float clipFps;
    private bool clipLoop;
    private float animTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        sr = GetComponent<SpriteRenderer>();
        health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        OffscreenArrows.Track(transform, new Color(1f, 0.3f, 0.3f));
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            lastPlayerPos = player.position;
            playerHealth = p.GetComponent<PlayerHealth>();
            foreach (Collider2D mine in GetComponents<Collider2D>())
                foreach (Collider2D theirs in p.GetComponents<Collider2D>())
                    Physics2D.IgnoreCollision(mine, theirs);
        }
        SetState(State.Walk);
    }

    public bool IsImpaired => slowTimer > 0f;

    public void Slow(float amount, float seconds)
    {
        slowAmount = Mathf.Max(slowAmount, Mathf.Clamp01(amount) * 0.5f);
        slowTimer = Mathf.Max(slowTimer, seconds);
    }

    public void Knockback(Vector2 impulse)
    {
        knockback = impulse;
        hurtTimer = 0.2f;
    }

    public void Die()
    {
        SetState(State.Dead);
        rb.simulated = false;
        CameraFollow.Shake(0.25f, 0.4f);
        ExplosionFx.Spawn(transform.position, 2.5f);
        Destroy(gameObject, death.down.Length / 10f + 0.4f);
    }

    private void SetState(State next)
    {
        state = next;
        stateTime = 0f;
        switch (next)
        {
            case State.Walk: Play(walk, 8f, true); break;
            case State.Recover: Play(idle, 10f, true); break;
            case State.Charge: Play(run, 12f, true); chargeDir = facing; chargeLocked = false; break;
            case State.Dead: Play(death, 10f, false); break;
            case State.Zap:
                Play(attack, zapFps, false);
                struck = false;
                float delay = zapStrikeFrame / zapFps;
                zapTargets.Clear();
                zapTargets.Add((Vector2)player.position + (zapStrikes > 1 ? playerVel * delay : Vector2.zero));
                for (int i = 1; i < zapStrikes; i++) zapTargets.Add((Vector2)player.position + Random.insideUnitCircle * zapSpread);
                foreach (Vector2 t in zapTargets) AoeWarning.Spawn(t, zapRadius, delay);
                break;
        }
    }

    private void FireRing()
    {
        if (ringBullets <= 0 || playerHealth == null) return;
        int waves = ringWaves + (enraged ? 1 : 0);
        for (int w = 0; w < waves; w++)
        {
            float offset = w * 180f / ringBullets;
            float speed = ringSpeed * (1f - 0.2f * w);
            for (int i = 0; i < ringBullets; i++)
            {
                float a = (offset + i * 360f / ringBullets) * Mathf.Deg2Rad;
                EnemyProjectile.Spawn(rb.position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, ringDamage, playerHealth, 0.45f, new Color(0.75f, 0.35f, 1f));
            }
        }
    }

    private void CheckEnrage()
    {
        if (enraged || enrageBelow <= 0f || health == null || health.CurrentHealth >= health.maxHealth * enrageBelow) return;
        enraged = true;
        DamagePopup.SpawnText(rb.position + Vector2.up * 1.5f, "ENRAGED!", new Color(1f, 0.3f, 0.3f), 6f);
        ExplosionFx.Spawn(rb.position, 3f, new Color(1f, 0.25f, 0.2f));
        CameraFollow.Shake(0.2f, 0.3f);
    }

    private void Play(Dir4 c, float fps, bool loop)
    {
        clip = c;
        clipFps = fps;
        clipLoop = loop;
        animTime = 0f;
    }

    private int Frame => (int)(animTime * clipFps);

    private void Update()
    {
        if (damageTimer > 0f) damageTimer -= Time.deltaTime;
        if (hurtTimer > 0f) hurtTimer -= Time.deltaTime;
        animTime += Time.deltaTime;

        bool showHurt = hurtTimer > 0f && state == State.Walk;
        Sprite[] frames = (showHurt ? hurt : clip).Get(facing);
        if (frames == null || frames.Length == 0) return;
        int i = showHurt ? (int)(Time.time * 20f) % frames.Length : Frame;
        sr.sprite = SpriteFx.Outlined(frames[clipLoop || showHurt ? i % frames.Length : Mathf.Min(i, frames.Length - 1)]);

        if (state == State.Dead)
        {
            float fadeStart = death.down.Length / 10f;
            Color c = sr.color;
            c.a = 1f - Mathf.Clamp01((animTime - fadeStart) / 0.4f);
            sr.color = c;
        }
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || player == null) return;

        float dt = Time.fixedDeltaTime;
        stateTime += dt;

        Vector2 pp = player.position;
        playerVel = Vector2.Lerp(playerVel, Vector2.ClampMagnitude((pp - lastPlayerPos) / dt, 8f), 0.3f);
        lastPlayerPos = pp;

        CheckEnrage();
        float pace = enraged ? enrageSpeedup : 1f;
        zapTimer -= dt * pace;
        chargeTimer -= dt * pace;

        Vector2 toPlayer = (Vector2)player.position - rb.position;
        float dist = toPlayer.magnitude;
        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.down;
        Vector2 velocity = Vector2.zero;

        if (dist > recycleDistance)
        {
            rb.position = (Vector2)player.position + dir * 12f;
            rb.linearVelocity = knockback = Vector2.zero;
            return;
        }

        switch (state)
        {
            case State.Walk:
                facing = dir;
                velocity = dir * moveSpeed * (enraged ? 1.25f : 1f);
                if (zapTimer <= 0f && dist < zapRange) SetState(State.Zap);
                else if (chargeTimer <= 0f && dist > 3f) { chargesLeft = chargeRepeats; SetState(State.Charge); }
                break;

            case State.Zap:
                facing = zapTargets[0] - rb.position;
                if (!struck && Frame >= zapStrikeFrame)
                {
                    struck = true;
                    bool hit = false;
                    foreach (Vector2 t in zapTargets)
                    {
                        ExplosionFx.Spawn(t, zapRadius);
                        hit |= Vector2.Distance(player.position, t) <= zapRadius;
                    }
                    CameraFollow.Shake(0.12f, 0.15f);
                    if (hit && playerHealth != null) playerHealth.TakeDamage(zapDamage);
                }
                if (Frame >= attack.down.Length) { zapTimer = zapCooldown; SetState(State.Recover); }
                break;

            case State.Charge:
                float lockAt = chargeWindup * 0.35f;
                if (stateTime < lockAt) { facing = dir; chargeDir = dir; }
                else if (!chargeLocked)
                {
                    chargeLocked = true;
                    AoeWarning.SpawnLine(rb.position, chargeDir, chargeSpeed * chargeDuration + contactRange, contactRange * 2f, chargeWindup - lockAt);
                }
                if (stateTime >= chargeWindup) velocity = chargeDir * chargeSpeed;
                if (stateTime >= chargeWindup + chargeDuration)
                {
                    FireRing();
                    if (--chargesLeft > 0) SetState(State.Charge);
                    else { chargeTimer = chargeCooldown; SetState(State.Recover); }
                }
                break;

            case State.Recover:
                facing = dir;
                if (stateTime >= 0.6f) SetState(State.Walk);
                break;
        }

        if (slowTimer > 0f)
        {
            slowTimer -= dt;
            velocity *= 1f - slowAmount;
            if (slowTimer <= 0f) slowAmount = 0f;
        }

        rb.linearVelocity = velocity + knockback;
        knockback = Vector2.MoveTowards(knockback, Vector2.zero, knockbackDecay * dt);

        if (damageTimer <= 0f && playerHealth != null && dist <= contactRange)
        {
            playerHealth.TakeDamage(contactDamage * AuraWeapon.ContactScale);
            damageTimer = damageCooldown;
        }
    }
}
