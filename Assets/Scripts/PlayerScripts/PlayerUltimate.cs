using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerAutoAttack))]
public class PlayerUltimate : MonoBehaviour
{
    public float cooldown = 60f;
    public float firstChargeTime = 30f;
    public float duration = 10f;

    [Header("While Active")]
    public float damageMultiplier = 1.6f;
    public float radiusMultiplier = 1.5f;
    public int extraBombs = 2;

    [Header("Opening Burst")]
    public int burstBombs = 8;

    public float CooldownRemaining { get; private set; }
    public float ActiveRemaining { get; private set; }
    public bool IsActive => ActiveRemaining > 0f;
    public bool IsReady => !IsActive && CooldownRemaining <= 0f;
    public void Recharge() => CooldownRemaining = 0f;

    public float DamageMultiplier => IsActive ? damageMultiplier : 1f;
    public float RadiusMultiplier => IsActive ? radiusMultiplier : 1f;
    public int ExtraBombs => IsActive ? extraBombs : 0;

    private class Style { public string name, shout; public Color main, light; public float speedBonus; public Color? mood; }
    private static readonly Style Mira = new Style { name = "SPECIAL", shout = "ULTIMATE!", main = new Color(1f, 0.6f, 0.15f), light = new Color(1f, 0.85f, 0.4f) };
    private static readonly Style Kai = new Style { name = "RAGE", shout = "RAGE!", main = new Color(0.95f, 0.12f, 0.1f), light = new Color(1f, 0.45f, 0.35f), speedBonus = 0.4f, mood = new Color(1f, 0.6f, 0.55f) };
    private static readonly Style Nyx = new Style { name = "OVERRIDE", shout = "SYSTEM OVERRIDE!", main = new Color(0.3f, 0.9f, 1f), light = new Color(0.75f, 1f, 1f), speedBonus = 0.5f, mood = new Color(0.7f, 0.88f, 1f) };
    private static Style Current => Hero.Current.id == "Kai" ? Kai : Hero.Current.id == "Nyx" ? Nyx : Mira;

    public string Name => Current.name;
    public static readonly Color RageRed = Kai.main;

    private PlayerAutoAttack attack;
    private PlayerStats stats;
    private SpriteRenderer aura, body;
    private SpriteRenderer ring, ripple;
    private readonly SpriteRenderer[] dataBits = new SpriteRenderer[6];
    private float trailTimer, flameTimer, appliedSpeed;
    private Color? moodBefore;
    private Style active;

    private void Awake()
    {
        attack = GetComponent<PlayerAutoAttack>();
        stats = GetComponent<PlayerStats>();
        CooldownRemaining = firstChargeTime;
        body = GetComponentInChildren<SpriteRenderer>();
        aura = CreateAura();
        ring = FieldPart("UltimateRing", Shapes.Ring);
        ripple = FieldPart("UltimateRipple", Shapes.Ring);
        for (int i = 0; i < dataBits.Length; i++) dataBits[i] = FieldPart("DataBit", Shapes.Square);
    }

    private void OnDisable()
    {
        if (IsActive) End(false);
    }

    private void Update()
    {
        if (IsActive)
        {
            ActiveRemaining -= Time.deltaTime;
            Style s = active ?? Current;
            UpdateField(s);
            if ((trailTimer -= Time.deltaTime) <= 0f)
            {
                trailTimer = 0.07f;
                if (body != null && !Settings.LowParticles) AfterImage.Spawn(body, new Color(s.light.r, s.light.g, s.light.b, 0.85f));
                Juice.SparksDir((Vector2)transform.position + Random.insideUnitCircle * 0.5f, Vector2.up, 30f, s == Kai ? new Color(1f, 0.6f, 0.2f) : s.main, s == Kai ? 1 : 2, 2.5f, 0.1f);
                if (s == Nyx && Random.value < 0.45f) HeroFx.Glitch(body, 0.08f);
            }
            if (!IsActive) End();
        }
        else if (CooldownRemaining > 0f)
        {
            CooldownRemaining -= Time.deltaTime;
        }

        if (IsReady && Time.timeScale > 0f && Pressed()) Activate();
    }

    private static bool Pressed() =>
        Keybinds.Pressed(Keybinds.Action.Special) ||
        Keybinds.PadPressed(Keybinds.Action.Special);

    private void Activate()
    {
        Style s = active = Current;
        ActiveRemaining = duration;
        ShowField(s);
        if (TryGetComponent(out KaiSword sword) && sword.enabled) sword.Spin(transform.position, 2.5f);
        else if (TryGetComponent(out NyxDrones drones) && drones.enabled)
        {
            drones.Barrage();
            if (NyxPassives.Active != null) NyxPassives.Active.HackAll(3f);
        }
        else attack.Burst(burstBombs);

        appliedSpeed = s.speedBonus;
        stats.attackSpeedMultiplier += appliedSpeed;
        if (s.mood != null) { moodBefore = Lighting.Mood; Lighting.Mood = s.mood; }

        Vector2 at = transform.position;
        ExplosionFx.Spawn(at, 2.5f, s.main);
        CameraFollow.Shake(0.2f, 0.3f);
        DamagePopup.SpawnText(at + Vector2.up * 0.5f, s.shout, s.light, 6f);
        if (s == Kai) KaiOpener(at);
        else if (s == Nyx) StartCoroutine(NyxOpener(at));
        else Juice.Flash(new Color(s.main.r, s.main.g, s.main.b, 0.45f), 0.35f);
        Juice.HitStop(0.1f);
        ExplosionFx.Ring(at, 9f, s.light, 0.6f);
        ExplosionFx.Ring(at, 5f, Color.white, 0.4f);
        Juice.Sparks(at, s.light, 24, 12f, 0.2f);
        if (s == Nyx) HeroFx.Bits(at, s.main, NyxPassives.Magenta, 24, 9f, 0.12f);
        CameraFollow.ZoomPunch(0.8f);
        GamepadUI.Rumble(0.6f, 0.3f, 0.35f);
        Sfx.Play(Sfx.Id.Whoosh, 0.8f, 0.7f);
    }

    private void End(bool fx = true)
    {
        ActiveRemaining = 0f;
        CooldownRemaining = cooldown;
        if (fx && active == Kai) { Juice.Smoke(transform.position, 0.6f, 5); Juice.SparksDir(transform.position, Vector2.up, 50f, new Color(1f, 0.45f, 0.2f), 6, 3f, 0.1f); }
        else if (fx && active == Nyx) { HeroFx.Bits(transform.position, Nyx.main, NyxPassives.Magenta, 12, 4f); HeroFx.Glitch(body, 0.15f); }
        HideField();
        stats.attackSpeedMultiplier -= appliedSpeed;
        appliedSpeed = 0f;
        if (active != null && active.mood != null && Lighting.Mood == active.mood) Lighting.Mood = moodBefore;
        active = null;
    }


    private SpriteRenderer FieldPart(string name, Sprite sprite)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.localPosition = new Vector3(0f, -0.35f, 0f);
        Lighting.Glow(sr);
        sr.sprite = sprite;
        sr.enabled = false;
        return sr;
    }

    private void ShowField(Style s)
    {
        bool fancy = s != Mira;
        aura.sprite = fancy ? Shapes.SoftGlow : ExplosionFx.GetCircle();
        aura.transform.localPosition = new Vector3(0f, fancy ? -0.3f : -0.1f, 0f);
        aura.enabled = true;
        ring.enabled = ripple.enabled = fancy;
        foreach (SpriteRenderer b in dataBits) b.enabled = s == Nyx;
    }

    private void HideField()
    {
        aura.enabled = ring.enabled = ripple.enabled = false;
        foreach (SpriteRenderer b in dataBits) b.enabled = false;
    }

    private void UpdateField(Style s)
    {
        float t = Time.time, pulse = 0.5f + 0.5f * Mathf.Sin(t * 10f);
        if (s == Mira)
        {
            aura.transform.localScale = Vector3.one * (1.6f + 0.15f * pulse);
            aura.color = new Color(s.main.r, s.main.g, s.main.b, 0.25f + 0.15f * pulse);
            return;
        }
        float a = Settings.EffectAlpha;
        int under = body != null ? body.sortingOrder - 2 : 9;
        aura.sortingOrder = ring.sortingOrder = ripple.sortingOrder = under;
        aura.transform.localScale = new Vector3(3.4f, 2.2f, 1f) * (1f + 0.06f * pulse);
        aura.color = new Color(s.main.r, s.main.g, s.main.b, (0.4f + 0.15f * pulse) * a);
        Vector3 flat = new Vector3(2.3f, 1.15f, 1f);
        float sweep = Mathf.Repeat(t * 1.4f, 1f);

        if (s == Kai)
        {
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 4f)), 6f);
            ring.transform.localScale = flat * (1f + 0.12f * beat);
            ring.color = new Color(1f, 0.28f, 0.18f, (0.45f + 0.45f * beat) * a);
            ripple.transform.localScale = flat * (1f + 0.6f * sweep);
            ripple.color = new Color(1f, 0.55f, 0.3f, 0.5f * (1f - sweep) * a);
            if ((flameTimer -= Time.deltaTime) <= 0f) { flameTimer = Settings.LowParticles ? 0.09f : 0.03f; Flame(); }
            return;
        }

        ring.transform.localScale = flat * 1.05f;
        ring.color = new Color(s.main.r, s.main.g, s.main.b, (Random.value < 0.07f ? 0.15f : 0.7f) * a);
        ripple.transform.localScale = flat * (0.3f + 0.9f * sweep);
        ripple.color = new Color(NyxPassives.Magenta.r, NyxPassives.Magenta.g, NyxPassives.Magenta.b, 0.7f * (1f - sweep) * a);
        for (int i = 0; i < dataBits.Length; i++)
        {
            float ang = t * 2.6f + i * Mathf.PI * 2f / dataBits.Length, sin = Mathf.Sin(ang);
            SpriteRenderer b = dataBits[i];
            b.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.9f - 0.06f, 0.3f + sin * 0.3f, 0f);
            b.transform.localScale = Vector3.one * (0.17f + 0.03f * Mathf.Sin(t * 9f + i));
            b.sortingOrder = body != null ? body.sortingOrder + (sin < 0f ? 1 : -1) : 11;
            Color c = i % 2 == 0 ? s.main : NyxPassives.Magenta;
            b.color = new Color(c.r, c.g, c.b, Random.value < 0.06f ? 0.2f : 1f);
        }
    }

    private static Color Fire(float k)
    {
        Color orange = new Color(1f, 0.5f, 0.1f);
        Color c = k < 0.3f ? Color.Lerp(new Color(1f, 0.95f, 0.6f), orange, k / 0.3f) : Color.Lerp(orange, new Color(0.8f, 0.08f, 0.06f), (k - 0.3f) / 0.7f);
        c.a = Mathf.Min(1f, Mathf.Pow(1f - k, 1.1f) * 1.2f) * Settings.EffectAlpha;
        return c;
    }

    private void Flame()
    {
        Vector2 at = (Vector2)transform.position + new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(-0.45f, -0.2f));
        float size = Random.Range(0.8f, 1.25f), life = Random.Range(0.35f, 0.55f), sway = Random.Range(0f, 6f);
        int order = body != null ? body.sortingOrder + (Random.value < 0.35f ? 1 : -1) : 11;
        Shot f = Shot.Spawn(FlameSprite, Fire(0f), size, at, order);
        f.Renderer.flipX = Random.value < 0.5f;
        f.life = life;
        f.velocity = new Vector2(0f, Random.Range(2.2f, 3.4f));
        f.onUpdate = shot =>
        {
            float k = shot.age / life;
            shot.velocity.x = Mathf.Sin(sway + shot.age * 14f) * 0.6f;
            shot.transform.localScale = new Vector3(size * (1f - 0.6f * k), size * (1.4f - 0.6f * k), 1f);
            shot.Renderer.color = Fire(k);
        };
    }

    private static Sprite flameSprite;
    private static Sprite FlameSprite
    {
        get
        {
            if (flameSprite != null) return flameSprite;
            string[] rows = {
                "...#...",
                "...##..",
                "..###..",
                "..####.",
                ".#####.",
                ".#####.",
                "#######",
                "#######",
                ".#####.",
                "..###..",
            };
            var tex = new Texture2D(7, rows.Length) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < 7; x++)
                    tex.SetPixel(x, rows.Length - 1 - y, rows[y][x] == '#' ? Color.white : Color.clear);
            tex.Apply();
            return flameSprite = Sprite.Create(tex, new Rect(0, 0, 7, rows.Length), new Vector2(0.5f, 0.2f), 16f);
        }
    }

    private void KaiOpener(Vector2 at)
    {
        StompFx.Spawn(at, 2.6f, MapGenerator.Winter);
        for (int i = 0; i < 18; i++)
        {
            float ang = (i * 20f + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float life = Random.Range(0.3f, 0.45f), size = Random.Range(0.5f, 0.8f);
            Shot jet = Shot.Spawn(Shapes.SoftGlow, Fire(0f), size, at + dir * 0.4f, 60);
            jet.life = life;
            jet.velocity = dir * Random.Range(7f, 10f);
            jet.onUpdate = shot =>
            {
                float k = shot.age / life;
                shot.velocity *= 1f - 4f * Time.deltaTime;
                shot.transform.localScale = Vector3.one * size * (1f + 0.8f * k);
                shot.Renderer.color = Fire(k);
            };
        }
        Juice.SparksDir(at, Vector2.up, 70f, new Color(1f, 0.6f, 0.2f), 16, 7f, 0.13f);
        Lighting.Flash(at, 7f, Kai.main, 0.6f);
        Juice.Flash(new Color(0.55f, 0.03f, 0.03f, 0.35f), 0.3f);
    }

    private IEnumerator NyxOpener(Vector2 at)
    {
        Juice.Flash(new Color(0.3f, 0.9f, 1f, 0.3f), 0.12f);
        ScanLine(at, Nyx.main, 0f);
        ScanLine(at, NyxPassives.Magenta, 0.06f);
        ExplosionFx.Crosses(at, 6f, Nyx.main, 14, 0.7f);
        yield return new WaitForSecondsRealtime(0.09f);
        Juice.Flash(new Color(1f, 0.25f, 0.85f, 0.22f), 0.12f);
        HeroFx.Glitch(body, 0.25f);
        yield return new WaitForSecondsRealtime(0.06f);
        HeroFx.Glitch(body, 0.18f);
    }

    private void ScanLine(Vector2 at, Color color, float delay)
    {
        Camera cam = Camera.main;
        float halfH = cam != null ? cam.orthographicSize : 6f, halfW = halfH * (cam != null ? cam.aspect : 1.8f);
        Vector2 center = cam != null ? (Vector2)cam.transform.position : at;
        const float life = 0.4f;
        Shot line = Shot.Spawn(Shapes.Square, new Color(color.r, color.g, color.b, 0f), 1f, new Vector2(center.x - halfW - 1f, center.y + halfH), 200);
        line.transform.localScale = new Vector3((halfW + 1f) * 2f, 0.07f, 1f);
        line.life = life + delay;
        line.onUpdate = shot =>
        {
            float k = Mathf.Clamp01((shot.age - delay) / life);
            shot.transform.position = new Vector3(center.x - halfW - 1f, center.y + halfH - k * halfH * 2f, 0f);
            shot.Renderer.color = new Color(color.r, color.g, color.b, shot.age < delay ? 0f : 0.8f * (1f - k * 0.5f) * Settings.EffectAlpha);
        };
    }

    private SpriteRenderer CreateAura()
    {
        var go = new GameObject("UltimateAura");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(sr);
        sr.sprite = ExplosionFx.GetCircle();
        sr.sortingOrder = 9;
        sr.enabled = false;
        return sr;
    }
}
