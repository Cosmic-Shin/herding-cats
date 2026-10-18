using System.Collections.Generic;
using UnityEngine;

// HoloCure-style hamburger pickup: dropped by enemies, walk over it to heal.
// The sprite is drawn from the pixel map below, so no art asset is needed
// (swap in a real sprite by assigning one to the SpriteRenderer later).
public class Burger : MonoBehaviour
{
    public const float HealPercent = 0.2f; // of max HP
    public static float DropChanceMultiplier = 1f; // raised by the Gourmet skill

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

    public static readonly List<Burger> Active = new List<Burger>(); // for Blink to aim at
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    private static Sprite sprite;
    private static Transform player;
    private static PlayerStats playerStats;

    private Vector3 basePos;
    private float age;

    public static void Spawn(Vector2 pos)
    {
        pos = MapGenerator.PlaceDrop(pos);
        var go = new GameObject("Burger");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSprite();
        sr.sortingOrder = 4; // under enemies and player
        go.AddComponent<Burger>();
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
            // eaten: pop up, then shrink into the player
            eatTime += Time.deltaTime / EatDuration;
            float hop = Mathf.Sin(Mathf.Clamp01(eatTime) * Mathf.PI) * 0.4f;
            transform.position = Vector3.Lerp(basePos, player.position, eatTime * eatTime) + Vector3.up * hop;
            transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 0f, eatTime * eatTime);
            if (eatTime >= 1f) Eat();
            return;
        }

        age += Time.deltaTime;
        float dropHop = age < 0.35f ? Mathf.Sin(Mathf.PI * age / 0.35f) * 0.5f : 0f; // hops out of the cat when it drops
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 4f) * 0.03f + dropHop); // little bob
        transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(age / 0.15f));

        if (player != null && Vector2.Distance(basePos, player.position) <= PickupRadius) eatTime = 0f;
    }

    private const float EatDuration = 0.2f;
    private float eatTime = -1f; // < 0 = sitting on the ground

    private void Eat()
    {
        float amount = playerStats.maxHealth * HealPercent * playerStats.burgerHealMultiplier;
        playerStats.Heal(amount);
        DamagePopup.SpawnText(player.position, "+" + Mathf.RoundToInt(amount), new Color(0.4f, 1f, 0.4f), 4.5f);
        Sfx.Play(Sfx.Id.Pickup, 0.6f, 0.8f);
        ExplosionFx.Spawn(player.position, 0.6f, new Color(0.5f, 1f, 0.4f), order: 9);
        // heal sparkles: green and pink, plus a soft green screen tint
        Juice.Sparks(player.position, new Color(0.5f, 1f, 0.5f), 8, 3f, 0.15f);
        Juice.Sparks(player.position, new Color(1f, 0.5f, 0.7f), 5, 2.5f, 0.15f);
        Juice.Flash(new Color(0.4f, 1f, 0.4f, 0.15f), 0.25f);
        if (player.TryGetComponent(out PlayerAnimator anim)) anim.Squash(new Vector2(0.9f, 1.15f), 0.15f);
        Destroy(gameObject);
    }

    private static Sprite GetSprite()
    {
        if (sprite != null) return sprite;

        int w = Pixels[0].Length, h = Pixels.Length;
        var tex = new Texture2D(w, h) { filterMode = FilterMode.Point };
        for (int row = 0; row < h; row++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - row, ColorFor(Pixels[row][x]));
        tex.Apply();
        sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f); // 12px wide -> ~0.37 units, about a third of the player
        return sprite;
    }

    private static Color ColorFor(char c)
    {
        switch (c)
        {
            case 'b': return new Color(0.87f, 0.56f, 0.25f); // bun
            case 'w': return new Color(1f, 0.95f, 0.8f);     // sesame
            case 'g': return new Color(0.35f, 0.8f, 0.3f);   // lettuce
            case 'r': return new Color(0.85f, 0.2f, 0.15f);  // tomato
            case 'm': return new Color(0.42f, 0.22f, 0.12f); // patty
            case 'y': return new Color(1f, 0.8f, 0.2f);      // cheese
            default: return Color.clear;
        }
    }
}
