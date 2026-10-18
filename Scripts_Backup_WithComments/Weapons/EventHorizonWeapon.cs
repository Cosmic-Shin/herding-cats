using System.Collections.Generic;
using UnityEngine;

// Collab: Laser Beam + Black Hole. Max-level black holes that spin 4 lasers outward
// while they drag cats in, then pop as usual.
public class EventHorizonWeapon : BlackHoleWeapon
{
    private const int BeamsPerHole = 4;
    private const float BeamLength = 6f;
    private const float BeamWidth = 0.6f;
    private const float SpinSpeed = 160f; // degrees / sec
    private const float TickDamage = 10f;
    private const float TickInterval = 0.15f;

    private readonly List<BlackHole> holes = new List<BlackHole>();
    private readonly List<SpriteRenderer> beams = new List<SpriteRenderer>();
    private static readonly List<Collider2D> boxHits = new List<Collider2D>(64);
    private float tickTimer;

    protected override void OnOpened(BlackHole hole) => holes.Add(hole);

    protected override void Update()
    {
        base.Update(); // opens the holes on cooldown
        holes.RemoveAll(h => h == null); // popped

        tickTimer -= Time.deltaTime;
        bool tick = tickTimer <= 0f;
        if (tick) tickTimer = TickInterval;

        float length = Area(BeamLength), width = Area(BeamWidth), damage = Dmg(TickDamage);
        int used = 0;
        for (int h = 0; h < holes.Count; h++)
        {
            Vector2 center = holes[h].transform.position;
            float spin = Time.time * SpinSpeed + h * 45f; // offset so two holes don't line up
            for (int k = 0; k < BeamsPerHole; k++)
            {
                Vector2 dir = Rotate(Vector2.right, spin + k * 360f / BeamsPerHole);
                DrawBeam(used++, center, dir, length, width);
                if (tick) DamageLine(center, dir, length, width, damage);
            }
        }
        for (int i = used; i < beams.Count; i++) beams[i].gameObject.SetActive(false);
    }

    private void DamageLine(Vector2 from, Vector2 dir, float length, float width, float damage)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = Bomb.EnemyMask, useTriggers = true };
        Physics2D.OverlapBox(from + dir * length / 2f, new Vector2(length, width), Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, filter, boxHits);
        foreach (Collider2D c in boxHits)
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, from, knockback: false);
    }

    private void DrawBeam(int index, Vector2 from, Vector2 dir, float length, float width)
    {
        while (beams.Count <= index)
        {
            var go = new GameObject("HorizonLaser");
            var sr = go.AddComponent<SpriteRenderer>();
            Lighting.Glow(sr);
            sr.sprite = Shapes.Square; // pivot on the left edge
            sr.sortingOrder = 40;
            var core = new GameObject("Core").AddComponent<SpriteRenderer>(); // hot white center of the beam
            Lighting.Glow(core);
            core.transform.SetParent(go.transform, false);
            core.transform.localScale = new Vector3(1f, 0.3f, 1f);
            core.sprite = Shapes.Square;
            core.sortingOrder = 41;
            core.color = new Color(1f, 0.92f, 1f, 0.95f);
            beams.Add(sr);
        }

        SpriteRenderer beam = beams[index];
        beam.gameObject.SetActive(true);
        beam.transform.position = from;
        beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        beam.transform.localScale = new Vector3(length, width * (0.8f + 0.2f * Mathf.Sin(Time.time * 60f)), 1f);
        beam.color = new Color(0.7f, 0.35f, 1f, 0.55f); // softer outer glow now that the core carries the brightness
        if (Random.value < 0.3f) Juice.Sparks(from + dir * length, new Color(0.85f, 0.6f, 1f), 1, 2f, 0.12f); // fizzing tip
    }

    private void OnDisable()
    {
        foreach (SpriteRenderer b in beams) if (b != null) b.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer b in beams) if (b != null) Destroy(b.gameObject);
    }
}
