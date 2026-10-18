using System.Collections;
using UnityEngine;

// What EnemyHealth needs from an enemy's brain (EnemyAI for cats, BossAI for bosses).
public interface IEnemyBrain
{
    void Knockback(Vector2 impulse);
    void Die(); // play death, then destroy
    void Slow(float amount, float seconds); // amount 0.4 = 40% slower; strongest active slow wins
    bool IsImpaired { get; } // slowed or stunned right now (Mira's Weak Spot)
}

// Enemy health with HoloCure-style feedback: damage numbers, hit flash, knockback.
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
    public int Life { get; private set; } // bumps every (re)spawn, so bombs can tell a reused enemy apart

    // run stats for the game over screen (reset by GameOverScreen on scene load)
    public static int Kills { get; private set; }
    public static int BossKills { get; private set; }
    public static int EliteKills { get; private set; }
    public static int GoldenKills { get; private set; }
    public static int KillScore { get; private set; } // 10 x the XP value of everything defeated (Leaderboard.Score)
    public static void ResetRunCounters() { Kills = BossKills = EliteKills = GoldenKills = KillScore = PlayerHealth.Hits = RewardMenu.ChestsOpened = 0; DamageStats.Reset(); }
    private static float nextBossHitStop;

    private const int MaxGemsOnField = 250; // past this, new XP merges into an existing gem

    [Header("Drops")]
    public GameObject xpGemPrefab;
    public int xpValue = 1;
    public int xpGemCount = 1; // > 1 = burst into that many gems (bosses)
    [Range(0f, 1f)] public float burgerDropChance = 0.02f;
    public bool dropsHoloBox;   // minibosses and Beholders
    [System.NonSerialized] public bool golden; // the golden cat event (one-off, never pooled)
    public bool dropsAnvil;     // Beholders

    [Header("Splitter")]
    public GameObject splitInto; // spawned on death (pooled), e.g. kittens
    public int splitCount = 2;
    private float nextBlockPopup;

    [Header("Feedback")]
    public float knockbackForce = 4f;
    public Color hitFlashColor = new Color(1f, 0.35f, 0.35f);

    private SpriteRenderer sr;
    private Color baseColor;
    private IEnemyBrain ai;
    private EnemyAnimator anim; // cats: white silhouette flash; others (bosses): color tint

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

    // Fire puddles mark what they burn; Firebomb makes cats that die burning explode.
    private float burningUntil;
    public void MarkBurning() => burningUntil = Time.time + 0.6f;

    // Elites (EnemySpawner): tougher, more XP, a gem shower. Undone on the next spawn from the pool.
    public bool IsElite { get; private set; }
    public int BaseGemCount { get; private set; } // the prefab's gem count (minibosses > 1)
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
        xpValue = baseXpValue; // undo elite XP and anything a Gem Thief stole last life
        xpGemCount = BaseGemCount;
        burningUntil = 0f;
        maxHealth = baseMaxHealth; // undo last life's ScaleHealth
        currentHealth = maxHealth;
        isDead = false;
        StopAllCoroutines();
        if (sr != null) sr.color = baseColor;
    }

    // Recolor for good (special one-off spawns like the golden cat; pooled cats would keep it).
    public void Tint(Color color)
    {
        baseColor = color;
        if (sr != null) sr.color = color;
    }

    // Healer Cat. Returns how much it actually healed.
    public float Heal(float amount)
    {
        if (isDead) return 0f;
        float healed = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += healed;
        return healed;
    }

    // Spawner calls this right after Instantiate to toughen enemies as the run goes on.
    public void ScaleHealth(float multiplier)
    {
        maxHealth *= multiplier;
        currentHealth = maxHealth;
    }

    // knockback: false for damage-over-time (napalm) so enemies aren't shoved out of the fire every tick
    public void TakeDamage(float amount, Vector2 sourcePos, bool crit = false, bool knockback = true)
    {
        if (isDead) return;
        if (ai is EnemyAI frozen && frozen.IsFrozen) amount *= 1.5f;      // Glacier Quake shatter bonus
        if (ai is EnemyAI armored && armored.IsArmored) amount *= 0.2f;   // Fat Cat's bouncy belly
        if (ai is EnemyAI shielded && shielded.Blocks(sourcePos))         // Shield cat, hit from the front
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
        if (knockback) // sparks spray out the far side, away from whatever hit it
            Juice.SparksDir(Vector2.MoveTowards(transform.position, sourcePos, 0.25f), (Vector2)transform.position - sourcePos, 35f,
                            crit ? new Color(1f, 0.9f, 0.3f) : Color.white, crit ? 7 : 4, crit ? 7f : 5f, 0.11f);
        if (crit && ai is BossAI && Time.unscaledTime >= nextBossHitStop) // crits on bosses land heavy
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

    // Boss drop: one big "core" gem (30% of the XP) plus a spray of random-sized
    // gems, so the shower mixes several gem tiers like HoloCure.
    private void SpawnGemShower()
    {
        int core = Mathf.Max(1, Mathf.RoundToInt(xpValue * 0.3f));
        SpawnGem(core, Vector2.zero);

        int rest = xpValue - core;
        int count = Mathf.Min(xpGemCount - 1, rest);
        if (count <= 0) return;

        // split `rest` by random weights; floor each share, then hand out the leftover 1 by 1
        float[] weights = new float[count];
        float total = 0f;
        for (int i = 0; i < count; i++) total += weights[i] = Random.Range(0.3f, 2f);

        int[] values = new int[count];
        int assigned = 0;
        for (int i = 0; i < count; i++) assigned += values[i] = Mathf.Max(1, Mathf.FloorToInt(rest * weights[i] / total));
        for (int i = 0; assigned < rest; i = (i + 1) % count, assigned++) values[i]++;

        foreach (int value in values)
        {
            SpawnGem(value, Random.insideUnitCircle.normalized * Random.Range(6f, 14f)); // skids ~1-2.2 units
        }
    }

    private void SpawnGem(int value, Vector2 launch)
    {
        // too many gems lying around: fold the XP into the nearest idle gem (HoloCure does this too)
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

        GameObject gem = Pool.Spawn(xpGemPrefab, MapGenerator.PlaceDrop(transform.position), Quaternion.identity); // reachable: not in a pond or past the wall
        if (!gem.TryGetComponent(out XPGem xp)) return;
        xp.SetValue(value);
        if (launch != Vector2.zero) xp.Launch(launch, 0.5f);
        else xp.Launch(Random.insideUnitCircle.normalized * Random.Range(1.5f, 2.5f), 0.2f); // pops out of the cat and skids
    }

    // Splitter: pops into smaller cats that carry the same late-run toughness.
    private void Split()
    {
        float growth = maxHealth / Mathf.Max(1f, baseMaxHealth);
        for (int i = 0; i < splitCount; i++)
        {
            GameObject kid = Pool.Spawn(splitInto, (Vector2)transform.position + Random.insideUnitCircle * 0.4f, Quaternion.identity);
            if (kid.TryGetComponent(out EnemyHealth h)) h.ScaleHealth(growth);
        }
    }

    private const float EliteChestChance = 0.12f;

    private void EliteDeath()
    {
        EliteKills++;
        if (Random.value < EliteChestChance) Pickup.Spawn(Pickup.Kind.HoloBox, (Vector2)transform.position + Vector2.left * 0.6f);
        if (!(ai is EnemyAI cat)) return;
        if (cat.Mod == EnemyAI.EliteMod.Volatile)
            DelayedBlast.Spawn(transform.position, 2.2f, 0.8f, cat.contactDamage * 2f);
        else if (cat.Mod == EnemyAI.EliteMod.Splitter && TryGetComponent(out Pooled pooled))
        {
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>(); // rare: fine to look it up
            float growth = spawner != null ? spawner.HealthMultiplier : 1f; // the three come out as regular cats
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
        isDead = true; // guards double XP drops when several bombs land the same frame
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
            if (IsElite) // common late game, so a punchy pop without the slow-mo beat
            {
                ExplosionFx.Spawn(transform.position, 1.6f);
                ExplosionFx.Ring(transform.position, 2.5f, new Color(1f, 0.5f, 0.4f), 0.35f);
                Juice.Sparks(transform.position, new Color(1f, 0.5f, 0.4f), 10, 8f, 0.15f);
                CameraFollow.Shake(0.1f, 0.12f);
            }
            else if (xpGemCount > 1) // miniboss / golden cat: slow-mo beat, big flash, shockwave
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
            // defeat pop: the cat flashes white, a little shock ring and a spray of sparks
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
            Burger.Spawn((Vector2)transform.position + Random.insideUnitCircle * 0.3f); // offset so it doesn't hide under the gem
        }

        StopAllCoroutines();
        if (sr != null) sr.color = baseColor;

        if (ai != null) ai.Die(); // plays the death animation, then despawns
        else Pool.Despawn(gameObject);
    }
}
