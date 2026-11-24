using UnityEngine;

// Tidal Wave (super: Grilled Sardines + Hiss Storm): a wall of sardines sweeps across the screen through you,
// shoving every cat it hits along with it; some get struck by lightning on the way.
public class TidalWaveWeapon : SuperWeapon
{
    protected override float Tune => 1.3f;
    private const int Fish = 9;
    private const float Speed = 13f, Start = 9f, Gap = 0.85f, StrikeChance = 0.25f;
    private static readonly Color Water = new Color(0.55f, 0.85f, 1f);

    private void Update()
    {
        if (!Ready(4.5f)) return;
        Sfx.Play(Sfx.Id.Whoosh, 0.9f, 0.5f);
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector2 across = Vector2.Perpendicular(dir);
        Vector2 start = Pos - dir * Start;
        for (int i = 0; i < Fish; i++)
            Swim(start + across * (i - (Fish - 1) / 2f) * Gap + dir * Random.Range(-0.3f, 0.3f), dir);
    }

    private void Swim(Vector2 from, Vector2 dir)
    {
        float damage = Dmg(28f), strike = Dmg(24f);
        Shot s = Shot.Spawn(Shapes.Art("Icons/sardine"), Color.white, Area(0.8f), from, 30);
        s.velocity = dir * Speed;
        s.life = Start * 2f / Speed;
        s.hitRadius = Area(0.6f);
        s.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        s.trail = new Color(Water.r, Water.g, Water.b, 0.7f);
        s.trailSize = 0.14f;
        s.onHit = (shot, e) =>
        {
            Hit(e, damage, shot.Position - dir); // knocked back along the wave
            Juice.SparksDir(shot.Position, dir, 60f, Water, 3, 5f, 0.1f);
            if (Random.value >= StrikeChance || e.IsDead) return;
            LightningFx.Spawn(e.transform.position, new Color(0.6f, 0.85f, 1f));
            Hit(e, strike, e.transform.position, knockback: false);
        };
    }
}
