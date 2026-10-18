using UnityEngine;

// Collab: Glue Bomb + Molotov. Lobs pools of burning tar: cats are stuck (75% slow) in the fire.
public class TarPitWeapon : Weapon
{
    private const int Count = 4;
    private const float Radius = 3.2f;
    private const float Duration = 6f;
    private const float TickDamage = 14f;
    private const float Slow = 0.75f;
    private const float FlightTime = 0.45f;

    private void Update()
    {
        if (!Ready(2.5f)) return;
        Sfx.Play(Sfx.Id.Swish, 0.3f, 0.6f);

        for (int i = 0; i < Count; i++)
        {
            Transform target = i == 0 ? NearestEnemy(8f) : RandomEnemy(8f);
            Vector2 at = target != null ? (Vector2)target.position : Pos + Rotate(Facing, Random.Range(-60f, 60f)) * 3.5f;
            Lob(at);
        }
    }

    private void Lob(Vector2 at)
    {
        Shot s = Shot.Spawn(Shapes.Art("Icons/glue"), new Color(0.55f, 0.3f, 0.15f), 0.6f, Pos);
        s.trail = new Color(1f, 0.5f, 0.1f);
        s.velocity = (at - Pos) / FlightTime;
        s.life = FlightTime;
        s.spin = 540f;

        float radius = Area(Radius), tick = Dmg(TickDamage);
        s.onExpire = shot =>
        {
            // tar splash: dark globs and a few flames flung out, a puff of smoke
            Juice.Sparks(shot.Position, new Color(0.15f, 0.08f, 0.04f), 10, 4f + radius * 2f, 0.25f);
            Juice.Sparks(shot.Position, new Color(1f, 0.55f, 0.15f), 6, 3f + radius, 0.16f);
            Juice.Smoke(shot.Position, radius * 0.6f, 3);
            BurningGround.Spawn(shot.Position, radius, Duration, tick, new Color(0.75f, 0.32f, 0.05f), Slow, burns: true);
        };
    }
}
