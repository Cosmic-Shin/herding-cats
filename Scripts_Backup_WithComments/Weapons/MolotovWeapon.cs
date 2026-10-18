using UnityEngine;

// Lobs bottles at enemies; each leaves a fire puddle that burns whatever stands in it.
public class MolotovWeapon : Weapon
{
    private static readonly int[] Count = { 2, 2, 3, 3, 3, 4, 5 };
    private static readonly float[] TickDamage = { 6, 7, 7, 8, 9, 9, 12 };
    private static readonly float[] Radius = { 1.6f, 1.6f, 1.6f, 1.9f, 1.9f, 2.1f, 2.5f };
    private static readonly float[] Duration = { 4f, 4f, 4f, 4.5f, 4.5f, 5f, 6f };
    private const float FlightTime = 0.45f;

    private void Update()
    {
        if (!Ready(3f)) return;
        Sfx.Play(Sfx.Id.Swish, 0.3f, 0.7f);

        for (int i = 0; i < Count[L]; i++)
        {
            Transform target = RandomEnemy(7f);
            Vector2 at = target != null ? (Vector2)target.position : Pos + Rotate(Facing, Random.Range(-40f, 40f)) * 3.5f;
            Lob(at);
        }
    }

    private void Lob(Vector2 at)
    {
        Shot s = Shot.Spawn(Shapes.Art("Icons/molotov"), Color.white, 0.55f, Pos);
        s.trail = new Color(1f, 0.55f, 0.15f);
        s.trailSize = 0.12f;
        s.velocity = (at - Pos) / FlightTime;
        s.life = FlightTime;
        s.spin = 720f;

        float radius = Area(Radius[L]), duration = Duration[L], tick = Dmg(TickDamage[L]);
        s.onExpire = shot =>
        {
            // the bottle smashes: glass glints, a whoosh of flame, smoke
            Juice.Sparks(shot.Position, new Color(0.75f, 0.95f, 0.85f), 6, 5f, 0.1f);
            Juice.SparksDir(shot.Position, Vector2.up, 50f, new Color(1f, 0.6f, 0.15f), 8, 5f + radius * 2f, 0.2f);
            Juice.Smoke(shot.Position, radius * 0.5f, 2);
            Lighting.Flash(shot.Position, radius * 2f, new Color(1f, 0.6f, 0.2f), 0.2f);
            BurningGround.Spawn(shot.Position, radius, duration, tick, healPlayer: PlayerCollabs.Has("campfire") ? 2f : 0f);
        };
    }
}
