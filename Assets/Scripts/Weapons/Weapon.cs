using System.Collections.Generic;
using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    public const int MaxLevel = 7;

    public int Level { get; private set; }
    public string DisplayName = "Weapon";
    public float LastFired { get; private set; } = -10f;

    public const int MaxEnhance = 5;
    public int Enhance { get; private set; }
    private float bonusDamage, bonusArea, bonusSpeed;

    public enum Boost { Damage, Area, Speed }

    public void AddEnhance(Boost boost)
    {
        Enhance++;
        if (boost == Boost.Damage) bonusDamage += 0.15f;
        else if (boost == Boost.Area) bonusArea += 0.06f; // +30% at +5; 15% each let a maxed aura cover the screen
        else bonusSpeed += 0.12f;
        OnEnhanced(boost);
    }

    protected virtual void OnEnhanced(Boost boost) { }

    public void CopyEnhance(Weapon from)
    {
        Enhance = from.Enhance;
        (bonusDamage, bonusArea, bonusSpeed) = (from.bonusDamage, from.bonusArea, from.bonusSpeed);
    }

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

    protected int L => Level - 1;

    protected Vector2 Pos => transform.position;
    protected Vector2 Facing => controller.GetFacingDirection().normalized;

    protected float Dmg(float baseDamage) =>
        baseDamage * (stats.AttackDamage / stats.baseAttackDamage) * (ult != null ? ult.DamageMultiplier : 1f) * (1f + bonusDamage);

    protected float Area(float radius) => radius * stats.areaMultiplier * (1f + bonusArea);

    protected bool Ready(float baseCooldown)
    {
        DamageStats.Source = DisplayName;
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

    protected void Explode(Vector2 pos, float radius, float damage)
    {
        DamageStats.Source = DisplayName;
        Bomb.Blast(pos, radius, damage, stats.critChance, stats.critDamage, false);
    }

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

    protected Vector2 AimDirection(float range)
    {
        Transform t = NearestEnemy(range);
        return t != null ? ((Vector2)t.position - Pos).normalized : Facing;
    }

    protected static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;
}
