using UnityEngine;

public class EnemyAnimator : MonoBehaviour
{
    public Sprite[] born, idle, walk, jump, attack, hurt, death, sleep, wake;
    [Tooltip("Recolour + accessory for special cats (needs the white cat frames)")]
    public SpriteFx.CatSkin skin;

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

    public void Flash(float seconds)
    {
        flashUntil = Time.time + seconds;
        hurtUntil = Time.time + HurtFaceTime;
        if (current != null) Show(CurrentFrame());
    }

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
        auraColor = Color.Lerp(color.Value, Color.white, 0.45f);
        aura.enabled = true;
    }

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
        if (Time.time < hurtUntil && current != death && hurt != null && hurt.Length > 0) frame = hurt[0];
        frame = SpriteFx.Skinned(frame, skin);
        sr.sprite = Time.time < flashUntil ? SpriteFx.Flash(frame) : SpriteFx.Outlined(frame);
    }

    private void Update()
    {
        shadow.color = new Color(0f, 0f, 0f, 0.3f * sr.color.a);
        if (aura != null && aura.enabled)
            aura.color = new Color(auraColor.r, auraColor.g, auraColor.b, (0.35f + 0.15f * Mathf.Sin(Time.time * 5f)) * sr.color.a);
        if (current == null) return;

        time += Time.deltaTime;
        Show(CurrentFrame());
    }
}
