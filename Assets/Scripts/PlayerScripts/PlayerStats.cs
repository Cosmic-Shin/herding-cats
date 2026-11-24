using UnityEngine;
using System;

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
    public float lowHealthDamageBonus = 0f;
    public float AttackDamage =>
        baseAttackDamage * attackDamageMultiplier * (1f + lowHealthDamageBonus * (1f - currentHealth / maxHealth));

    public float baseAttackSpeed = 1f;
    public float attackSpeedMultiplier = 1f;
    public float AttackSpeed => baseAttackSpeed * attackSpeedMultiplier;

    public int projectileCount = 1;
    public int ProjectileCount => projectileCount;

    [Range(0f, 1f)] public float critChance = 0.05f;
    public float critDamage = 1.5f;

    public float baseBombRadius = 1.2f;
    public float areaMultiplier = 1f;
    public float BombRadius => baseBombRadius * areaMultiplier;

    public float pickupRange = 2f;

    [Header("Defense / Utility (mostly set by skills)")]
    [Range(0f, 0.9f)] public float damageReduction = 0f;
    [Range(0f, 0.9f)] public float dodgeChance = 0f;
    public float expMultiplier = 1f;
    public float burgerHealMultiplier = 1f;
    private float expCarry;

    [Header("Level")]
    public int level = 1;
    public int currentExp = 0;
    public int expToNextLevel = 10;
    public float levelExpGrowth = 1.16f;
    [Tooltip("Growth per level from lateGrowthLevel on (at 1.16 all the way, level 40 needed 3,500 XP and level-ups crawled)")]
    public float lateExpGrowth = 1.08f;
    public int lateGrowthLevel = 20;

    public event Action<float, float> OnHealthChanged;
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

    private System.Collections.IEnumerator Start()
    {
        Hero.Apply(this);
        // Shop upgrades land when the run really starts (Start button, Restart or a map-change reload), never on the menu.
        yield return new WaitUntil(() => MainMenu.Started && !MainMenu.IsOpen);
        yield return null; // let LevelUpUIController finish its Start so head-start levels open its picker
        Shop.ApplyToRun(this);
    }

    public void GrantLevels(int count)
    {
        for (int i = 0; i < count; i++) LevelUp();
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ModifyHealth(float delta)
    {
        currentHealth = Mathf.Clamp(currentHealth + delta, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
            OnDeath?.Invoke();
    }

    // Black cat curse: less crit and dodge for a while. Re-cursing refreshes the timer instead of stacking.
    public const float CurseCrit = 0.15f, CurseDodge = 0.15f;
    public bool Cursed => Time.time < curseUntil;
    private float curseUntil;

    public void Curse(float seconds)
    {
        bool fresh = !Cursed;
        curseUntil = Mathf.Max(curseUntil, Time.time + seconds);
        if (fresh) StartCoroutine(CurseRoutine());
    }

    private System.Collections.IEnumerator CurseRoutine()
    {
        critChance -= CurseCrit;
        dodgeChance -= CurseDodge;
        DamagePopup.SpawnText((Vector2)transform.position + Vector2.up, "CURSED!", new Color(0.7f, 0.4f, 1f), 4f);
        while (Cursed)
        {
            if (UnityEngine.Random.value < 0.3f) Juice.Sparks((Vector2)transform.position + UnityEngine.Random.insideUnitCircle * 0.4f, new Color(0.45f, 0.2f, 0.7f, 0.8f), 1, 0.8f, 0.1f);
            yield return null;
        }
        critChance += CurseCrit;
        dodgeChance += CurseDodge;
    }

    public void AddTimedSpeed(float amount, float seconds) => StartCoroutine(TimedSpeed(amount, seconds));

    private System.Collections.IEnumerator TimedSpeed(float amount, float seconds)
    {
        moveSpeedMultiplier += amount;
        yield return new WaitForSeconds(seconds);
        moveSpeedMultiplier -= amount;
    }

    public const float MaxHealthCap = 500f;

    public void IncreaseMaxHealth(float amount)
    {
        amount = Mathf.Min(amount, MaxHealthCap - maxHealth);
        if (amount <= 0f) return;
        maxHealth += amount;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void IncreaseProjectileCount(int amount)
    {
        projectileCount = Mathf.Min(projectileCount + amount, PlayerCollabs.MaxBombsPerThrow);
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
        expToNextLevel = Mathf.RoundToInt(expToNextLevel * (level < lateGrowthLevel ? levelExpGrowth : lateExpGrowth));
        OnLevelUp?.Invoke(level);
    }
}
