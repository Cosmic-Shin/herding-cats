using UnityEngine;
using UnityEngine.InputSystem;

// Handles input + movement only. Reads speed from PlayerStats so upgrades
// automatically apply here. Animation lives in PlayerAnimator.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerStats stats;
    private Vector2 moveInput;
    private Vector2 lastMoveDir = Vector2.down;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector2 dashDirection;

    [Header("Dash (Shift / Space)")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.25f;
    public float dashCooldown = 0.6f;

    [Header("Blink (skill: dash becomes a teleport)")]
    public int blinkLevel;                 // 0 = normal dash; set by PlayerSkills
    public float blinkMinDistance = 3f;
    public float blinkMaxDistance = 5.5f;
    public float blinkClearRadius = 1.6f;  // landing spot must have no enemies this close
    public float blinkCooldown = 4f;       // much longer than a dash: a free, safe teleport is strong

    private float invulnTimer;
    private static readonly System.Collections.Generic.List<Collider2D> scratch = new System.Collections.Generic.List<Collider2D>(32);
    private SpriteRenderer sr;
    private CircleCollider2D body;

    public bool IsDashing => isDashing;
    public bool IsInvulnerable => isDashing || invulnTimer > 0f;
    public float CooldownRemaining => dashCooldownTimer;
    public float CooldownTotal { get; private set; } = 1f; // length of the cooldown currently running
    public Vector2 MoveInput => moveInput;
    public Vector2 DashDirection => dashDirection;
    public float DashProgress => Mathf.Clamp01(1f - dashTimer / dashDuration); // a dash stretched over a rock starts below 0

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<CircleCollider2D>();
        stats = GetComponent<PlayerStats>();
        sr = GetComponent<SpriteRenderer>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // smooth camera follow
    }

    private void Update()
    {
        moveInput = ReadMoveInput();
        if (moveInput.sqrMagnitude > 0.01f) lastMoveDir = moveInput;

        var kb = Keyboard.current;
        bool dashPressed = (kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                           || GamepadUI.Pressed(g => g.buttonSouth) || (Joystick.current != null && Joystick.current.trigger.wasPressedThisFrame);
        if (dashPressed && Time.timeScale > 0f && !isDashing && dashCooldownTimer <= 0f) // no blinking while paused
        {
            BeginDash();
        }

        if (dashCooldownTimer > 0f && (dashCooldownTimer -= Time.deltaTime) <= 0f && CooldownTotal >= 1f)
        {
            // ready again: a little sparkle (only for long cooldowns like Blink; a 0.6s dash would ping constantly)
            Juice.Sparks(transform.position, new Color(0.6f, 0.9f, 1f), 6, 2.5f, 0.1f);
            if (TryGetComponent(out PlayerAnimator readyAnim)) readyAnim.Squash(new Vector2(0.92f, 1.1f), 0.12f);
        }
        if (invulnTimer > 0f) invulnTimer -= Time.deltaTime;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) isDashing = false;
        }
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;
        var kb = Keyboard.current;

        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1;
        }

        if (input == Vector2.zero) input = GamepadUI.Move(); // stick, d-pad or generic joystick

        return input.normalized; // stops diagonal from being faster
    }

    private void FixedUpdate()
    {
        Vector2 velocity = isDashing ? dashDirection * dashSpeed : moveInput * stats.MoveSpeed;
        rb.MovePosition(rb.position + Slide(rb.position, velocity * Time.fixedDeltaTime, isDashing ? dashMask : MapGenerator.ObstacleMask));
    }

    // Dashes hop low obstacles (rocks, stumps, logs) but never ponds or tree trunks. Decided when the dash starts:
    // if she'd land inside a rock, the dash runs a little longer to clear it; if it can't clear it (a trunk or
    // pond right behind, or the rock is too big), the rock blocks this dash like it blocks walking.
    private int dashMask;
    private const float MaxDashOvershoot = 1.5f;

    private int DashMask(Vector2 dir, float length)
    {
        if (!MapGenerator.Active || body == null) return 0;
        float radius = FeetRadius;
        Vector2 start = rb.position + FeetOffset;
        RaycastHit2D tall = Physics2D.CircleCast(start, radius - Skin, dir, length + MaxDashOvershoot, MapGenerator.TallMask);
        float limit = tall ? tall.distance - Skin * 2f : length + MaxDashOvershoot;

        for (float d = Mathf.Min(length, limit); d <= limit + 0.001f; d += 0.1f)
        {
            Vector2 land = start + dir * d;
            bool inside = MapGenerator.ClampInside(land - FeetOffset, MapGenerator.WallPad) == land - FeetOffset;
            if (inside && Physics2D.OverlapCircle(land, radius + 0.25f, MapGenerator.ObstacleMask) == null) // +0.25: room for a physics step of overshoot
            {
                dashTimer += Mathf.Max(0f, d - length) / dashSpeed; // run on just far enough to land clear
                return MapGenerator.TallMask;
            }
        }
        return MapGenerator.ObstacleMask;
    }

    // The body is kinematic (physics never pushes Mira), so map obstacles are handled here: cast the step
    // one axis at a time against the Obstacle layer, so she stops at ponds / rocks / trunks and slides along
    // them, then keep her inside the map's wall.
    private const float Skin = 0.02f;
    // Obstacles are checked against her feet, not her hitbox (which sits at her middle), so her feet meet the
    // rock or trunk instead of overlapping it, like most top-down games.
    public static readonly Vector2 FeetOffset = new Vector2(0f, -0.5f);
    public const float FeetRadius = 0.3f;

    private Vector2 Slide(Vector2 pos, Vector2 delta, int mask)
    {
        if (!MapGenerator.Active || body == null) return delta;
        float radius = FeetRadius - Skin;
        Vector2 center = pos + FeetOffset;

        Vector2 Step(Vector2 d)
        {
            if (d.sqrMagnitude < 1e-10f) return Vector2.zero;
            RaycastHit2D hit = Physics2D.CircleCast(center, radius, d.normalized, d.magnitude + Skin, mask);
            if (!hit || hit.distance <= 0f) return d; // distance 0 = already overlapping: let her walk out
            return d.normalized * Mathf.Max(0f, hit.distance - Skin);
        }
        Vector2 x = Step(new Vector2(delta.x, 0f));
        center += x;
        Vector2 y = Step(new Vector2(0f, delta.y));
        return MapGenerator.ClampInside(pos + x + y, MapGenerator.WallPad) - pos;
    }

    private void BeginDash()
    {
        if (blinkLevel > 0)
        {
            Blink();
            return;
        }

        dashDirection = lastMoveDir;
        isDashing = true;
        dashTimer = dashDuration;
        dashMask = DashMask(dashDirection.normalized, dashSpeed * dashDuration);
        Sfx.Play(Sfx.Id.Dash, 0.7f);
        dashCooldownTimer = CooldownTotal = dashCooldown;
    }

    // Lv1: teleport to a random enemy-free spot nearby
    // Lv2: -25% cooldown (4s -> 3s), 0.6s invulnerability after landing (Lv1: 0.3s)
    // Lv3: bomb blast at the departure point (200% attack damage)
    private void Blink()
    {
        Vector2 from = rb.position;
        Vector2 to = MapGenerator.PlaceDrop(FindSafeSpot(from)); // inside the wall, out of ponds and rocks
        Color blinkColor = new Color(0.5f, 0.85f, 1f);
        Sfx.Play(Sfx.Id.Blink, 0.7f);

        if (sr != null) AfterImage.Spawn(sr);
        ExplosionFx.Spawn(from, 0.7f, blinkColor);
        Juice.Smoke(from, 1.2f, 6);
        Juice.Sparks(from, blinkColor, 10, 5f, 0.14f);

        if (PlayerCollabs.Has("blinkmines") && TryGetComponent(out MineWeapon mines)) mines.DropBurst(from);

        if (blinkLevel >= 3)
        {
            DamageStats.Source = "Blink";
            Bomb.Blast(from, 1.8f, stats.AttackDamage * 2f, stats.critChance, stats.critDamage, secondary: true);
        }

        rb.position = to;
        transform.position = to; // skip interpolation so it reads as a teleport, not a slide
        ExplosionFx.Spawn(to, 0.7f, blinkColor);
        Juice.Smoke(to, 1f, 4);
        Juice.Sparks(to, blinkColor, 10, 5f, 0.14f);
        if (TryGetComponent(out PlayerAnimator anim)) anim.Squash(new Vector2(1.3f, 0.75f), 0.18f);

        invulnTimer = blinkLevel >= 2 ? 0.6f : 0.3f;
        dashCooldownTimer = CooldownTotal = blinkCooldown * (blinkLevel >= 2 ? 0.75f : 1f);
    }

    // Score candidate landing spots and take the best:
    //   - enemies right at the spot are a huge penalty, enemies a bit further a small one
    //   - spots in the direction you're facing/moving are preferred
    //   - a burger in range is a strong pull when you're hurt (you land on it and eat it)
    // Candidates: burgers in range + spots fanned toward the facing direction + random spots.
    private Vector2 FindSafeSpot(Vector2 from)
    {

        Vector2 facing = lastMoveDir.sqrMagnitude > 0.01f ? lastMoveDir.normalized : Vector2.down;
        bool hurt = stats.currentHealth < stats.maxHealth * 0.95f;

        var candidates = new System.Collections.Generic.List<Vector2>();
        if (hurt)
        {
            foreach (Burger b in Burger.Active)
            {
                float d = Vector2.Distance(from, b.transform.position);
                if (d <= blinkMaxDistance * 1.3f) candidates.Add(b.transform.position);
            }
        }
        for (int i = 0; i < 12; i++) // fan of ±60° around facing
        {
            float angle = Random.Range(-60f, 60f);
            candidates.Add(from + (Vector2)(Quaternion.Euler(0f, 0f, angle) * facing) * Random.Range(blinkMinDistance, blinkMaxDistance));
        }
        for (int i = 0; i < 12; i++) // anywhere, as an escape hatch when facing is blocked
        {
            candidates.Add(from + Random.insideUnitCircle.normalized * Random.Range(blinkMinDistance, blinkMaxDistance));
        }

        Vector2 best = from;
        float bestScore = float.MinValue;
        foreach (Vector2 c in candidates)
        {
            int close = Bomb.OverlapEnemies(c, blinkClearRadius, scratch).Count;
            int nearby = Bomb.OverlapEnemies(c, blinkClearRadius * 2f, scratch).Count;
            float score = -100f * close - 3f * nearby;
            score += 4f * Vector2.Dot((c - from).normalized, facing); // -4..+4
            score += Random.Range(0f, 1.5f); // a little randomness so it is not always dead ahead
            if (hurt && IsOnBurger(c)) score += 12f * (1f - stats.currentHealth / stats.maxHealth) + 6f;
            if (MapGenerator.Blocked(c + FeetOffset, FeetRadius + 0.05f)) score -= 1000f; // never into a pond, rock, trunk or past the wall
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best;
    }

    private static bool IsOnBurger(Vector2 spot)
    {
        foreach (Burger b in Burger.Active)
        {
            if (Vector2.Distance(spot, b.transform.position) < 0.3f) return true;
        }
        return false;
    }

    public Vector2 GetFacingDirection() => lastMoveDir;
}
