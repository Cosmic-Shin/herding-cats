using UnityEngine;

// Sardine Rain (shop weapon): sardines drop out of the sky onto cats around you and splash everything nearby.
// Combos reuse this class: Fish Traps (+Mines), Grilled Sardines (+Molotov), Fish Feast (+Gourmet).
public class SardineRainWeapon : Weapon
{
    private static readonly int[] Count = { 3, 3, 4, 4, 5, 6, 8 };
    private static readonly float[] Damage = { 14, 17, 17, 21, 21, 25, 32 };
    private static readonly float[] Splash = { 1.2f, 1.2f, 1.2f, 1.4f, 1.4f, 1.6f, 1.9f };
    private const float FallTime = 0.45f, FallHeight = 7f, Reach = 7f;
    private const float FeastHealPerCat = 0.5f, FeastHealMax = 3f, FeastSnackChance = 0.03f;
    private static readonly Color Water = new Color(0.55f, 0.85f, 1f);

    private static bool Traps => PlayerCollabs.Has("fishtraps");
    private static bool Grilled => PlayerCollabs.Has("grilled");
    private static bool Feast => PlayerCollabs.Has("fishfeast");

    private void Update()
    {
        if (!Ready(L >= 6 ? 2f : 2.5f)) return;
        int n = Count[L] + (Grilled ? 2 : 0);
        for (int i = 0; i < n; i++)
        {
            Transform target = RandomEnemy(Reach);
            Vector2 at = target != null ? (Vector2)target.position + Random.insideUnitCircle * 0.4f : Pos + Random.insideUnitCircle * Reach * 0.6f;
            Drop(at, i * 0.06f, i % 2 == 0);
        }
    }

    private void Drop(Vector2 at, float delay, bool trap)
    {
        bool grilled = Grilled;
        float damage = Dmg(Damage[L]), radius = Area(Splash[L]), fall = FallTime + delay;
        Shot s = Shot.Spawn(Shapes.Art("Icons/sardine"), grilled ? new Color(1f, 0.75f, 0.5f) : Color.white, 0.6f, at + new Vector2(-0.6f, FallHeight), 30);
        s.velocity = (at - s.Position) / fall;
        s.life = fall;
        s.transform.rotation = Quaternion.Euler(0f, 0f, -70f); // nose down
        s.trail = grilled ? new Color(1f, 0.55f, 0.15f, 0.8f) : new Color(0.75f, 0.9f, 1f, 0.6f);
        s.trailSize = 0.08f;
        s.onExpire = shot => Land(at, radius, damage, trap);
    }

    // 'alt' = every other sardine: only those become mines (Fish Traps) or fires (Grilled), so the screen stays readable.
    private void Land(Vector2 at, float radius, float damage, bool alt)
    {
        Sfx.Play(Sfx.Id.Pop, 0.3f, 1.3f);
        Juice.SparksDir(at, Vector2.up, 70f, Water, 8, 4f + radius * 2f, 0.12f);
        ExplosionFx.Ring(at, radius, Water, 0.25f);
        int hits = 0;
        foreach (Collider2D c in EnemiesNear(at, radius).ToArray()) // copy: kills can refill the shared scratch list
        {
            if (!c.TryGetComponent(out EnemyHealth e) || e.IsDead) continue;
            Hit(e, damage, at);
            hits++;
        }

        if (alt && Grilled) BurningGround.Spawn(at, radius * 0.6f, 2.5f, Dmg(8f));
        if (alt && Traps) MineWeapon.SpawnMine(at, damage * 1.5f, radius * 1.3f, (pos, r, d) => Explode(pos, r, d));
        if (Feast)
        {
            if (hits > 0) stats.Heal(Mathf.Min(FeastHealMax, hits * FeastHealPerCat));
            if (Random.value < FeastSnackChance) Burger.Spawn(at);
        }
    }
}
