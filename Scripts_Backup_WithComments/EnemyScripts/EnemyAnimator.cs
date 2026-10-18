using UnityEngine;

// Plays sprite-array clips. EnemyAI decides which clip; this just flips frames.
// Frames are shown outlined (SpriteFx), swapped for a white silhouette during hit flashes and
// the hurt face just after, with a soft shadow underneath and an optional pulsing aura (elites).
public class EnemyAnimator : MonoBehaviour
{
    public Sprite[] born, idle, walk, jump, attack, hurt, death, sleep, wake;

    private const float HurtFaceTime = 0.2f;

    private SpriteRenderer sr, shadow, aura;
    private Color auraColor;
    private Sprite[] current;
    private bool loop;
    private float fps;
    private float time;
    private float flashUntil, hurtUntil;

    public int Frame => (int)(time * fps);

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        shadow = SpriteFx.AddShadow(sr);
    }

    // Hit: white silhouette for `seconds`, then the hurt frame for a moment.
    public void Flash(float seconds)
    {
        flashUntil = Time.time + seconds;
        hurtUntil = Time.time + HurtFaceTime;
        if (current != null) Show(CurrentFrame());
    }

    // Elites and minibosses: a pulsing glow behind the cat so it stands out in a crowd. null = off.
    public void SetAura(Color? color)
    {
        if (color == null) { if (aura != null) aura.enabled = false; return; }
        if (aura == null)
        {
            aura = new GameObject("Aura").AddComponent<SpriteRenderer>();
            aura.transform.SetParent(transform, false);
            aura.sprite = Shapes.Circle;
            aura.sortingOrder = sr.sortingOrder - 1;
        }
        Bounds b = sr.sprite != null ? sr.sprite.bounds : new Bounds(Vector3.zero, Vector3.one * 0.7f);
        aura.transform.localPosition = b.center;
        aura.transform.localScale = Vector3.one * Mathf.Max(b.size.x, b.size.y) * 1.5f;
        auraColor = Color.Lerp(color.Value, Color.white, 0.45f); // lighter: translucent saturated colors go murky over the blue
        aura.enabled = true;
    }

    // Returns clip duration in seconds. Re-playing the clip that is already looping is a no-op.
    public float Play(Sprite[] clip, float clipFps, bool looping = false)
    {
        if (clip == null || clip.Length == 0) return 0f;
        if (clip == current && looping && loop) return clip.Length / clipFps;

        current = clip;
        fps = clipFps;
        loop = looping;
        time = 0f;
        Show(clip[0]);
        return clip.Length / clipFps;
    }

    private Sprite CurrentFrame() => current[loop ? Frame % current.Length : Mathf.Min(Frame, current.Length - 1)];

    private void Show(Sprite frame)
    {
        // hurt face only interrupts living clips (never the death animation)
        if (Time.time < hurtUntil && current != death && hurt != null && hurt.Length > 0) frame = hurt[0];
        sr.sprite = Time.time < flashUntil ? SpriteFx.Flash(frame) : SpriteFx.Outlined(frame);
    }

    private void Update()
    {
        shadow.color = new Color(0f, 0f, 0f, 0.3f * sr.color.a); // fades out with the death fade
        if (aura != null && aura.enabled)
            aura.color = new Color(auraColor.r, auraColor.g, auraColor.b, (0.35f + 0.15f * Mathf.Sin(Time.time * 5f)) * sr.color.a);
        if (current == null) return;

        time += Time.deltaTime;
        Show(CurrentFrame());
    }
}
