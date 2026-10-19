using System.Collections;
using UnityEngine;

public interface IEnemyBrain
{
    void Knockback(Vector2 impulse);
    void Die();
    void Slow(float amount, float seconds);
    bool IsImpaired { get; }
}

public class EnemyHealth : MonoBehaviour, IPoolable
{
    public float maxHealth = 30f;
    private float baseMaxHealth;
    private float currentHealth;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public bool IsImpaired => ai != null && ai.IsImpaired;
    public Color BaseColor => baseColor;
    public int Life { get; private set; }

    public static int Kills { get; private set; }
    public static int BossKills { get; private set; }
    public static int EliteKills { get; private set; }
    public static int GoldenKills { get; private set; }
    public static int KillScore { get; private set; }
    public static void ResetRunCounters() { Kills = BossKills = EliteKills = GoldenKills = KillScore = PlayerHealth.Hits = RewardMenu.ChestsOpened = 0; DamageStats.Reset(); }
    private static float nextBossHitStop;

    private const int MaxGemsOnField = 250;

    [Header("Drops")]
    public GameObject xpGemPrefab;
    public int xpValue = 1;
    public int xpGemCount = 1;
    [Range(0f, 1f)] public float burgerDropChance = 0.02f;
    public bool dropsHoloBox;
    [System.NonSerialized] public bool golden;
    public bool dropsAnvil;

    [Header("Splitter")]
    public GameObject splitInto;
    public int splitCount = 2;
    private float nextBlockPopup;

    [Header("Feedback")]
    public float knockbackForce = 4f;
    public Color hitFlashColor = new Color(1f, 0.35f, 0.35f);

    private SpriteRenderer sr;
    private Color baseColor;
    private IEnemyBrain ai;
    private EnemyAnimator anim;

    private void Awake()
    {
        baseMaxHealth = maxHealth;
        currentHealth = maxHealth;
        baseXpValue = xpValue;
        BaseGemCount = xpGemCount;
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        ai = GetComponent<IEnemyBrain>();
        anim = GetComponent<EnemyAnimator>();
    }

    private float burningUntil;
    public void MarkBurning() => burningUntil = Time.time + 0.6f;

    public bool IsElite { get; private set; }
    public int BaseGemCount { get; private set; }
    private int baseXpValue;

    public void MakeElite(float healthMultiplier, int xpMultiplier)
    {
        IsElite = true;
        ScaleHealth(healthMultiplier);
        xpValue = baseXpValue * xpMultiplier;
        xpGemCount = 6;
    }

    public void OnSpawn()
    {
        Life++;
        IsElite = false;
        xpValue = baseXpValue;
        xpGemCount = BaseGemCount;
        burningUntil = 0f;
        maxHealth = baseMaxHealth;
        currentHealth = maxHealth;
        isDead = false;
        StopAllCoroutines();
        if (sr != null) sr.color = baseColor;
    }

    public void Tint(Color color)
    {
        baseColor = color;
        if (sr != null) sr.color = color;
    }

    public float Heal(float amount)
    {
        if (isDead) return 0f;
        float healed = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += healed;
        return healed;
    }

    public void ScaleHealth(float multiplier)
    {
        maxHealth *= multiplier;
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector2 sourcePos, bool crit = false, bool knockback = true)
    {
        if (isDead) return;
        if (ai is EnemyAI frozen && frozen.IsFrozen) amount *= 1.5f;
        if (ai is EnemyAI armored && armored.IsArmored) amount *= 0.2f;
        if (ai is EnemyAI shielded && shielded.Blocks(sourcePos))
        {
            amount *= 0.2f;
            if (Time.time >= nextBlockPopup)
            {
                nextBlockPopup = Time.time + 0.5f;
                DamagePopup.SpawnText(transform.position + Vector3.up * 0.3f, "BLOCK", new Color(0.75f, 0.85f, 1f), 3f);
            }
        }

        DamageStats.Add(Mathf.Min(amount, currentHealth));
        Sfx.Play(Sfx.Id.Hit, crit ? 0.5f : 0.3f, crit ? 1.3f : 1f);
        currentHealth -= amount;
        DamagePopup.Spawn(transform.position, amount, crit);
        if (knockback)
            Juice.SparksDir(Vector2.MoveTowards(transform.position, sourcePos, 0.25f), (Vector2)transform.position - sourcePos, 35f,
                            crit ? new Color(1f, 0.9f, 0.3f) : Color.white, crit ? 7 : 4, crit ? 7f : 5f, 0.11f);
        if (crit && ai is BossAI && Time.unscaledTime >= nextBossHitStop)
        {
            Juice.HitStop(0.04f);
            nextBossHitStop = Time.unscaledTime + 0.4f;
        }

        if (ai is EnemyAI cat) { cat.WakeUp(); cat.HitSquash(); }
        if (ai != null && knockback) ai.Knockback(((Vector2)transform.position - sourcePos).normalized * knockbackForce);
        if (sr != null)
        {
            StopAllCoroutines();
            StartCoroutine(Flash());
        }

        if (currentHealth <= 0) Die();
    }

    private void SpawnGemShower()
    {
        int core = Mathf.Max(1, Mathf.RoundToInt(xpValue * 0.3f));
        SpawnGem(core, Vector2.zero);

        int rest = xpValue - core;
        int count = Mathf.Min(xpGemCount - 1, rest);
        if (count <= 0) return;

        float[] weights = new float[count];
        float total = 0f;
        for (int i = 0; i < count; i++) total += weights[i] = Random.Range(0.3f, 2f);

        int[] values = new int[count];
        int assigned = 0;
        for (int i = 0; i < count; i++) assigned += values[i] = Mathf.Max(1, Mathf.FloorToInt(rest * weights[i] / total));
        for (int i = 0; assigned < rest; i = (i + 1) % count, assigned++) values[i]++;

        foreach (int value in values)
        {
            SpawnGem(value, Random.insideUnitCircle.normalized * Random.Range(6f, 14f));
        }
    }

    private void SpawnGem(int value, Vector2 launch)
    {
        XPCollectorManager manager = XPCollectorManager.Instance;
        if (manager != null && manager.Count >= MaxGemsOnField)
        {
            XPGem nearest = manager.NearestIdle(transform.position);
            if (nearest != null)
            {
                nearest.SetValue(nearest.expValue + value);
                return;
            }
        }

        GameObject gem = Pool.Spawn(xpGemPrefab, MapGenerator.PlaceDrop(transform.position), Quaternion.identity);
        if (!gem.TryGetComponent(out XPGem xp)) return;
        xp.SetValue(value);
        if (launch != Vector2.zero) xp.Launch(launch, 0.5f);
        else xp.Launch(Random.insideUnitCircle.normalized * Random.Range(1.5f, 2.5f), 0.2f);
    }

    private void Split()
    {
        float growth = maxHealth / Mathf.Max(1f, baseMaxHealth);
        for (int i = 0; i < splitCount; i++)
        {
            GameObject kid = Pool.Spawn(splitInto, (Vector2)transform.position + Random.insideUnitCircle * 0.4f, Quaternion.identity);
            if (kid.TryGetComponent(out EnemyHealth h)) h.ScaleHealth(growth);
        }
    }

    private const float EliteChestChance = 0.04f;
    public const int MaxRandomChests = 3;

    private void EliteDeath()
    {
        EliteKills++;
        if (Pickup.HoloBoxCount < MaxRandomChests && Random.value < EliteChestChance) Pickup.Spawn(Pickup.Kind.HoloBox, (Vector2)transform.position + Vector2.left * 0.6f);
        if (!(ai is EnemyAI cat)) return;
        if (cat.Mod == EnemyAI.EliteMod.Volatile)
            DelayedBlast.Spawn(transform.position, 2.2f, 0.8f, cat.contactDamage * 2f);
        else if (cat.Mod == EnemyAI.EliteMod.Splitter && TryGetComponent(out Pooled pooled))
        {
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
            float growth = spawner != null ? spawner.HealthMultiplier : 1f;
            for (int i = 0; i < 3; i++)
            {
                GameObject kid = Pool.Spawn(pooled.Prefab, (Vector2)transform.position + Random.insideUnitCircle * 0.5f, Quaternion.identity);
                if (kid.TryGetComponent(out EnemyHealth h)) h.ScaleHealth(growth);
            }
        }
    }

    private IEnumerator Flash()
    {
        if (anim != null) { anim.Flash(0.08f); yield break; }
        sr.color = hitFlashColor;
        yield return new WaitForSeconds(0.08f);
        sr.color = baseColor;
    }

    private void Die()
    {
        isDead = true;
        if (TryGetComponent(out Crate crate)) { crate.Break(); return; }
        KillScore += xpValue * 10;

        if (ai is BossAI)
        {
            BossKills++;
            Sfx.Play(Sfx.Id.Boom, 1f, 0.6f);
            Juice.HitStop(0.2f);
            Juice.Flash(new Color(1f, 0.95f, 0.8f, 0.6f), 0.4f);
        }
        else
        {
            Kills++;
            Sfx.Play(Sfx.Id.Pop, 0.45f);
            if (IsElite) EliteDeath();
            if (IsElite)
            {
                ExplosionFx.Spawn(transform.position, 1.6f);
                ExplosionFx.Ring(transform.position, 2.5f, new Color(1f, 0.5f, 0.4f), 0.35f);
                Juice.Sparks(transform.position, new Color(1f, 0.5f, 0.4f), 10, 8f, 0.15f);
                CameraFollow.Shake(0.1f, 0.12f);
            }
            else if (xpGemCount > 1)
            {
                Juice.HitStop(0.08f);
                Juice.SlowMo(0.3f, 0.6f);
                Juice.Flash(new Color(1f, 0.95f, 0.85f, 0.45f), 0.35f);
                ExplosionFx.Spawn(transform.position, 2.5f);
                ExplosionFx.Ring(transform.position, 4f, new Color(1f, 0.9f, 0.6f), 0.5f);
                Juice.Sparks(transform.position, new Color(1f, 0.85f, 0.3f), 16, 10f, 0.2f);
                CameraFollow.Shake(0.25f, 0.3f);
            }
            Juice.CatPuff(transform.position, new Color(1f, 0.93f, 0.82f));
            if (anim != null) anim.Flash(0.12f);
            Juice.Sparks(transform.position, new Color(1f, 0.85f, 0.55f), 5, 5f, 0.12f);
            if (!Settings.LowParticles) ExplosionFx.Ring(transform.position, 0.9f, new Color(1f, 0.97f, 0.9f, 0.8f), 0.2f);
            Juice.CatTalk(transform.position, Juice.Talk.Death, 0.12f);
            if (Time.time < burningUntil) PlayerCollabs.BurningDeath(transform.position);
            if (xpGemCount <= 1) Pickup.TryCatDrop(transform.position);
            if (splitInto != null) Split();
        }

        if (golden) GoldenKills++;
        if (dropsHoloBox) Pickup.Spawn(Pickup.Kind.HoloBox, (Vector2)transform.position + Vector2.left * 0.6f);
        if (dropsAnvil) Pickup.Spawn(Pickup.Kind.Anvil, (Vector2)transform.position + Vector2.right * 0.6f);

        if (xpGemPrefab != null)
        {
            if (xpGemCount <= 1) SpawnGem(xpValue, Vector2.zero);
            else SpawnGemShower();
        }

        if (Random.value < burgerDropChance * Burger.DropChanceMultiplier)
        {
            Burger.Spawn((Vector2)transform.position + Random.insideUnitCircle * 0.3f);
        }

        StopAllCoroutines();
        if (sr != null) sr.color = baseColor;

        if (ai != null) ai.Die();
        else Pool.Despawn(gameObject);
    }
}
