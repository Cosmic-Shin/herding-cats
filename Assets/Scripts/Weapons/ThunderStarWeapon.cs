using UnityEngine;

public class ThunderStarWeapon : ShurikenWeapon
{
    private const float StrikeDamage = 18f;
    private const float StrikeRadius = 1.3f;

    protected override Color StarTint => new Color(1f, 0.95f, 0.55f);

    protected override void OnStarHit(Vector2 at)
    {
        float damage = Dmg(StrikeDamage), radius = Area(StrikeRadius);
        LightningFx.Spawn(at);
        ExplosionFx.Ring(at, radius, new Color(1f, 0.9f, 0.35f), 0.2f);
        Sfx.Play(Sfx.Id.Crackle, 0.3f);
        foreach (Collider2D c in EnemiesNear(at, radius))
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, at, knockback: false);
    }
}
