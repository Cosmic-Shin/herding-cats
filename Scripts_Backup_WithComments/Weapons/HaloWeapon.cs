using UnityEngine;

// Collab: Orbiting Orbs + Damage Aura. Max-level orbs glowing gold, plus a pulse of light
// every second that hits everything out to just past the orbit.
public class HaloWeapon : OrbsWeapon
{
    private const float PulseEvery = 1f;   // shortened by Haste
    private const float PulseRadius = 3.6f;
    private const float PulseDamage = 14f;
    private static readonly Color Gold = new Color(1f, 0.9f, 0.55f);

    private float pulseTimer;

    protected override void OnLevelChanged()
    {
        base.OnLevelChanged(); // makes the orbs
        foreach (Transform orb in orbs) orb.GetComponent<SpriteRenderer>().color = new Color(1f, 0.97f, 0.8f);
    }

    protected override void Update()
    {
        base.Update(); // orbit + contact hits

        pulseTimer -= Time.deltaTime;
        if (pulseTimer > 0f) return;
        pulseTimer = PulseEvery / Mathf.Max(0.1f, stats.attackSpeedMultiplier);

        float radius = Area(PulseRadius), damage = Dmg(PulseDamage);
        foreach (Collider2D c in EnemiesNear(Pos, radius))
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, Pos, knockback: false);

        ExplosionFx.Ring(Pos, radius, Gold, 0.4f);
        Lighting.Flash(Pos, radius * 1.4f, Gold);
        for (int i = 0; i < 10; i++) // holy sparkles drifting up inside the pulse
            Juice.SparksDir(Pos + Random.insideUnitCircle * radius * 0.8f, Vector2.up, 15f, Gold, 1, 1.5f, 0.12f);
        foreach (Transform orb in orbs) Juice.Sparks(orb.position, Gold, 2, 2f, 0.12f);
    }
}
