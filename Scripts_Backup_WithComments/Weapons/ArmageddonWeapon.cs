using UnityEngine;

// Super: Carpet Bomb + Cluster Barrage (bomb super). Every throw also calls in an airstrike: 5 target marks
// light up around the target, then missiles scream down from the sky onto each one.
public class ArmageddonWeapon : SuperWeapon
{
    private float nextStrike;

    public override void OnThrow(Vector2 target)
    {
        if (Time.time < nextStrike) return;
        nextStrike = Time.time + 1.1f / Mathf.Max(0.1f, stats.attackSpeedMultiplier);
        Airstrike(target);
    }

    private void Airstrike(Vector2 center)
    {
        DamageStats.Source = DisplayName;
        const float warn = 0.55f;
        float radius = Area(1.8f), damage = Dmg(42f);
        var spots = new Vector2[5];
        for (int i = 0; i < spots.Length; i++)
        {
            spots[i] = center + (i == 0 ? Vector2.zero : Random.insideUnitCircle * Area(3.2f));
            AoeWarning.Spawn(spots[i], radius, warn + i * 0.07f);
        }
        Sfx.Play(Sfx.Id.Alert, 0.35f, 1.4f);

        for (int i = 0; i < spots.Length; i++)
        {
            Vector2 spot = spots[i];
            Shot m = Shot.Spawn(Shapes.Art("Icons/missiles"), Color.white, 0.7f, spot + new Vector2(-2.5f, 9f), 57);
            m.life = warn + i * 0.07f;
            m.velocity = (spot - m.Position) / m.life;
            m.trail = new Color(1f, 0.6f, 0.25f, 0.9f);
            m.trailSize = 0.22f;
            m.onUpdate = MissileWeapon.Exhaust;
            m.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(m.velocity.y, m.velocity.x) * Mathf.Rad2Deg - 90f);
            m.onExpire = shot =>
            {
                Explode(spot, radius, damage);
                Juice.SparksDir(spot, Vector2.up, 50f, new Color(1f, 0.7f, 0.3f), 8, 10f, 0.18f);
            };
        }
    }
}
