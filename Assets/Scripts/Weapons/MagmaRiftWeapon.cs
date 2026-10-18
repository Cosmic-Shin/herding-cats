using System.Collections;
using UnityEngine;

public class MagmaRiftWeapon : SuperWeapon
{
    private const int Fissures = 6;
    private static readonly Color Lava = new Color(1f, 0.45f, 0.1f);

    private void Update()
    {
        if (!Ready(5f)) return;
        Vector2 at = Pos;
        Fx(Shapes.Ring, new Color(1f, 0.55f, 0.2f, 0.9f), 1f, at, 0.4f, Area(7f), 44);
        Juice.Smoke(at, 1.5f, 8);
        StompFx.Spawn(at, Area(3f));
        CameraFollow.Shake(0.18f, 0.3f);
        Sfx.Play(Sfx.Id.Boom, 0.9f, 0.55f);
        float turn = Random.Range(0f, 60f);
        for (int i = 0; i < Fissures; i++)
            StartCoroutine(Fissure(at, Rotate(Vector2.right, turn + i * 360f / Fissures + Random.Range(-12f, 12f))));
    }

    private IEnumerator Fissure(Vector2 from, Vector2 dir)
    {
        float length = Area(8f), step = 0.6f, damage = Dmg(16f);
        Vector2 at = from;
        int steps = Mathf.CeilToInt(length / step);
        for (int i = 0; i < steps; i++)
        {
            dir = Rotate(dir, Random.Range(-14f, 14f));
            Vector2 next = at + dir * step;
            Beam(at, dir, step + 0.05f, 0.42f, new Color(0.15f, 0.05f, 0.03f, 0.9f), 3.5f, 3);
            Beam(at, dir, step + 0.05f, 0.18f, Lava, 3.5f, 4);
            if (i % 2 == 0) BurningGround.Spawn(next, Area(0.75f), 3f, Dmg(5f), slow: 0.4f, burns: true);
            HitArea(next, Area(0.8f), damage, e => { EnemyAI cat = Cat(e); if (cat != null) cat.Stun(0.35f); });
            Juice.SparksDir(next, Vector2.up, 35f, Lava, 2, 4f, 0.12f);
            at = next;
            yield return new WaitForSeconds(0.035f);
        }

        Explode(at, Area(2f), Dmg(48f));
        Juice.SparksDir(at, Vector2.up, 40f, new Color(1f, 0.6f, 0.15f), 14, 12f, 0.2f);
        Fx(Shapes.Circle, new Color(1f, 0.75f, 0.3f, 0.9f), Area(1.5f), at, 0.35f, 2.2f, 52);
        BurningGround.Spawn(at, Area(1.6f), 3.5f, Dmg(7f), burns: true);
        Lighting.Flash(at, 4f, Lava, 0.35f);
    }
}
