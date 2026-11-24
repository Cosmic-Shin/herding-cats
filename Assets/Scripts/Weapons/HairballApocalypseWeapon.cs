using UnityEngine;

// Hairball Apocalypse (super: Furball Glob + Pounce Barrage): giant glue hairballs fall on the crowd,
// blow up, leave a glue pool and scatter a little minefield.
public class HairballApocalypseWeapon : SuperWeapon
{
    protected override float Tune => 1.3f;
    private const int Drops = 3, MinesPerDrop = 3;
    private const float FallTime = 0.6f, FallHeight = 9f;
    private static readonly Color Slime = new Color(0.62f, 0.85f, 0.35f);

    private void Update()
    {
        if (!Ready(3.5f)) return;
        Sfx.Play(Sfx.Id.Whoosh, 0.6f, 0.6f);
        for (int i = 0; i < Drops; i++)
        {
            Transform target = RandomEnemy(9f);
            Vector2 at = target != null ? (Vector2)target.position : Pos + Random.insideUnitCircle * 5f;
            Drop(at, i * 0.12f);
        }
    }

    private void Drop(Vector2 at, float delay)
    {
        float fall = FallTime + delay, size = Area(1.6f);
        ExplosionFx.Ring(at, Area(2.2f), new Color(Slime.r, Slime.g, Slime.b, 0.9f), fall); // friendly landing marker (red AoeWarning = enemy danger)
        Shot s = Shot.Spawn(Shapes.Art("Icons/hairball"), new Color(0.85f, 1f, 0.75f), size, at + new Vector2(-1f, FallHeight), 57);
        s.velocity = (at - s.Position) / fall;
        s.life = fall;
        s.spin = 540f;
        s.trail = new Color(0.6f, 0.85f, 0.35f, 0.8f);
        s.trailSize = 0.25f;
        s.onExpire = shot => Land(at);
    }

    private void Land(Vector2 at)
    {
        float radius = Area(2.2f);
        Explode(at, radius, Dmg(55f));
        CameraFollow.Shake(0.12f, 0.15f);
        Juice.Sparks(at, Slime, 12, 6f, 0.18f);
        BurningGround.Spawn(at, radius * 0.9f, 4f, Dmg(5f), Slime, 0.7f, palette: LavaArt.FurPal);
        for (int i = 0; i < MinesPerDrop; i++)
        {
            Vector2 spot = at + Random.insideUnitCircle.normalized * Random.Range(1f, radius * 1.3f);
            MineWeapon.SpawnMine(spot, Dmg(30f), Area(1.4f), (pos, r, d) => Explode(pos, r, d));
        }
    }
}
