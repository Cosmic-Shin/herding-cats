using System.Collections;
using UnityEngine;

public class TempestBladesWeapon : SuperWeapon
{
    private const int Blades = 6;
    private static readonly Color Storm = new Color(0.55f, 0.85f, 1f);

    private void Update()
    {
        if (Ready(5f)) StartCoroutine(Tempest());
    }

    private IEnumerator Tempest()
    {
        Fx(Shapes.Ring, new Color(0.25f, 0.3f, 0.45f, 0.7f), Area(16f), Pos, 0.45f, 0.15f, 44);
        Sfx.Play(Sfx.Id.Whoosh, 0.7f, 0.7f);
        yield return new WaitForSeconds(0.35f);
        Sfx.Play(Sfx.Id.Zap, 0.8f, 0.8f);
        CameraFollow.Shake(0.12f, 0.2f);

        float damage = Dmg(34f), zapDamage = Dmg(30f), size = Area(1.7f), reach = Area(8.5f);
        var blades = new Shot[Blades];
        var gens = new int[Blades];
        for (int i = 0; i < Blades; i++)
        {
            Shot s = Shot.Spawn(Shapes.Projectile("Icons/tempest", 1), Color.white, size, Pos, 25);
            s.life = 3f;
            s.spin = 1300f;
            s.trail = new Color(0.6f, 0.9f, 1f, 0.7f);
            s.trailSize = 0.25f;
            s.ghost = new Color(0.45f, 0.8f, 1f, 0.35f);
            s.hitRadius = size * 0.45f;
            s.onHit = (shot, e) => Hit(e, damage, shot.Position);
            blades[i] = s;
            gens[i] = s.Generation;
        }

        const float duration = 2.6f;
        float zap = 0f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            for (int i = 0; i < Blades; i++)
            {
                if (!blades[i].IsSame(gens[i])) continue;
                float angle = i * 360f / Blades + t * 240f;
                blades[i].transform.position = Pos + Rotate(Vector2.right, angle) * Mathf.Lerp(1f, reach, Mathf.Sqrt(k));
                if (Random.value < 0.08f) blades[i].ClearHits();
            }

            if ((zap -= Time.deltaTime) <= 0f)
            {
                zap = 0.12f;
                int pick = Random.Range(0, Blades);
                if (blades[pick].IsSame(gens[pick]))
                {
                    Vector2 p = blades[pick].Position;
                    LightningFx.Spawn(p, Storm, 1.5f);
                    HitArea(p, Area(1.4f), zapDamage);
                    if (Random.value < 0.4f) BurningGround.Spawn(p, Area(0.9f), 2.5f, Dmg(6f), burns: true);
                    Lighting.Flash(p, 3f, Storm, 0.15f);
                }
            }
            yield return null;
        }
        for (int i = 0; i < Blades; i++) if (blades[i].IsSame(gens[i])) blades[i].Release();
    }
}
