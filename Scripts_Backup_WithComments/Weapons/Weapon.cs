using System.Collections.Generic;
using UnityEngine;

// Base for every extra weapon (bombs stay the main weapon in PlayerAutoAttack).
// Added to the player by PlayerWeapons; levels 1..7. Shared scaling rules:
//   damage   x (current attack / base attack)  -> Attack Up, Berserk, etc. apply
//            x ultimate damage multiplier while it's active
//   area     x areaMultiplier (Big Boom)
//   cooldown / attackSpeedMultiplier (Haste)
//   crits use the player's crit chance / crit damage
public abstract class Weapon : MonoBehaviour
{
    public const int MaxLevel = 7;

    public int Level { get; private set; }
    public string DisplayName = "Weapon"; // set by PlayerWeapons; also the damage stats label
    public float LastFired { get; private set; } = -10f; // Time.time of the last Ready() trigger (HUD slot pulse)

    // Anvil enhancements on a maxed weapon (see RewardMenu): each one is +15% damage, +15% area or +12% speed.
    public const int MaxEnhance = 5;
    public int Enhance { get; private set; }
    private float bonusDamage, bonusArea, bonusSpeed;

    public enum Boost { Damage, Area, Speed }

    public void AddEnhance(Boost boost)
    {
        Enhance++;
        if (boost == Boost.Damage) bonusDamage += 0.15f;
        else if (boost == Boost.Area) bonusArea += 0.15f;
        else bonusSpeed += 0.12f;
        OnEnhanced(boost);
    }

    protected virtual void OnEnhanced(Boost boost) { } // super weapons pass anvil boosts on to their parts

    protected PlayerStats stats;
    protected PlayerController controller;
    protected PlayerUltimate ult;

    private float cooldownTimer;
    private static readonly List<Collider2D> scratch = new List<Collider2D>(64);

    protected virtual void Awake()
    {
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        ult = GetComponent<PlayerUltimate>();
    }

    public void SetLevel(int level)
    {
        Level = Mathf.Clamp(level, 1, MaxLevel);
        OnLevelChanged();
    }

    protected virtual void OnLevelChanged() { }

    // index into per-level tables
    protected int L => Level - 1;

    protected Vector2 Pos => transform.position;
    protected Vector2 Facing => controller.GetFacingDirection().normalized;

    protected float Dmg(float baseDamage) =>
        baseDamage * (stats.AttackDamage / stats.baseAttackDamage) * (ult != null ? ult.DamageMultiplier : 1f) * (1f + bonusDamage);

    protected float Area(float radius) => radius * stats.areaMultiplier * (1f + bonusArea);

    // Counts down; returns true (and restarts) when the weapon should fire.
    protected bool Ready(float baseCooldown)
    {
        DamageStats.Source = DisplayName; // whatever this weapon spawns from here on is credited to it
        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer > 0f) return false;
        cooldownTimer = baseCooldown / Mathf.Max(0.1f, stats.attackSpeedMultiplier) / (1f + bonusSpeed);
        LastFired = Time.time;
        return true;
    }

    protected void Hit(EnemyHealth enemy, float damage, Vector2 from, bool knockback = true)
    {
        DamageStats.Source = DisplayName;
        bool crit = Random.value < stats.critChance || MiraPassives.AutoCrit(enemy);
        enemy.TakeDamage(damage * (crit ? stats.critDamage * MiraPassives.CritBonus(enemy) : 1f) * Random.Range(0.9f, 1.1f), from, crit, knockback);
    }

    // Explosions from weapons go through the bomb blast so explosion skills (chain/cluster/napalm) apply.
    protected void Explode(Vector2 pos, float radius, float damage)
    {
        DamageStats.Source = DisplayName;
        Bomb.Blast(pos, radius, damage, stats.critChance, stats.critDamage, false);
    }

    // Shared buffer: valid until the next call. Includes sleeping cats: hitting one wakes it.
    protected static List<Collider2D> EnemiesNear(Vector2 pos, float radius)
    {
        Bomb.OverlapEnemies(pos, radius, scratch);
        return scratch;
    }

    protected Transform NearestEnemy(float range) => NearestTo(Pos, range);

    protected static Transform NearestTo(Vector2 from, float range)
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (Collider2D c in EnemiesNear(from, range))
        {
            float d = ((Vector2)c.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c.transform; }
        }
        return best;
    }

    protected Transform RandomEnemy(float range)
    {
        List<Collider2D> list = EnemiesNear(Pos, range);
        return list.Count > 0 ? list[Random.Range(0, list.Count)].transform : null;
    }

    // Direction to the nearest enemy, or the facing direction if nothing's around.
    protected Vector2 AimDirection(float range)
    {
        Transform t = NearestEnemy(range);
        return t != null ? ((Vector2)t.position - Pos).normalized : Facing;
    }

    protected static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;
}
