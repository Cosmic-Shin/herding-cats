using System.Collections.Generic;
using UnityEngine;

public class LaserWeapon : Weapon
{
    private static readonly float[] Cooldown = { 3f, 3f, 3f, 2.6f, 2.6f, 2.2f, 2f };
    private static readonly float[] Width = { 0.4f, 0.4f, 0.55f, 0.55f, 0.7f, 0.7f, 0.9f };
    private static readonly float[] TickDamage = { 6, 7, 7, 9, 9, 11, 14 };
    private static readonly float[] Length = { 8f, 8f, 8f, 9f, 9f, 10f, 12f };
    private static readonly int[] Beams = { 3, 3, 3, 4, 4, 5, 8 };
    private const float FanSpread = 20f;
    private const float FireTime = 0.5f;
    private const float TickInterval = 0.1f;

    private readonly List<SpriteRenderer> beams = new List<SpriteRenderer>();
    private static readonly List<Collider2D> boxHits = new List<Collider2D>(64);
    private Vector2 aim;
    private float firing;
    private float tickTimer;
    private static readonly Color BeamColor = new Color(1f, 0.35f, 0.4f, 0.8f);

    private void Update()
    {
        if (firing <= 0f)
        {
            SetBeamsVisible(false);
            if (!Ready(Cooldown[L])) return;
            aim = AimDirection(10f);
            firing = FireTime;
            Sfx.Play(Sfx.Id.Zap, 0.45f);
            tickTimer = 0f;
            Lighting.Flash(Pos, 2.5f, BeamColor, 0.2f);
            Juice.Sparks(Pos, new Color(1f, 0.6f, 0.6f), 6, 4f, 0.12f);
        }

        firing -= Time.deltaTime;
        tickTimer -= Time.deltaTime;
        int count = Beams[L];
        bool allAround = Level >= MaxLevel;
        float width = Area(Width[L]), length = Length[L];

        for (int i = 0; i < count; i++)
        {
            Vector2 dir = allAround ? Rotate(aim, i * 360f / count) : Rotate(aim, (i - (count - 1) / 2f) * FanSpread);
            DrawBeam(i, dir, length, width);
            if (tickTimer <= 0f) DamageLine(dir, length, width);
        }
        if (tickTimer <= 0f) tickTimer = TickInterval;
    }

    private void DamageLine(Vector2 dir, float length, float width)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = Bomb.EnemyMask, useTriggers = true };
        Vector2 center = Pos + dir * length / 2f;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Physics2D.OverlapBox(center, new Vector2(length, width), angle, filter, boxHits);

        float damage = Dmg(TickDamage[L]);
        foreach (Collider2D c in boxHits)
        {
            if (!c.TryGetComponent(out EnemyHealth e)) continue;
            Hit(e, damage, Pos, knockback: false);
            Juice.Sparks(c.transform.position, BeamColor, 2, 3f, 0.1f);
        }
    }

    private void DrawBeam(int index, Vector2 dir, float length, float width)
    {
        while (beams.Count <= index)
        {
            var go = new GameObject("Laser");
            var sr = go.AddComponent<SpriteRenderer>();
            Lighting.Glow(sr);
            sr.sprite = Shapes.Square;
            sr.sortingOrder = 40;
            var core = new GameObject("Core").AddComponent<SpriteRenderer>();
            Lighting.Glow(core);
            core.transform.SetParent(go.transform, false);
            core.transform.localScale = new Vector3(1f, 0.35f, 1f);
            core.sprite = Shapes.Square;
            core.color = new Color(1f, 0.92f, 0.92f);
            core.sortingOrder = 41;
            beams.Add(sr);
        }

        SpriteRenderer beam = beams[index];
        beam.gameObject.SetActive(true);
        beam.transform.position = Pos;
        beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        float age = FireTime - firing;
        float env = age < 0.06f ? Mathf.Lerp(1.8f, 1f, age / 0.06f) : Mathf.Clamp01(firing / 0.12f);
        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 60f + index);
        beam.transform.localScale = new Vector3(length, width * flicker * env, 1f);
        beam.color = BeamColor;
        if (Random.value < 0.5f) Juice.Sparks(Pos + dir * Random.Range(0.5f, length), new Color(1f, 0.55f, 0.55f), 1, 2f, 0.12f);
    }

    private void SetBeamsVisible(bool visible)
    {
        foreach (SpriteRenderer b in beams) if (b != null && b.gameObject.activeSelf != visible) b.gameObject.SetActive(visible);
    }

    private void OnDisable() => SetBeamsVisible(false);

    private void OnDestroy()
    {
        foreach (SpriteRenderer b in beams) if (b != null) Destroy(b.gameObject);
    }
}
