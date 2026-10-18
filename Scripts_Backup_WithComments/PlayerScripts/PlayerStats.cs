using UnityEngine;
using System;

// Holds all the player's raw numbers (health, speed, damage, etc.)
// Other scripts read from here instead of hardcoding values.
// Upgrades/items should modify the multiplier fields, not the base fields.
public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Movement")]
    public float baseMoveSpeed = 5f;
    public float moveSpeedMultiplier = 1f;
    public float MoveSpeed => baseMoveSpeed * moveSpeedMultiplier;

    [Header("Combat")]
    public float baseAttackDamage = 10f;
    public float attackDamageMultiplier = 1f;
    public float lowHealthDamageBonus = 0f; // Berserk: bonus at 0 HP, scales with missing HP
    public float AttackDamage =>
        baseAttackDamage * attackDamageMultiplier * (1f + lowHealthDamageBonus * (1f - currentHealth / maxHealth));

    public float baseAttackSpeed = 1f; // attacks per second
    public float attackSpeedMultiplier = 1f;
    public float AttackSpeed => baseAttackSpeed * attackSpeedMultiplier;

    public int projectileCount = 1;
    public int ProjectileCount => projectileCount;

    [Range(0f, 1f)] public float critChance = 0.05f;
    public float critDamage = 1.5f; // crit damage multiplier

    public float baseBombRadius = 1.2f;
    public float areaMultiplier = 1f;
    public float BombRadius => baseBombRadius * areaMultiplier;

    public float pickupRange = 2f;

    [Header("Defense / Utility (mostly set by skills)")]
    [Range(0f, 0.9f)] public float damageReduction = 0f;
    [Range(0f, 0.9f)] public float dodgeChance = 0f;
    public float expMultiplier = 1f;
    public float burgerHealMultiplier = 1f;
    private float expCarry; // fractional XP from expMultiplier

    [Header("Level")]
    public int level = 1;
    public int currentExp = 0;
    public int expToNextLevel = 10;
    public float levelExpGrowth = 1.16f; // each level needs this much more XP than the last (compounds: keep it low)

    // Other scripts subscribe to these instead of polling every frame
    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action OnDeath;
    public event Action<int> OnLevelUp;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (GetComponent<XPCollectorManager>() == null)
        {
            gameObject.AddComponent<XPCollectorManager>();
        }

        if (GetComponent<LevelUpUIController>() == null)
        {
            gameObject.AddComponent<LevelUpUIController>();
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // Positive delta heals, negative delta damages. PlayerHealth calls this.
    public void ModifyHealth(float delta)
    {
        currentHealth = Mathf.Clamp(currentHealth + delta, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
            OnDeath?.Invoke();
    }

    // Speed pickup: temporary move speed that wears off by itself.
    public void AddTimedSpeed(float amount, float seconds) => StartCoroutine(TimedSpeed(amount, seconds));

    private System.Collections.IEnumerator TimedSpeed(float amount, float seconds)
    {
        moveSpeedMultiplier += amount;
        yield return new WaitForSeconds(seconds);
        moveSpeedMultiplier -= amount;
    }

    public void IncreaseMaxHealth(float amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void IncreaseProjectileCount(int amount)
    {
        projectileCount += amount;
    }

    public void AddExp(int amount)
    {
        float total = amount * expMultiplier + expCarry;
        int whole = Mathf.FloorToInt(total);
        expCarry = total - whole;
        currentExp += whole;

        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
        }
    }

    private void LevelUp()
    {
        level++;
        expToNextLevel = Mathf.RoundToInt(expToNextLevel * levelExpGrowth);
        OnLevelUp?.Invoke(level);
    }
}