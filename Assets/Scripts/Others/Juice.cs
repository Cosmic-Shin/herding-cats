using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Juice : MonoBehaviour
{
    private const float StopScale = 0.02f;
    private const float LowHpFraction = 0.3f;

    private static Juice instance;
    private Image flash, vignette;
    private ParticleSystem puffs;
    private PlayerStats player;
    private bool wasLow;
    private float stopUntil;
    private float slowUntil, slowScale = 1f;
    private float nextBeat;
    private bool stopping;
    private float nextTalk;

    private static Juice I
    {
        get
        {
            if (instance == null) instance = new GameObject("Juice").AddComponent<Juice>();
            return instance;
        }
    }

    public static void HitStop(float seconds)
    {
        if (Time.timeScale == 0f) return;
        Juice j = I;
        j.stopUntil = Mathf.Max(j.stopUntil, Time.unscaledTime + seconds);
        j.ApplyTimeScale();
    }

    public static void SlowMo(float scale, float seconds)
    {
        if (Time.timeScale == 0f) return;
        Juice j = I;
        j.slowUntil = Mathf.Max(j.slowUntil, Time.unscaledTime + seconds);
        j.slowScale = scale;
        j.ApplyTimeScale();
    }

    private void ApplyTimeScale()
    {
        float now = Time.unscaledTime;
        float want = now < stopUntil ? StopScale : now < slowUntil ? slowScale : 1f;
        if (want < 1f) { stopping = true; Time.timeScale = want; }
        else if (stopping) { stopping = false; Time.timeScale = 1f; }
    }

    public static void CancelHitStop()
    {
        if (instance == null || !instance.stopping) return;
        instance.stopping = false;
        instance.stopUntil = instance.slowUntil = 0f;
        Time.timeScale = 1f;
    }

    public static void Flash(Color color, float seconds = 0.25f)
    {
        if (Settings.FlashScale <= 0f) return;
        color.a *= Settings.FlashScale;
        I.StartCoroutine(I.FlashRoutine(color, seconds));
    }

    private IEnumerator FlashRoutine(Color color, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            flash.color = new Color(color.r, color.g, color.b, color.a * (1f - t / seconds));
            yield return null;
        }
        flash.color = Color.clear;
    }

    public static void CatPuff(Vector2 pos, Color tint)
    {
        for (int i = 0; i < 7; i++)
            Emit(pos, Random.Range(1.5f, 3.5f), Random.Range(0.12f, 0.28f), Random.Range(0.25f, 0.45f),
                 Color.Lerp(tint, Color.white, Random.Range(0.2f, 0.7f)));
    }

    public static void Sparks(Vector2 pos, Color color, int count, float speed, float size)
    {
        for (int i = 0; i < count; i++)
            Emit(pos, speed * Random.Range(0.5f, 1f), size * Random.Range(0.6f, 1.2f), Random.Range(0.2f, 0.35f),
                 Color.Lerp(color, Color.white, Random.Range(0f, 0.5f)));
    }

    public static void Smoke(Vector2 pos, float radius, int count)
    {
        for (int i = 0; i < count; i++)
            Emit(pos + Random.insideUnitCircle * radius * 0.4f, Random.Range(0.5f, 1.5f), radius * Random.Range(0.35f, 0.6f),
                 Random.Range(0.4f, 0.7f), new Color(0.35f, 0.32f, 0.3f, 0.4f));
    }

    public static void SparksDir(Vector2 pos, Vector2 dir, float spread, Color color, int count, float speed, float size)
    {
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            float a = (baseAngle + Random.Range(-spread, spread)) * Mathf.Deg2Rad;
            Emit(pos, speed * Random.Range(0.6f, 1f), size * Random.Range(0.6f, 1.2f), Random.Range(0.15f, 0.3f),
                 Color.Lerp(color, Color.white, Random.Range(0f, 0.5f)), new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
        }
    }

    private static void Emit(Vector2 pos, float speed, float size, float life, Color color, Vector2? dir = null)
    {
        if (Settings.LowParticles && Random.value < 0.5f) return;
        var p = new ParticleSystem.EmitParams
        {
            position = pos, applyShapeToPosition = true,
            velocity = (dir ?? Random.insideUnitCircle.normalized) * speed,
            startSize = size, startLifetime = life, startColor = color,
        };
        I.puffs.Emit(p, 1);
    }

    private static readonly string[] WakeLines = { "!", "mrrp?", "nya!", "!?" };
    private static readonly string[] LeapLines = { "HISS!", "nyaa!", "rawr!" };
    private static readonly string[] DeathLines = { "mew...", "nyu~", "x_x", "meow!" };

    public enum Talk { Wake, Leap, Death }

    public static void CatTalk(Vector2 pos, Talk kind, float chance)
    {
        Juice j = I;
        if (Time.time < j.nextTalk || Random.value > chance) return;
        j.nextTalk = Time.time + 0.5f;
        string[] lines = kind == Talk.Wake ? WakeLines : kind == Talk.Leap ? LeapLines : DeathLines;
        Color color = kind == Talk.Leap ? new Color(1f, 0.45f, 0.35f) : new Color(1f, 0.9f, 0.75f);
        DamagePopup.SpawnText(pos + Vector2.up * 0.5f, lines[Random.Range(0, lines.Length)], color, 3f);
        Sfx.Play(Sfx.Id.Meow, 0.3f, kind == Talk.Leap ? 0.8f : kind == Talk.Death ? 1.25f : 1.05f);
    }

    private void Awake()
    {
        var canvasGo = new GameObject("JuiceCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        vignette = FullScreen(canvasGo.transform, "LowHpVignette", MakeVignette());
        flash = FullScreen(canvasGo.transform, "Flash", null);
        vignette.color = flash.color = Color.clear;

        puffs = MakePuffs();
        player = FindFirstObjectByType<PlayerStats>();
    }

    private void Update()
    {
        if (stopping && Time.timeScale > 0f) ApplyTimeScale();
        UpdateLowHp();
    }

    private void UpdateLowHp()
    {
        if (player == null) return;
        bool low = player.currentHealth > 0f && player.currentHealth < player.maxHealth * LowHpFraction;
        if (low && !wasLow)
            DamagePopup.SpawnText((Vector2)player.transform.position + Vector2.up * 0.9f, "!", new Color(1f, 0.25f, 0.2f), 7f);
        wasLow = low;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        vignette.color = low ? new Color(0.9f, 0.05f, 0.05f, 0.35f + 0.3f * pulse) : Color.clear;
        if (low && Time.timeScale > 0f && Time.unscaledTime >= nextBeat)
        {
            nextBeat = Time.unscaledTime + 0.85f;
            Sfx.Play(Sfx.Id.Heartbeat, 0.6f);
        }
    }

    private static Image FullScreen(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static Sprite MakeVignette()
    {
        const int n = 128;
        var tex = new Texture2D(n, n) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.05f, d))));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    private ParticleSystem MakePuffs()
    {
        var go = new GameObject("CatPuffs");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;
        main.gravityModifier = 0f;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.5f;
        limit.dampen = 0.15f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.AddSprite(Shapes.Circle);
        var r = go.GetComponent<ParticleSystemRenderer>();
        var probe = new GameObject("probe").AddComponent<SpriteRenderer>();
        r.sharedMaterial = probe.sharedMaterial;
        Destroy(probe.gameObject);
        Lighting.Glow(r);
        r.sortingOrder = 55;

        ps.Play();
        return ps;
    }
}
