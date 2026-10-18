using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Super: Cryo Beam + Phoenix Rang. Signature: 4 ice beams sweep one way and 4 fire beams the other. A cat that
// both beams catch within a second cracks from thermal shock: big damage, a burst of ice and flame.
public class FrostfireEclipseWeapon : SuperWeapon
{
    private const int PerSide = 4;
    private const float Duration = 1.8f;
    private static readonly Color Ice = new Color(0.55f, 0.88f, 1f);
    private static readonly Color Fire = new Color(1f, 0.45f, 0.12f);

    private readonly Dictionary<EnemyHealth, float> iced = new Dictionary<EnemyHealth, float>();
    private readonly Dictionary<EnemyHealth, float> burned = new Dictionary<EnemyHealth, float>();

    private void Update()
    {
        if (Ready(5f)) StartCoroutine(Eclipse());
    }

    private IEnumerator Eclipse()
    {
        iced.Clear();
        burned.Clear();
        Sfx.Play(Sfx.Id.Zap, 0.8f, 0.6f);
        Lighting.Flash(Pos, 6f, Color.white, 0.4f);
        Fx(Shapes.Ring, Ice, 1f, Pos, 0.4f, Area(8f), 44);
        Fx(Shapes.Ring, Fire, 1f, Pos, 0.5f, Area(10f), 44);

        // each beam is a soft halo + bright core we steer every frame
        var beams = new Shot[PerSide * 2 * 2];
        var gens = new int[beams.Length];
        for (int i = 0; i < beams.Length; i++)
        {
            bool ice = i < PerSide * 2, core = i % 2 == 1;
            Color c = ice ? Ice : Fire;
            beams[i] = Shot.Spawn(Shapes.Square, core ? Color.Lerp(c, Color.white, 0.7f) : new Color(c.r, c.g, c.b, 0.4f), 1f, Pos, core ? 46 : 45);
            beams[i].life = 10f;
            gens[i] = beams[i].Generation;
        }

        float length = Area(9f), damage = Dmg(14f), shock = Dmg(70f);
        float start = Random.Range(0f, 90f), tick = 0f;
        for (float t = 0f; t < Duration; t += Time.deltaTime)
        {
            float env = Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((Duration - t) / 0.2f); // flare on, thin off
            bool hit = (tick -= Time.deltaTime) <= 0f;
            if (hit) tick = 0.1f;
            for (int b = 0; b < PerSide * 2; b++)
            {
                bool ice = b < PerSide;
                float angle = start + (b % PerSide) * 360f / PerSide + (ice ? t : -t) * 150f + (ice ? 0f : 45f);
                Vector2 dir = Rotate(Vector2.right, angle);
                for (int layer = 0; layer < 2; layer++)
                {
                    Shot beam = beams[b * 2 + layer];
                    if (!beam.IsSame(gens[b * 2 + layer])) continue;
                    beam.transform.SetPositionAndRotation(Pos, Quaternion.Euler(0f, 0f, angle));
                    beam.transform.localScale = new Vector3(length, (layer == 1 ? 0.18f : 0.6f) * env, 1f);
                }
                if (Random.value < 0.3f) Juice.Sparks(Pos + dir * Random.Range(1f, length), ice ? Ice : Fire, 1, 1.5f, 0.12f);
                if (hit) HitLine(Pos, dir, length, 0.5f, damage, e => Mark(e, ice, shock));
            }
            yield return null;
        }
        for (int i = 0; i < beams.Length; i++) if (beams[i].IsSame(gens[i])) beams[i].Release();
    }

    // Ice then fire (or fire then ice) inside a second: thermal shock.
    private void Mark(EnemyHealth e, bool ice, float shock)
    {
        float now = Time.time;
        Dictionary<EnemyHealth, float> mine = ice ? iced : burned, other = ice ? burned : iced;
        mine[e] = now;
        if (ice) { EnemyAI cat = Cat(e); if (cat != null) cat.Freeze(0.6f); }
        else e.MarkBurning();
        if (!other.TryGetValue(e, out float then) || now - then > 1f || e.IsDead) return;

        other.Remove(e);
        mine.Remove(e);
        Vector2 p = e.transform.position;
        Hit(e, shock, p, knockback: false);
        Shatter(p, 1f);
        Juice.SparksDir(p, Vector2.up, 50f, Fire, 8, 7f, 0.16f);
        Fx(Shapes.Ring, Color.white, 0.4f, p, 0.25f, 4f, 50);
        Sfx.Play(Sfx.Id.Shatter, 0.35f, Random.Range(1.1f, 1.3f));
    }
}
