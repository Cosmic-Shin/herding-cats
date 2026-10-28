using UnityEngine;

public class EarthshakerWeapon : SuperWeapon
{
    private static readonly Color Crack = new Color(1f, 0.7f, 0.35f);
    private float nextCrack;

    public override void OnBombBlast(Vector2 pos, float radius, float damage)
    {
        if (Time.time < nextCrack) return;
        nextCrack = Time.time + 0.12f;
        StompFx.Spawn(pos, radius * 1.2f);
        DamageStats.Source = DisplayName;

        float length = radius * 2.6f + Area(1.5f), dmg = Dmg(18f);
        float turn = Random.Range(0f, 120f);
        for (int i = 0; i < 3; i++)
        {
            Vector2 dir = Rotate(Vector2.right, turn + i * 120f + Random.Range(-20f, 20f));
            Fissure(pos, dir, length, 1f);
            HitLine(pos, dir, length, 0.6f, dmg, e => { EnemyAI cat = Cat(e); if (cat != null) cat.Stun(0.9f); });
            Vector2 end = pos + dir * length;
            Juice.Smoke(end, 0.8f, 3);
            Juice.SparksDir(end, dir, 30f, new Color(0.65f, 0.5f, 0.35f), 4, 6f, 0.14f);
        }
        CameraFollow.Shake(0.07f, 0.12f);
    }

    private static void Fissure(Vector2 from, Vector2 dir, float length, float width, bool branch = true)
    {
        const int Segments = 6;
        Vector2 side = Vector2.Perpendicular(dir);
        Vector2 a = from;
        for (int s = 1; s <= Segments; s++)
        {
            float k = s / (float)Segments;
            Vector2 b = from + dir * length * k + side * Random.Range(-0.35f, 0.35f) * Mathf.Min(1f, length * 0.2f);
            Vector2 seg = b - a;
            float len = seg.magnitude, taper = width * Mathf.Lerp(1f, 0.35f, k);
            Vector2 d = seg / len;
            Beam(a, d, len + 0.06f, 0.42f * taper, new Color(0.16f, 0.09f, 0.05f, 0.9f), 1.4f, 3);
            Beam(a, d, len + 0.06f, 0.14f * taper, Crack, 1f, 4);
            if (branch && s == 2)
                Fissure(b, Rotate(d, Random.value < 0.5f ? 40f : -40f), length * 0.3f, taper * 0.7f, false);
            a = b;
        }
    }
}
