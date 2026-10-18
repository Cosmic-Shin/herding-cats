using UnityEngine;

// Super: Concussion Bombs + Seismic Field (bomb super). Every bomb blast cracks the ground: 3 glowing fissures
// shoot out from it, stunning and damaging every cat along them, with dust and debris flying.
public class EarthshakerWeapon : SuperWeapon
{
    private static readonly Color Crack = new Color(1f, 0.7f, 0.35f);
    private float nextCrack;

    public override void OnBombBlast(Vector2 pos, float radius, float damage)
    {
        if (Time.time < nextCrack) return;
        nextCrack = Time.time + 0.12f;
        StompFx.Spawn(pos, radius * 1.2f); // the blast cracks the ground
        DamageStats.Source = DisplayName;

        float length = radius * 2.6f + Area(1.5f), dmg = Dmg(18f);
        float turn = Random.Range(0f, 120f);
        for (int i = 0; i < 3; i++)
        {
            Vector2 dir = Rotate(Vector2.right, turn + i * 120f + Random.Range(-20f, 20f));
            Beam(pos, dir, length, 0.38f, new Color(0.18f, 0.1f, 0.05f, 0.85f), 1.4f, 3);
            Beam(pos, dir, length, 0.13f, Crack, 1f, 4);
            HitLine(pos, dir, length, 0.6f, dmg, e => { EnemyAI cat = Cat(e); if (cat != null) cat.Stun(0.9f); });
            Vector2 end = pos + dir * length;
            Juice.Smoke(end, 0.8f, 3);
            Juice.SparksDir(end, dir, 30f, new Color(0.65f, 0.5f, 0.35f), 4, 6f, 0.14f); // debris
        }
        CameraFollow.Shake(0.07f, 0.12f);
    }
}
