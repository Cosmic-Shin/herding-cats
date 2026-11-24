using UnityEngine;

// Hairball Cannon (shop weapon): a heavy, slow hairball at the nearest cat that plows through a few of them,
// then splats into a slime puddle that slows whatever walks in.
// Combos reuse this class (like Campfire reuses Molotov): Furball Glob (+Glue), Hairball Volley (+Missiles), Deadeye Hairball (+Sharpshooter).
public class HairballWeapon : Weapon
{
    private static readonly int[] Count = { 1, 1, 2, 2, 2, 3, 3 };
    private static readonly int[] Pierce = { 3, 3, 3, 4, 4, 5, 999 };
    private static readonly float[] Damage = { 18, 22, 22, 27, 27, 32, 40 };
    private static readonly float[] Puddle = { 1f, 1f, 1.1f, 1.1f, 1.35f, 1.35f, 1.7f };
    private const float Speed = 9f, Range = 1.1f, Slow = 0.4f, PuddleTime = 3.5f, TurnRate = 260f;
    private static readonly Color Slime = new Color(0.62f, 0.78f, 0.35f);

    private static bool Furball => PlayerCollabs.Has("furball");
    private static bool Volley => PlayerCollabs.Has("hairvolley");
    private static bool Deadeye => PlayerCollabs.Has("deadeye");

    private void Update()
    {
        if (!Ready(Volley ? 1.8f : 2.2f)) return;
        Sfx.Play(Sfx.Id.Pop, 0.5f, Furball ? 0.45f : 0.6f);
        if (Volley)
        {
            // 4 homing hairballs, each locked on its own cat when there are enough
            for (int i = 0; i < 4; i++)
            {
                Transform target = RandomEnemy(10f);
                Fire(Rotate(AimDirection(10f), (i - 1.5f) * 25f), target);
            }
            return;
        }
        Vector2 aim = AimDirection(9f);
        int n = Count[L] + (Furball ? 1 : 0);
        for (int i = 0; i < n; i++) Fire(Rotate(aim, (i - (n - 1) / 2f) * 14f), null);
    }

    private void Fire(Vector2 dir, Transform target)
    {
        bool furball = Furball, deadeye = Deadeye;
        float size = furball ? 1.15f : 0.75f;
        float speed = Speed * (deadeye ? 1.8f : 1f);
        Shot s = Shot.Spawn(Shapes.Art("Icons/hairball"), furball ? new Color(0.8f, 1f, 0.7f) : Color.white, size, Pos);
        s.velocity = dir * speed;
        s.life = Range * (Volley ? 2f : 1f);
        s.hitRadius = furball ? 0.75f : 0.5f;
        s.spin = 360f;
        s.trail = furball ? new Color(0.6f, 0.85f, 0.35f, 0.8f) : new Color(0.75f, 0.55f, 0.35f, 0.7f);
        s.trailSize = furball ? 0.18f : 0.12f;
        if (deadeye) s.ghost = new Color(1f, 0.9f, 0.6f, 0.45f);

        float damage = Dmg(Damage[L]) * (furball ? 1.3f : deadeye ? 1.5f : 1f);
        float radius = Area(Puddle[L]) * (furball ? 1.4f : 1f);
        int hitsLeft = furball || deadeye ? 999 : Volley ? 2 : Pierce[L];

        if (target != null)
        {
            EnemyHealth locked = target.GetComponent<EnemyHealth>();
            int life = locked != null ? locked.Life : 0;
            s.onUpdate = shot =>
            {
                if (locked == null || locked.IsDead || locked.Life != life) return;
                Vector2 want = ((Vector2)locked.transform.position - shot.Position).normalized;
                float angle = Vector2.SignedAngle(shot.velocity, want);
                shot.velocity = Rotate(shot.velocity, Mathf.Clamp(angle, -TurnRate * Time.deltaTime, TurnRate * Time.deltaTime));
            };
        }

        // Pierce is counted here (not Shot.pierce) so the last hit still splats a puddle.
        s.onHit = (shot, e) =>
        {
            Hit(e, damage, shot.Position);
            if (furball && e.TryGetComponent(out EnemyAI cat)) cat.Slow(0.6f, 2.5f); // glued on contact
            Juice.SparksDir(shot.Position, shot.velocity, 50f, new Color(0.8f, 0.6f, 0.4f), 4, 5f, 0.1f);
            if (--hitsLeft > 0) return;
            Splat(shot.Position, radius, damage);
            shot.Release();
        };
        s.onExpire = shot => Splat(shot.Position, radius, damage);
    }

    private void Splat(Vector2 at, float radius, float damage)
    {
        bool furball = Furball;
        Sfx.Play(Sfx.Id.Pop, 0.35f, 0.5f);
        Juice.Sparks(at, Slime, 8, 4f, 0.14f);
        ExplosionFx.Ring(at, radius, Slime, 0.25f);
        BurningGround.Spawn(at, radius, furball ? 5f : PuddleTime, Dmg(furball ? 4f : 2f), Slime, furball ? 0.7f : Slow,
                            palette: furball ? LavaArt.FurPal : LavaArt.SlimePal);
        if (Volley) Explode(at, Area(1.2f), damage * 0.6f); // volley hairballs burst
    }
}
