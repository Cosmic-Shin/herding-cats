using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [System.Serializable]
    public class Clip { public Sprite[] frames; }

    public SpriteRenderer spriteRenderer;
    public float walkFps = 10f;
    public float throwFps = 20f;
    public float afterImageInterval = 0.03f;

    public Clip[] walk = new Clip[8];
    public Clip[] roll = new Clip[8];
    public Clip[] throwClips = new Clip[4];

    private PlayerController controller;
    private Vector2 facing = Vector2.down;
    private Vector2 throwDir;
    private float throwTime = -1f;
    private float walkTime;
    private float afterImageTimer;
    private static readonly Color Dust = new Color(0.85f, 0.78f, 0.65f);

    private Vector3 baseScale;
    private Vector2 squash = Vector2.one;
    private float squashTime, squashLeft;
    private bool wasDashing;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        SpriteFx.AddShadow(spriteRenderer, 0.55f);
        baseScale = transform.localScale;
    }

    public void Squash(Vector2 amount, float seconds)
    {
        squash = amount;
        squashTime = squashLeft = seconds;
    }

    private const float GlowTime = 1.2f;
    private float stepTimer, glowLeft;
    private SpriteRenderer glow;
    private Color glowColor;

    public void Glow(Color color)
    {
        if (glow == null)
        {
            glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(spriteRenderer.transform, false);
            glow.transform.localPosition = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.center : Vector3.zero;
            glow.transform.localScale = Vector3.one * 1.3f;
            glow.sprite = Shapes.Circle;
            glow.sortingOrder = spriteRenderer.sortingOrder - 1;
        }
        glowColor = color;
        glowLeft = GlowTime;
        glow.enabled = true;
    }

    public void PlayThrow(Vector2 dir)
    {
        throwDir = dir;
        throwTime = 0f;
        if (squashLeft <= 0f) Squash(new Vector2(1.08f, 0.93f), 0.1f);
    }

    private void Update()
    {
        Vector2 move = controller.MoveInput;
        bool moving = move.sqrMagnitude > 0.01f;
        if (moving) facing = move;

        Sprite frame;
        if (controller.IsDashing)
        {
            Sprite[] f = roll[Dir8(controller.DashDirection)].frames;
            frame = f[Mathf.Min(f.Length - 1, (int)(controller.DashProgress * f.Length))];
        }
        else if (throwTime >= 0f)
        {
            Sprite[] f = throwClips[Quadrant(throwDir)].frames;
            int i = (int)(throwTime * throwFps);
            throwTime += Time.deltaTime;
            if (i >= f.Length - 1) throwTime = -1f;
            frame = f[Mathf.Min(i, f.Length - 1)];
        }
        else
        {
            Sprite[] f = walk[Dir8(facing)].frames;
            walkTime = moving ? walkTime + Time.deltaTime : 0f;
            frame = f[(int)(walkTime * walkFps) % f.Length];
        }

        if (frame != null) spriteRenderer.sprite = frame;

        if (controller.IsDashing && !wasDashing) Squash(new Vector2(0.8f, 1.2f), 0.12f);
        if (controller.IsDashing != wasDashing) Juice.CatPuff(transform.position, Dust);
        if (!controller.IsDashing && wasDashing) Squash(new Vector2(1.25f, 0.8f), 0.15f);
        wasDashing = controller.IsDashing;

        squashLeft -= Time.deltaTime;
        float k = squashLeft > 0f ? squashLeft / squashTime : 0f;
        float breathe = moving || controller.IsDashing ? 0f : 0.025f * Mathf.Sin(Time.time * 3f);
        transform.localScale = Vector3.Scale(baseScale, new Vector3(Mathf.Lerp(1f, squash.x, k) * (1f - breathe * 0.5f), Mathf.Lerp(1f, squash.y, k) * (1f + breathe), 1f));

        if (moving && !controller.IsDashing && (stepTimer -= Time.deltaTime) <= 0f)
        {
            stepTimer = 0.28f;
            Vector2 feet = (Vector2)transform.position + Vector2.down * 0.3f;
            Juice.Smoke(feet, 0.3f, 1);
            Sfx.Play(Sfx.Id.Step, 0.25f, Random.Range(0.9f, 1.15f));
        }

        if (glowLeft > 0f && glow != null)
        {
            glowLeft -= Time.deltaTime;
            float g = Mathf.Clamp01(glowLeft / GlowTime);
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.45f * g * (0.75f + 0.25f * Mathf.Sin(Time.time * 12f)));
            if (Random.value < 0.3f * g) Juice.Sparks((Vector2)transform.position + Random.insideUnitCircle * 0.5f, glowColor, 1, 0.8f, 0.1f);
            if (glowLeft <= 0f) glow.enabled = false;
        }

        if (controller.IsDashing && (afterImageTimer -= Time.deltaTime) <= 0f)
        {
            AfterImage.Spawn(spriteRenderer);
            Juice.Smoke(transform.position, 0.5f, 1);
            afterImageTimer = afterImageInterval;
        }
    }

    private static int Dir8(Vector2 v)
    {
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        return ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;
    }

    private static int Quadrant(Vector2 v)
    {
        if (v.y >= 0f) return v.x >= 0f ? 0 : 1;
        return v.x < 0f ? 2 : 3;
    }
}
