using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Dash")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.25f;
    public float dashCooldown = 0.6f;

    [Header("Blink (skill: dash becomes a teleport)")]
    public int blinkLevel;
    public float blinkMinDistance = 3f;
    public float blinkMaxDistance = 5.5f;
    public float blinkClearRadius = 1.6f;
    public float blinkCooldown = 4f;

    private float invulnTimer;
    private static readonly System.Collections.Generic.List<Collider2D> scratch = new System.Collections.Generic.List<Collider2D>(32);
    private SpriteRenderer sr;
    private CircleCollider2D body;

    public bool IsDashing => isDashing;
    public bool IsInvulnerable => isDashing || invulnTimer > 0f;
    public float CooldownRemaining => dashCooldownTimer;
    public float CooldownTotal { get; private set; } = 1f;
    public Vector2 MoveInput => moveInput;
    public Vector2 DashDirection => dashDirection;
    public float DashProgress => Mathf.Clamp01(1f - dashTimer / dashDuration);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<CircleCollider2D>();
        stats = GetComponent<PlayerStats>();
        sr = GetComponent<SpriteRenderer>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        moveInput = ReadMoveInput();
        if (moveInput.sqrMagnitude > 0.01f) lastMoveDir = moveInput;

        var kb = Keyboard.current;
        bool dashPressed = Keybinds.Pressed(Keybinds.Action.Dash) || Keybinds.AltPressed(Key.LeftShift)
                           || Keybinds.PadPressed(Keybinds.Action.Dash);
        if (dashPressed && Time.timeScale > 0f && !isDashing && dashCooldownTimer <= 0f)
        {
            BeginDash();
        }

        if (dashCooldownTimer > 0f && (dashCooldownTimer -= Time.deltaTime) <= 0f && CooldownTotal >= 1f)
        {
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
            if (Keybinds.Held(Keybinds.Action.Left) || kb.leftArrowKey.isPressed) input.x -= 1;
            if (Keybinds.Held(Keybinds.Action.Right) || kb.rightArrowKey.isPressed) input.x += 1;
            if (Keybinds.Held(Keybinds.Action.Up) || kb.upArrowKey.isPressed) input.y += 1;
            if (Keybinds.Held(Keybinds.Action.Down) || kb.downArrowKey.isPressed) input.y -= 1;
        }

        // Stick: ignore small tilts so a worn or drifting stick doesn't walk the hero on its own
        // (any tilt past the deadzone is still full speed, like the keyboard).
        if (input == Vector2.zero)
        {
            Vector2 pad = GamepadUI.Move();
            if (pad.sqrMagnitude >= StickDeadzone * StickDeadzone) input = pad;
        }

        return input.normalized;
    }

    private const float StickDeadzone = 0.3f;

    private void FixedUpdate()
    {
        Vector2 velocity = isDashing ? dashDirection * dashSpeed : moveInput * stats.MoveSpeed;
        // On ice you keep your momentum (a dash onto ice skids on afterwards).
        if (!isDashing && MapGenerator.OnIce(rb.position)) velocity = Vector2.MoveTowards(iceVelocity, velocity, MapGenerator.IceGrip * Time.fixedDeltaTime);
        iceVelocity = velocity;
        if (isDashing) MapGenerator.Nudge(rb.position, 0.9f, 1f); // dashing through bushes shakes them hard
        rb.MovePosition(rb.position + Slide(rb.position, velocity * Time.fixedDeltaTime, isDashing ? dashMask : MapGenerator.ObstacleMask));
    }

    private int dashMask;
    private Vector2 iceVelocity;
    public void ResetDashCooldown() => dashCooldownTimer = 0f;
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
            if (inside && Physics2D.OverlapCircle(land, radius + 0.25f, MapGenerator.ObstacleMask) == null)
            {
                dashTimer += Mathf.Max(0f, d - length) / dashSpeed;
                return MapGenerator.TallMask;
            }
        }
        return MapGenerator.ObstacleMask;
    }

    private const float Skin = 0.02f;
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
            if (!hit || hit.distance <= 0f) return d;
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

    private void Blink()
    {
        Vector2 from = rb.position;
        Vector2 to = MapGenerator.PlaceDrop(FindSafeSpot(from));
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
            if (KaiSword.Active != null) KaiSword.Active.Spin(from, 2f);
            else if (NyxDrones.Active != null) NyxDrones.Active.Emp(from, 2f);
            else Bomb.Blast(from, 1.8f, stats.AttackDamage * 2f, stats.critChance, stats.critDamage, secondary: true);
        }

        rb.position = to;
        transform.position = to;
        ExplosionFx.Spawn(to, 0.7f, blinkColor);
        Juice.Smoke(to, 1f, 4);
        if (KaiPassives.Active != null) KaiPassives.Active.BlinkCut(from, to); // Dash Slash works with Blink too
        Juice.Sparks(to, blinkColor, 10, 5f, 0.14f);
        if (TryGetComponent(out PlayerAnimator anim)) anim.Squash(new Vector2(1.3f, 0.75f), 0.18f);

        invulnTimer = blinkLevel >= 2 ? 0.6f : 0.3f;
        dashCooldownTimer = CooldownTotal = blinkCooldown * (blinkLevel >= 2 ? 0.75f : 1f);
    }

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
        for (int i = 0; i < 12; i++)
        {
            float angle = Random.Range(-60f, 60f);
            candidates.Add(from + (Vector2)(Quaternion.Euler(0f, 0f, angle) * facing) * Random.Range(blinkMinDistance, blinkMaxDistance));
        }
        for (int i = 0; i < 12; i++)
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
            score += 4f * Vector2.Dot((c - from).normalized, facing);
            score += Random.Range(0f, 1.5f);
            if (hurt && IsOnBurger(c)) score += 12f * (1f - stats.currentHealth / stats.maxHealth) + 6f;
            if (MapGenerator.Blocked(c + FeetOffset, FeetRadius + 0.05f)) score -= 1000f;
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
