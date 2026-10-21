using UnityEngine;

public class GlueWeapon : Weapon
{
    private static readonly int[] Count = { 2, 2, 2, 3, 3, 3, 4 };
    private static readonly float[] Radius = { 2f, 2f, 2.4f, 2.4f, 2.8f, 2.8f, 3.4f };
    private static readonly float[] SlowAmount = { 0.5f, 0.55f, 0.55f, 0.6f, 0.6f, 0.65f, 0.75f };
    private static readonly float[] Duration = { 3f, 3.5f, 3.5f, 4f, 4f, 4.5f, 5f };
    private static readonly float[] Sting = { 2, 2, 3, 3, 4, 4, 6 };
    private const float FlightTime = 0.4f;

    private void Update()
    {
        if (!Ready(3.5f)) return;
        Sfx.Play(Sfx.Id.Swish, 0.3f, 0.8f);

        for (int i = 0; i < Count[L]; i++)
        {
            Transform target = i == 0 ? NearestEnemy(7f) : RandomEnemy(7f);
            Vector2 at = target != null ? (Vector2)target.position : Pos + Rotate(Facing, Random.Range(-40f, 40f)) * 3f;
            Lob(at);
        }
    }

    private void Lob(Vector2 at)
    {
        Color glue = new Color(0.55f, 1f, 0.35f);
        Shot s = Shot.Spawn(Shapes.Art("Icons/glue"), Color.white, 0.5f, Pos);
        s.trail = glue;
        s.spin = 360f;
        s.velocity = (at - Pos) / FlightTime;
        s.life = FlightTime;

        float radius = Area(Radius[L]), slow = SlowAmount[L], duration = Duration[L];
        float tick = Dmg(Sting[L]);
        s.onExpire = shot =>
        {
            // Splat: a squelch, a ring of goo and droplets flung outward.
            ExplosionFx.Spawn(shot.Position, radius * 0.5f, glue);
            ExplosionFx.Ring(shot.Position, radius * 0.9f, new Color(0.7f, 1f, 0.5f, 0.8f), 0.25f);
            Juice.Sparks(shot.Position, glue, 10, 7f, 0.16f);
            Sfx.Play(Sfx.Id.Pop, 0.35f, 0.55f);
            bool magnet = PlayerCollabs.Has("gluemagnet");
            BurningGround.Spawn(shot.Position, radius, duration, tick, glue, slow, pull: magnet ? 3f : 0f, magnetGems: magnet);
        };
    }
}
