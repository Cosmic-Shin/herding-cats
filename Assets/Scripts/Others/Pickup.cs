using System.Collections.Generic;
using UnityEngine;

public class Pickup : MonoBehaviour
{
    public enum Kind { Magnet, Bomb, Speed, HoloBox, Anvil }

    private const float CollectRadius = 1f;
    private const float SmallLife = 40f;
    private const float CatDropChance = 0.004f;
    public static float AnvilChanceMultiplier = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { AnvilChanceMultiplier = 1f; HoloBoxCount = AnvilCount = 0; }
    public static int HoloBoxCount { get; private set; }
    public static int AnvilCount { get; private set; }
    public const int MaxHoloBoxes = 5, MaxAnvils = 5;
    private const float InteractRadius = 1.5f;
    private static int claimedFrame = -1;
    private TMPro.TextMeshPro prompt;

    private void OnDestroy()
    {
        if (kind == Kind.HoloBox) HoloBoxCount--; else if (kind == Kind.Anvil) AnvilCount--;
        if (prompt != null) Destroy(prompt.gameObject);
    }

    private void ShowPrompt(bool show)
    {
        if (!show) { if (prompt != null) prompt.gameObject.SetActive(false); return; }
        if (prompt == null)
        {
            prompt = new GameObject("OpenPrompt").AddComponent<TMPro.TextMeshPro>();
            prompt.fontSize = 4f;
            prompt.fontStyle = TMPro.FontStyles.Bold;
            prompt.alignment = TMPro.TextAlignmentOptions.Center;
            prompt.sortingOrder = 100;
            prompt.outlineWidth = 0.25f;
            prompt.outlineColor = Color.black;
            prompt.rectTransform.sizeDelta = new Vector2(6f, 1f);
        }
        prompt.gameObject.SetActive(true);
        prompt.text = $"[{Keybinds.Prompt(Keybinds.Action.Interact)}] {(kind == Kind.HoloBox ? "Open" : "Use anvil")}";
        prompt.transform.position = new Vector3(transform.position.x, baseY + 1.5f + 0.06f * Mathf.Sin(age * 5f), 0f);
        prompt.color = Color.Lerp(glowColor, Color.white, 0.5f + 0.5f * Mathf.Sin(age * 6f));
    }
    private static readonly List<Collider2D> hits = new List<Collider2D>(256);

    private Kind kind;
    private Transform player;
    private PlayerStats stats;
    private SpriteRenderer sr;
    private Vector3 baseScale;
    private float age, baseY;

    private bool Small => kind == Kind.Magnet || kind == Kind.Bomb || kind == Kind.Speed;

    public static void Spawn(Kind kind, Vector2 pos)
    {
        if (kind == Kind.HoloBox && HoloBoxCount >= MaxHoloBoxes) return;
        if (kind == Kind.Anvil && AnvilCount >= MaxAnvils) return;
        pos = MapGenerator.PlaceDrop(pos);
        var go = new GameObject("Pickup_" + kind);
        go.transform.position = pos;
        var p = go.AddComponent<Pickup>();
        p.kind = kind;
        if (kind == Kind.HoloBox) HoloBoxCount++;
        else if (kind == Kind.Anvil) AnvilCount++;
        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sprite = UiIcons.Icon(kind switch
        {
            Kind.Magnet => "magnet",
            Kind.Bomb => "bomb",
            Kind.Speed => "speed",
            Kind.HoloBox => "chest",
            _ => "anvil",
        });
        p.sr.sortingOrder = 8;
        p.baseScale = Vector3.one * (p.Small ? 4.5f : 6f);
        go.transform.localScale = p.baseScale;
        p.baseY = pos.y;

        if (!p.Small)
        {
            p.glowColor = kind == Kind.HoloBox ? new Color(1f, 0.85f, 0.3f) : new Color(0.7f, 0.85f, 1f);
            p.pillar = p.Glow(new Vector2(0f, 1.5f), new Vector2(0.28f, 3.4f), 7);
            p.pool = p.Glow(new Vector2(0f, -0.35f), new Vector2(1.2f, 0.35f), 7);
            OffscreenArrows.Track(go.transform, p.glowColor);
        }
    }

    private Color glowColor;
    private SpriteRenderer pillar, pool;

    private SpriteRenderer Glow(Vector2 offset, Vector2 size, int order)
    {
        var g = new GameObject("Glow").AddComponent<SpriteRenderer>();
        g.transform.SetParent(transform, false);
        g.transform.localPosition = offset / baseScale.x;
        g.transform.localScale = new Vector3(size.x, size.y, 1f) / baseScale.x;
        g.sprite = Shapes.Circle;
        g.sortingOrder = order;
        return g;
    }

    public static void TryCatDrop(Vector2 pos)
    {
        if (Random.value >= CatDropChance) return;
        if (Random.value < 0.06f * AnvilChanceMultiplier) { Spawn(Kind.Anvil, pos); return; }
        float r = Random.value * 0.88f;
        Spawn(r < 0.35f ? Kind.Magnet : r < 0.6f ? Kind.Speed : Kind.Bomb, pos);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (player == null)
        {
            stats = FindFirstObjectByType<PlayerStats>();
            if (stats == null) return;
            player = stats.transform;
        }

        if (opening >= 0f) { UpdateOpening(); return; }

        float hop = age < HopTime ? Mathf.Sin(Mathf.PI * age / HopTime) * 0.6f : 0f;
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(age * 4f) * 0.08f + hop;
        transform.position = pos;
        transform.localScale = baseScale * (1f + 0.06f * Mathf.Sin(age * 6f)) * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(age / 0.15f));
        if (pillar != null)
        {
            float glow = 0.5f + 0.5f * Mathf.Sin(age * 3f);
            Color light = Color.Lerp(glowColor, Color.white, 0.6f);
            pillar.color = new Color(light.r, light.g, light.b, 0.3f + 0.25f * glow);
            pool.color = new Color(light.r, light.g, light.b, 0.35f + 0.2f * glow);
            if (Random.value < 0.15f) Juice.Sparks((Vector2)transform.position + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0f, 1.5f)), glowColor, 1, 0.4f, 0.1f);
        }
        if (Small)
        {
            if (age > SmallLife) { Destroy(gameObject); return; }
            sr.enabled = age < SmallLife - 3f || Mathf.Repeat(age * 6f, 1f) > 0.4f;
        }

        bool canAct = Time.timeScale > 0f && !RewardMenu.IsOpen && stats.currentHealth > 0f;
        float dist = Vector2.Distance(player.position, transform.position);

        if (!Small)
        {
            bool near = canAct && dist <= InteractRadius;
            ShowPrompt(near);
            if (!near || claimedFrame == Time.frameCount) return;
            if (!Keybinds.Pressed(Keybinds.Action.Interact) && !Keybinds.PadPressed(Keybinds.Action.Interact)) return;
            claimedFrame = Time.frameCount;
            ShowPrompt(false);
            if (kind == Kind.HoloBox) { opening = 0f; Juice.SlowMo(0.15f, 0.6f); return; }
            Collect();
            Destroy(gameObject);
            return;
        }

        if (!canAct || dist > CollectRadius) return;
        Collect();
        Destroy(gameObject);
    }

    private const float HopTime = 0.35f, OpenTime = 0.45f;
    private static readonly Color[] Loot = { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.95f, 0.6f), Color.white, new Color(0.6f, 0.9f, 1f) };
    private float opening = -1f;

    private void UpdateOpening()
    {
        opening += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(opening / OpenTime);
        transform.position = new Vector3(transform.position.x, baseY, 0f) + (Vector3)(Random.insideUnitCircle * 0.08f * k);
        transform.localScale = baseScale * (1f + 0.25f * k);
        if (Random.value < 0.5f) Juice.Sparks(transform.position, glowColor, 1, 2f + 4f * k, 0.12f);
        if (opening < OpenTime) return;

        Vector2 at = transform.position;
        PixelFx.Burst(Shapes.Starburst, at, new Color(1f, 0.9f, 0.45f, 0.85f), 0.45f, 1.3f, 3f, unscaled: true);
        for (int i = 0; i < 14; i++)
            PixelFx.Emit(Shapes.Sparkle, at + new Vector2(Random.Range(-0.25f, 0.25f), 0f), Vector2.up * Random.Range(6f, 14f),
                         Color.Lerp(glowColor, Color.white, Random.value * 0.6f), Random.Range(0.4f, 0.7f), Random.Range(0.8f, 1.3f), 0f, 0.6f, true);
        PixelFx.Toss(Shapes.Sparkle, at, Loot, 12, 8f, 1f, 1.1f, 14f, unscaled: true);
        PixelFx.Toss(Shapes.PawPrint, at, Loot, 4, 7f, 1f, 0.9f, 14f, unscaled: true);
        Juice.Sparks(at, Color.white, 10, 6f, 0.12f);
        Juice.Flash(new Color(1f, 0.95f, 0.75f, 0.4f), 0.3f);
        CameraFollow.Shake(0.15f, 0.2f);
        Collect();
        Destroy(gameObject);
    }

    private void Collect()
    {
        Vector2 at = player.position;
        Sfx.Play(Sfx.Id.Pickup, 0.7f);
        switch (kind)
        {
            case Kind.Magnet:
                if (XPCollectorManager.Instance != null) XPCollectorManager.Instance.MagnetizeNear(at, 100f);
                ExplosionFx.Ring(at, 14f, new Color(1f, 0.55f, 0.6f), 0.6f);
                Sfx.Play(Sfx.Id.Whoosh, 0.6f, 1.3f);
                Popup("MAGNET!", new Color(1f, 0.45f, 0.45f));
                break;

            case Kind.Bomb:
                float damage = Mathf.Max(200f, stats.AttackDamage * 20f);
                DamageStats.Source = "Bomb Pickup";
                foreach (Collider2D c in Bomb.OverlapEnemies(at, 14f, hits))
                    if (c.TryGetComponent(out EnemyHealth e)) e.TakeDamage(damage, at, false, knockback: false);
                Juice.Flash(new Color(1f, 0.95f, 0.85f, 0.8f), 0.45f);
                CameraFollow.Shake(0.3f, 0.35f);
                Popup("BOOM!", new Color(1f, 0.7f, 0.3f));
                break;

            case Kind.Speed:
                stats.AddTimedSpeed(0.4f, 8f);
                Popup("SPEED UP!", new Color(0.5f, 0.85f, 1f));
                break;

            case Kind.HoloBox:
                RewardMenu.OpenHoloBox();
                break;

            case Kind.Anvil:
                RewardMenu.OpenAnvil();
                break;
        }
    }

    private void Popup(string text, Color color) =>
        DamagePopup.SpawnText((Vector2)player.position + Vector2.up * 0.8f, text, color, 5f);
}
