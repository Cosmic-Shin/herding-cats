using System.Collections;
using UnityEngine;

// Super: Tesla Coils + Cluster Barrage. Signature: 5 tesla pylons slam down around Mira and link up into a
// crackling web of arcs that shocks anything crossing it; when the web fades, every pylon fires homing missiles.
public class StormNetworkWeapon : SuperWeapon
{
    private const int Pylons = 5;
    private static readonly Color Arc = new Color(0.45f, 0.85f, 1f);

    private void Update()
    {
        if (Ready(5.5f)) StartCoroutine(Network(Pos));
    }

    private IEnumerator Network(Vector2 center)
    {
        float radius = Area(4.8f);
        var points = new Vector2[Pylons];
        var pylons = new Shot[Pylons];
        var gens = new int[Pylons];
        float turn = Random.Range(0f, 72f);
        for (int i = 0; i < Pylons; i++)
        {
            points[i] = center + Rotate(Vector2.up, turn + i * 360f / Pylons) * radius;
            Shot p = Shot.Spawn(Shapes.Star, Arc, 0.9f, points[i], 26);
            p.life = 5f;
            p.spin = 180f;
            pylons[i] = p;
            gens[i] = p.Generation;
            Fx(Shapes.Ring, Arc, 0.5f, points[i], 0.35f, 4f, 44);
            Juice.Sparks(points[i], Arc, 8, 6f, 0.14f);
            StompFx.Spawn(points[i], 1.2f); // the pylon slams into the ground
        }
        Sfx.Play(Sfx.Id.Zap, 0.9f, 0.7f);

        float arcDamage = Dmg(12f);
        const float duration = 3.5f;
        float tick = 0f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            for (int i = 0; i < Pylons; i++)
                if (pylons[i].IsSame(gens[i])) pylons[i].transform.localScale = Vector3.one * (0.9f + 0.15f * Mathf.Sin(t * 20f + i));

            if ((tick -= Time.deltaTime) <= 0f)
            {
                tick = 0.25f;
                for (int i = 0; i < Pylons; i++)
                {
                    // outer ring + a star through the middle: every pylon links to its neighbour and the one after
                    foreach (int j in new[] { (i + 1) % Pylons, (i + 2) % Pylons })
                    {
                        Vector2 a = points[i], b = points[j];
                        LightningFx.SpawnBetween(a, b, Arc, 0.8f);
                        HitLine(a, (b - a).normalized, (b - a).magnitude, 0.55f, arcDamage, e => { EnemyAI cat = Cat(e); if (cat != null) cat.Stun(0.2f); });
                    }
                }
                Sfx.Play(Sfx.Id.Crackle, 0.35f, Random.Range(0.9f, 1.2f));
            }
            yield return null;
        }

        // the web collapses into a missile volley from every pylon
        for (int i = 0; i < Pylons; i++)
        {
            if (pylons[i].IsSame(gens[i])) pylons[i].Release();
            for (int m = 0; m < 3; m++) Missile(points[i], Rotate(Vector2.up, Random.Range(0f, 360f)));
            Lighting.Flash(points[i], 3f, Arc, 0.25f);
        }
        Sfx.Play(Sfx.Id.Whoosh, 0.8f, 1.2f);
    }

    private void Missile(Vector2 from, Vector2 dir)
    {
        Shot s = Shot.Spawn(Shapes.Art("Icons/missiles"), Color.white, 0.55f, from);
        s.velocity = dir * 6f;
        s.life = 3f;
        s.hitRadius = 0.35f;
        s.pierce = 1;
        s.trail = new Color(1f, 0.7f, 0.35f, 0.8f);
        float damage = Dmg(34f), radius = Area(1.7f);
        s.onUpdate = shot =>
        {
            Transform target = NearestTo(shot.Position, 8f);
            Vector2 want = target != null ? ((Vector2)target.position - shot.Position).normalized : shot.velocity.normalized;
            float speed = Mathf.Min(16f, shot.velocity.magnitude + 30f * Time.deltaTime);
            shot.velocity = Vector3.RotateTowards(shot.velocity.normalized, want, 8f * Time.deltaTime, 0f) * speed;
            shot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(shot.velocity.y, shot.velocity.x) * Mathf.Rad2Deg - 90f);
            MissileWeapon.Exhaust(shot);
        };
        s.onHit = (shot, e) => { Explode(shot.Position, radius, damage); shot.Release(); };
        s.onExpire = shot => Explode(shot.Position, radius, damage);
    }
}
