using System.Collections.Generic;
using UnityEngine;

// Collab: Laser Beam + Frost Nova. 6 icy beams fan out all around and sweep in a slow circle, slowing
// everything they touch; when they shut off, every cat they hit freezes solid (frozen cats take +50% damage).
public class CryoBeamWeapon : Weapon
{
    private const float Cooldown = 2.2f;
    private const int Beams = 6;
    private const float Length = 9f;
    private const float Width = 0.7f;
    private const float TickDamage = 9f;
    private const float TickInterval = 0.1f;
    private const float FireTime = 0.7f;
    private const float Sweep = 90f; // degrees / sec
    private const float FreezeTime = 1.2f;
    private static readonly Color Ice = new Color(0.55f, 0.88f, 1f, 0.6f);

    private readonly List<SpriteRenderer> beams = new List<SpriteRenderer>();
    private readonly HashSet<EnemyAI> chilled = new HashSet<EnemyAI>();
    private static readonly List<Collider2D> boxHits = new List<Collider2D>(64);
    private float firing, tickTimer, angle;

    private void Update()
    {
        if (firing <= 0f)
        {
            SetVisible(false);
            if (!Ready(Cooldown)) return;
            Vector2 aim = AimDirection(10f);
            angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            firing = FireTime;
            tickTimer = 0f;
            chilled.Clear();
            Sfx.Play(Sfx.Id.Zap, 0.5f, 0.7f);
            Lighting.Flash(Pos, 3f, Ice, 0.25f);
        }

        firing -= Time.deltaTime;
        tickTimer -= Time.deltaTime;
        angle += Sweep * Time.deltaTime;
        bool tick = tickTimer <= 0f;
        if (tick) tickTimer = TickInterval;

        float width = Area(Width), length = Area(Length);
        for (int i = 0; i < Beams; i++)
        {
            Vector2 dir = Rotate(Vector2.right, angle + i * 360f / Beams);
            Draw(i, dir, length, width);
            if (tick) Damage(dir, length, width);
        }

        if (firing > 0f) return;
        // shut off: everything the beams touched freezes solid
        foreach (EnemyAI cat in chilled)
        {
            if (cat == null || !cat.isActiveAndEnabled) continue;
            cat.Freeze(FreezeTime);
            Juice.Sparks(cat.transform.position, new Color(0.8f, 0.97f, 1f), 3, 3f, 0.12f);
        }
        if (chilled.Count > 0) Sfx.Play(Sfx.Id.Shatter, 0.45f, 1.2f);
    }

    private void Damage(Vector2 dir, float length, float width)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = Bomb.EnemyMask, useTriggers = true };
        Physics2D.OverlapBox(Pos + dir * length / 2f, new Vector2(length, width), Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, filter, boxHits);
        float damage = Dmg(TickDamage);
        foreach (Collider2D c in boxHits)
        {
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, Pos, knockback: false);
            if (c.TryGetComponent(out IEnemyBrain brain)) brain.Slow(0.6f, 0.4f);
            if (c.TryGetComponent(out EnemyAI cat)) chilled.Add(cat);
        }
    }

    private void Draw(int index, Vector2 dir, float length, float width)
    {
        while (beams.Count <= index)
        {
            var go = new GameObject("CryoBeam");
            var sr = go.AddComponent<SpriteRenderer>();
            Lighting.Glow(sr);
            sr.sprite = Shapes.Square; // pivot on the left edge, so it extends from the player
            sr.sortingOrder = 40;
            sr.color = Ice;
            var core = new GameObject("Core").AddComponent<SpriteRenderer>(); // white-hot frozen core
            Lighting.Glow(core);
            core.transform.SetParent(go.transform, false);
            core.transform.localScale = new Vector3(1f, 0.3f, 1f);
            core.sprite = Shapes.Square;
            core.sortingOrder = 41;
            core.color = new Color(0.92f, 0.98f, 1f);
            beams.Add(sr);
        }

        SpriteRenderer beam = beams[index];
        beam.gameObject.SetActive(true);
        beam.transform.position = Pos;
        beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        float age = FireTime - firing;
        float env = age < 0.08f ? Mathf.Lerp(1.8f, 1f, age / 0.08f) : Mathf.Clamp01(firing / 0.15f); // flares on, thins off
        beam.transform.localScale = new Vector3(length, width * env * (0.9f + 0.1f * Mathf.Sin(Time.time * 50f + index)), 1f);
        if (Random.value < 0.4f) // frost crystals drifting off the beam
            Juice.Sparks(Pos + dir * Random.Range(0.5f, length), new Color(0.85f, 0.97f, 1f), 1, 1f, 0.12f);
    }

    private void SetVisible(bool visible)
    {
        foreach (SpriteRenderer b in beams) if (b != null && b.gameObject.activeSelf != visible) b.gameObject.SetActive(visible);
    }

    private void OnDisable() => SetVisible(false);

    private void OnDestroy()
    {
        foreach (SpriteRenderer b in beams) if (b != null) Destroy(b.gameObject);
    }
}
