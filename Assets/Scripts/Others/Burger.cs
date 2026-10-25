using System.Collections.Generic;
using UnityEngine;

public class Burger : MonoBehaviour
{
    public const float HealPercent = 0.2f;
    public static float DropChanceMultiplier = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => DropChanceMultiplier = 1f;
    private const float PickupRadius = 0.45f;

    private static readonly string[] Pixels =
    {
        "...bbbbbb...",
        "..bbwbbbwb..",
        ".bbbbbbbbbb.",
        ".bwbbbbbwbb.",
        "gggggggggggg",
        ".rrrrrrrrrr.",
        ".mmmmmmmmmm.",
        "yyyyyyyyyyyy",
        ".bbbbbbbbbb.",
        "..bbbbbbbb..",
    };

    public static readonly List<Burger> Active = new List<Burger>();
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    private static Sprite sprite;
    private static Transform player;
    private static PlayerStats playerStats;

    private Vector3 basePos;
    private SpriteRenderer halo, rays;
    private float age;

    public const int MaxOnField = 12;
    public const float DropRateScale = 0.75f; // global nerf on every regular cat's burgerDropChance

    public static void Spawn(Vector2 pos)
    {
        if (Active.Count >= MaxOnField) return;
        pos = MapGenerator.PlaceDrop(pos);
        var go = new GameObject("Burger");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSprite();
        sr.sortingOrder = 8; // with the other pickups, above gems and ground effects
        var b = go.AddComponent<Burger>();
        // A soft gold glow and slowly turning rays behind it, so burgers read through late-game clutter.
        b.halo = b.Layer("Glow", Shapes.SoftGlow);
        b.rays = b.Layer("Rays", Shapes.SoftRays);
    }

    private SpriteRenderer Layer(string name, Sprite s)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(transform, false);
        r.sprite = s;
        r.sortingOrder = 7;
        Lighting.Glow(r); // unlit, so it stays bright at dusk
        return r;
    }

    private void Start()
    {
        basePos = transform.position;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { player = p.transform; playerStats = p.GetComponent<PlayerStats>(); }
        }
    }

    private void Update()
    {
        if (eatTime >= 0f)
        {
            eatTime += Time.deltaTime / EatDuration;
            float hop = Mathf.Sin(Mathf.Clamp01(eatTime) * Mathf.PI) * 0.4f;
            transform.position = Vector3.Lerp(basePos, player.position, eatTime * eatTime) + Vector3.up * hop;
            transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 0f, eatTime * eatTime);
            if (eatTime >= 1f) Eat();
            return;
        }

        age += Time.deltaTime;
        float dropHop = age < 0.35f ? Mathf.Sin(Mathf.PI * age / 0.35f) * 0.5f : 0f;
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 4f) * 0.03f + dropHop);
        transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(age / 0.15f));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
        halo.transform.localScale = Vector3.one * (1.1f + 0.2f * pulse);
        halo.color = new Color(1f, 0.78f, 0.3f, 0.75f + 0.25f * pulse); // warm gold: stands out on grass, snow and ash
        rays.transform.localScale = Vector3.one * (1.7f + 0.2f * pulse);
        rays.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 25f);
        rays.color = new Color(1f, 0.93f, 0.65f, 0.55f + 0.3f * pulse);
        if (Random.value < Time.deltaTime * 2f) // an occasional twinkle
            Juice.Sparks((Vector2)transform.position + Random.insideUnitCircle * 0.3f, new Color(1f, 0.95f, 0.7f), 1, 0.4f, 0.08f);

        if (player != null && Vector2.Distance(basePos, player.position) <= PickupRadius) eatTime = 0f;
    }

    private const float EatDuration = 0.2f;
    private float eatTime = -1f;

    private void Eat()
    {
        float amount = playerStats.maxHealth * HealPercent * playerStats.burgerHealMultiplier;
        playerStats.Heal(amount);
        DamagePopup.SpawnText(player.position, "+" + Mathf.RoundToInt(amount), new Color(0.4f, 1f, 0.4f), 4.5f);
        Sfx.Play(Sfx.Id.Pickup, 0.6f, 0.8f);
        ExplosionFx.Spawn(player.position, 0.6f, new Color(0.5f, 1f, 0.4f), order: 9);
        Juice.Sparks(player.position, new Color(0.5f, 1f, 0.5f), 8, 3f, 0.15f);
        Juice.Sparks(player.position, new Color(1f, 0.5f, 0.7f), 5, 2.5f, 0.15f);
        Juice.Flash(new Color(0.4f, 1f, 0.4f, 0.15f), 0.25f);
        if (player.TryGetComponent(out PlayerAnimator anim)) anim.Squash(new Vector2(0.9f, 1.15f), 0.15f);
        Destroy(gameObject);
    }

    private static Sprite GetSprite()
    {
        if (sprite != null) return sprite;

        // One pixel of dark outline around the art so it reads on any ground.
        int w = Pixels[0].Length, h = Pixels.Length;
        bool Solid(int x, int row) => x >= 0 && row >= 0 && x < w && row < h && Pixels[row][x] != '.';
        var tex = new Texture2D(w + 2, h + 2) { filterMode = FilterMode.Point };
        for (int row = -1; row <= h; row++)
            for (int x = -1; x <= w; x++)
            {
                Color c = Solid(x, row) ? ColorFor(Pixels[row][x])
                        : Solid(x - 1, row) || Solid(x + 1, row) || Solid(x, row - 1) || Solid(x, row + 1) ? new Color(0.1f, 0.05f, 0.04f) : Color.clear;
                tex.SetPixel(x + 1, h - row, c);
            }
        tex.Apply();
        sprite = Sprite.Create(tex, new Rect(0, 0, w + 2, h + 2), new Vector2(0.5f, 0.5f), 26f); // a bit bigger than before (was 32)
        return sprite;
    }

    private static Color ColorFor(char c)
    {
        switch (c)
        {
            case 'b': return new Color(0.87f, 0.56f, 0.25f);
            case 'w': return new Color(1f, 0.95f, 0.8f);
            case 'g': return new Color(0.35f, 0.8f, 0.3f);
            case 'r': return new Color(0.85f, 0.2f, 0.15f);
            case 'm': return new Color(0.42f, 0.22f, 0.12f);
            case 'y': return new Color(1f, 0.8f, 0.2f);
            default: return Color.clear;
        }
    }
}
