using UnityEngine;
using System.Collections.Generic;

// Spawns enemies in a ring just outside the camera and ramps difficulty over the run:
// spawns get faster (down to a floor), enemies get tougher, and the alive count is capped
// so late game stays hectic without melting the framerate.
public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyWave
    {
        public string name = "Basic Enemy";
        public GameObject enemyPrefab;
        public float spawnInterval = 2f;   // seconds between spawns of this type
        public float timeToUnlock = 0f;    // seconds into the run before this enemy appears
        public bool special;               // healers, mamas, thieves...: normal spawns only, never a whole swarm of them
    }

    [Header("References")]
    public Transform player; // auto-found by "Player" tag if left empty

    [Header("Enemy Waves")]
    public List<EnemyWave> enemyWaves = new List<EnemyWave>();

    [Header("Spawn Ring (should be just outside camera view)")]
    public float minSpawnRadius = 11f;
    public float maxSpawnRadius = 13f;

    [Header("Overall difficulty: x enemy HP and x enemy damage (1 = original tuning)")]
    [Range(0.5f, 1.5f)] public float difficulty = 0.85f;

    [Header("Difficulty Scaling")]
    public float difficultyRampInterval = 30f;    // every X seconds, spawn rate increases
    public float difficultyMultiplierStep = 0.88f; // <1 = faster spawns each ramp
    public float minSpawnInterval = 0.15f;         // per-wave floor so the ramp can't run away
    public float maxSpeedup = 4f;                  // a wave never spawns more than this many times its base rate
    public int maxEnemies = 250;
    public float healthGrowthPerMinute = 0.4f;     // +40% enemy HP per minute survived

    [Header("Late-game pressure (on top of the above)")]
    public float healthGrowthQuadratic = 0.05f;    // + this x minutes^2 to the HP multiplier: ~10x at 10 min, ~29x at 20
    public float damageGrowthPerMinute = 0.18f;    // enemy damage to the player: +18% per minute
    public float lateSpawnStart = 480f;            // after this, spawns keep speeding up past the normal floor
    public float lateSpawnRampSeconds = 300f;      // +1x spawn rate per this many seconds past lateSpawnStart
    public float lateSpawnMaxBoost = 3f;
    public int lateMaxEnemiesBonus = 100;          // extra alive cap, reached at full late boost

    [Header("Elites: random tougher cats with a red glow that drop a gem shower")]
    public float eliteStartTime = 120f;
    public float eliteChancePerMinute = 0.012f;    // chance per spawn grows by this each minute after the start
    public float eliteChanceMax = 0.1f;
    public float eliteHealth = 8f;                 // x the normal HP at that point
    public int eliteXpMultiplier = 6;

    // Enemy damage scale for the current run (PlayerHealth applies it to every hit Mira takes).
    public static float DamageScale { get; private set; } = 1f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => DamageScale = 1f;

    [Header("Endless (HoloCure 30-min style): when the final boss (BossTimeline, 20:00) dies, every enemy vanishes and only minibosses come")]
    // Minibosses come in rings that surround Mira, HoloCure style: bigger rings, more often, every minute.
    public float endlessWaveInterval = 2.2f;    // seconds between rings at the start of endless...
    public float endlessMinWaveInterval = 0.9f; // ...shrinking to this
    public int endlessRingStart = 14;           // minibosses per ring at the start...
    public int endlessRingPerMinute = 4;        // ...+ this per endless minute...
    public int endlessRingMax = 36;             // ...up to this
    public int endlessMaxAlive = 160;           // ponytail: perf cap; minibosses are heavier than cats, lower it if endless stutters
    public float endlessHealthStart = 1f;       // extra HP on endless minibosses, on top of the run's HP growth...
    public float endlessHealthPerMinute = 0.4f; // ...+ this per endless minute
    public static bool Endless { get; private set; }
    private float endlessStart, endlessTimer;

    public float RunTime => runTime;
    public float EndlessMinutes => Endless ? (runTime - endlessStart) / 60f : 0f;

    private void Awake() => Endless = false; // statics survive Restart's scene reload

    public void StartEndless()
    {
        if (Endless) return;
        Endless = true;
        endlessStart = runTime;
        endlessTimer = 2.5f; // let the shockwave finish before the big cats walk in
        StartCoroutine(EndlessShockwave());
        BossTimeline timeline = FindFirstObjectByType<BossTimeline>();
        if (timeline != null) timeline.Announce("ENDLESS MODE", "here come the big cats");
    }

    // A red shockwave rolls out from Mira and every cat it passes vanishes (no XP, no drops);
    // the sky turns red behind it (Lighting).
    private System.Collections.IEnumerator EndlessShockwave()
    {
        const float speed = 22f, maxRadius = 26f;
        Color red = new Color(1f, 0.25f, 0.3f);
        Vector2 center = player.position;
        Juice.Flash(new Color(1f, 0.3f, 0.3f, 0.6f), 0.8f);
        Juice.SlowMo(0.3f, 0.6f);
        CameraFollow.Shake(0.35f, 0.6f);
        CameraFollow.ZoomPunch(0.8f);
        Sfx.Play(Sfx.Id.Boom, 1f, 0.5f);
        ExplosionFx.Ring(center, maxRadius, red, maxRadius / speed);
        ExplosionFx.Ring(center, maxRadius * 0.6f, Color.white, maxRadius / speed * 0.7f);
        Lighting.Flash(center, 14f, red, 1f);

        var targets = new List<EnemyHealth>();
        foreach (EnemyHealth e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            if (!e.IsDead && !e.TryGetComponent(out Crate _)) targets.Add(e);

        for (float radius = 0f; targets.Count > 0; radius += speed * Time.unscaledDeltaTime)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                EnemyHealth e = targets[i];
                if (e == null || !e.gameObject.activeInHierarchy || e.IsDead) { targets.RemoveAt(i); continue; }
                if (radius < maxRadius && Vector2.Distance(center, e.transform.position) > radius) continue;
                Juice.CatPuff(e.transform.position, new Color(1f, 0.6f, 0.6f));
                Pool.Despawn(e.gameObject);
                targets.RemoveAt(i);
            }
            yield return null;
        }
    }

    private void UpdateEndless()
    {
        if (miniBosses.Count == 0 || (endlessTimer -= Time.deltaTime) > 0f) return;
        float minutes = EndlessMinutes;
        endlessTimer = Mathf.Max(endlessMinWaveInterval, endlessWaveInterval - minutes * 0.25f);

        // a full ring closing in from every side, slightly jittered so it doesn't look like a drawn circle
        int ring = Mathf.Min(endlessRingMax, endlessRingStart + Mathf.FloorToInt(minutes * endlessRingPerMinute));
        ring = Mathf.Min(ring, endlessMaxAlive - EnemyAI.AliveCount);
        float turn = Random.Range(0f, 360f);
        float health = endlessHealthStart + minutes * endlessHealthPerMinute;
        for (int i = 0; i < ring; i++)
        {
            float angle = (turn + i * 360f / ring + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
            Vector2 at = (Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(minSpawnRadius, maxSpawnRadius);
            SpawnMiniBoss(miniBosses[Random.Range(0, miniBosses.Count)], at, health, unstoppable: true);
        }
    }

    private float LateBoost => 1f + Mathf.Clamp(runTime - lateSpawnStart, 0f, lateSpawnRampSeconds * (lateSpawnMaxBoost - 1f)) / lateSpawnRampSeconds;

    [System.Serializable]
    public class MiniBoss
    {
        public float time = 90f;            // seconds into the run
        public GameObject prefab;
        public float scale = 3f;
        public float healthMultiplier = 1f; // on top of the regular cat HP growth
        [System.NonSerialized] public bool spawned;
    }

    [Header("Minibosses (HoloCure style: no warning or HP bar, a big tanky cat just walks in)")]
    public List<MiniBoss> miniBosses = new List<MiniBoss>();

    [Header("Swarms (a school of cats crossing the screen in a straight line)")]
    public float firstSwarmTime = 50f;
    public float swarmInterval = 70f;
    public int swarmSizeStart = 12;
    public float swarmSizeGrowthPerMinute = 2f;
    public int swarmSizeMax = 30;
    public float swarmSpeed = 4f;
    private float swarmTimer;

    [Header("Crates (break for burgers, pickups, rarely a chest)")]
    public float crateInterval = 25f;
    public int maxCrates = 4;
    public float crateHealth = 15f;
    private float crateTimer = 10f;

    [Header("Stage events (one at random every interval)")]
    public float firstEventTime = 100f;
    public float eventInterval = 90f;
    public int encircleCount = 28;
    public float goldenCatHealth = 15f;   // x a normal cat's HP at that point in the run
    public int goldenCatXp = 60;
    private float eventTimer; // 0: the first event fires right at firstEventTime

    private float runTime = 0f;
    private float difficultyTimer = 0f;
    private float difficultyMultiplier = 1f;
    private Dictionary<EnemyWave, float> spawnTimers = new Dictionary<EnemyWave, float>();

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        foreach (var wave in enemyWaves)
        {
            spawnTimers[wave] = 0f;
        }

        Prewarm();
    }

    // Build pools up front so the first big waves (and first explosions/numbers) don't hitch.
    private void Prewarm()
    {
        foreach (var wave in enemyWaves) Pool.Prewarm(wave.enemyPrefab, wave.special ? 6 : 40); // rare specials need few

        if (player != null && player.TryGetComponent(out PlayerAutoAttack attack)) Pool.Prewarm(attack.bombPrefab, 40);
        if (enemyWaves.Count > 0 && enemyWaves[0].enemyPrefab != null &&
            enemyWaves[0].enemyPrefab.TryGetComponent(out EnemyHealth health)) Pool.Prewarm(health.xpGemPrefab, 150);

        DamagePopup.Prewarm(40);
        ExplosionFx.Prewarm(30);
        LavaArt.Prewarm(); // fire pools: drawn now so the first molotov doesn't hitch
        StompFx.Prewarm();
    }

    private void Update()
    {
        if (player == null) return;

        runTime += Time.deltaTime;
        difficultyTimer += Time.deltaTime;
        DamageScale = (1f + runTime / 60f * damageGrowthPerMinute) * difficulty;

        if ((crateTimer -= Time.deltaTime) <= 0f)
        {
            crateTimer = crateInterval;
            if (Crate.Count < maxCrates) SpawnCrate();
        }

        if (Endless) { UpdateEndless(); return; }

        float lateBoost = LateBoost;
        int cap = maxEnemies + Mathf.RoundToInt(lateMaxEnemiesBonus * (lateBoost - 1f) / Mathf.Max(0.01f, lateSpawnMaxBoost - 1f));

        if (difficultyTimer >= difficultyRampInterval)
        {
            difficultyTimer = 0f;
            difficultyMultiplier *= difficultyMultiplierStep;
        }

        foreach (var wave in enemyWaves)
        {
            if (runTime < wave.timeToUnlock) continue;

            spawnTimers[wave] += Time.deltaTime;
            float effectiveInterval = Mathf.Max(minSpawnInterval, wave.spawnInterval / maxSpeedup, wave.spawnInterval * difficultyMultiplier) / lateBoost;

            if (spawnTimers[wave] >= effectiveInterval && EnemyAI.AliveCount < cap)
            {
                spawnTimers[wave] = 0f;
                SpawnEnemy(wave.enemyPrefab);
            }
        }

        foreach (MiniBoss m in miniBosses)
        {
            if (m.spawned || runTime < m.time) continue;
            m.spawned = true;
            SpawnMiniBoss(m);
        }

        if (runTime >= firstSwarmTime && (swarmTimer -= Time.deltaTime) <= 0f)
        {
            swarmTimer = swarmInterval;
            SpawnSwarm();
        }

        if (runTime >= firstEventTime && (eventTimer -= Time.deltaTime) <= 0f)
        {
            eventTimer = eventInterval;
            StartEvent(Random.Range(0, 4));
        }
    }

    // Not pooled (it's scaled up), spawned off-screen like any cat; its prefab carries the tankiness and XP.
    public void SpawnMiniBoss(MiniBoss m) => SpawnMiniBoss(m, GetSpawnPositionAroundPlayer(), 1f, false);

    public void SpawnMiniBoss(MiniBoss m, Vector2 at, float extraHealth, bool unstoppable)
    {
        if (m.prefab == null || player == null) return;
        GameObject boss = Instantiate(m.prefab, at, Quaternion.identity);
        boss.transform.localScale *= m.scale;
        if (boss.TryGetComponent(out EnemyAI ai))
        {
            ai.contactRange *= m.scale;
            ai.sleepChance = 0f; // set before its Start runs
            ai.unstoppable = unstoppable;
        }
        if (boss.TryGetComponent(out EnemyHealth health))
        {
            health.ScaleHealth(m.healthMultiplier * HealthMultiplier * extraHealth);
            health.xpValue = Mathf.RoundToInt(health.xpValue * m.healthMultiplier);
        }
    }

    // HoloCure-style school: starts off-screen on one side, aimed at where the player is right now,
    // and runs straight across in formation until it's off-screen on the far side (EnemyAI.Swarm).
    public void SpawnSwarm(GameObject prefab = null)
    {
        if (player == null) return;
        if (prefab == null) prefab = RandomUnlockedPrefab();
        if (prefab == null) return;

        int count = Mathf.Min(swarmSizeMax, swarmSizeStart + Mathf.FloorToInt(runTime / 60f * swarmSizeGrowthPerMinute));
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 side = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 start = (Vector2)player.position + side * maxSpawnRadius;
        Vector2 dir = -side;                         // straight at the player's current spot
        Vector2 across = Vector2.Perpendicular(dir);
        const int PerRow = 4;
        const float Gap = 0.9f;

        for (int i = 0; i < count; i++)
        {
            int row = i / PerRow, col = i % PerRow;
            Vector2 pos = start - dir * row * Gap + across * (col - (PerRow - 1) / 2f) * Gap + Random.insideUnitCircle * 0.25f;
            GameObject cat = Pool.Spawn(prefab, pos, Quaternion.identity);
            if (cat.TryGetComponent(out EnemyHealth health)) health.ScaleHealth(HealthMultiplier);
            if (cat.TryGetComponent(out EnemyAI ai)) ai.Swarm(dir, swarmSpeed);
        }
    }

    // On screen near the edge, so you notice it and detour for it.
    public void SpawnCrate()
    {
        if (player == null) return;
        Vector2 pos = (Vector2)player.position + Random.insideUnitCircle.normalized * Random.Range(5f, 8f);
        Crate.Spawn(pos, crateHealth * HealthMultiplier);
    }

    public enum StageEvent { Encircle, Stampede, GoldenCat, ElitePack }

    public void StartEvent(int which)
    {
        if (player == null) return;
        var e = (StageEvent)which;
        string banner = e == StageEvent.Encircle ? "SURROUNDED!" : e == StageEvent.Stampede ? "STAMPEDE!" : e == StageEvent.ElitePack ? "ELITE PACK!" : "A GOLDEN CAT!";
        DamagePopup.SpawnText((Vector2)player.position + Vector2.up * 1.6f, banner, new Color(1f, 0.85f, 0.3f), 8f);
        Sfx.Play(Sfx.Id.Alert, 0.8f);

        if (e == StageEvent.Encircle) SpawnEncircle();
        else if (e == StageEvent.Stampede) for (int i = 0; i < 3; i++) SpawnSwarm();
        else if (e == StageEvent.ElitePack) SpawnElitePack();
        else StartCoroutine(SpawnGoldenCat());
    }

    // A full ring of awake cats just off-screen, all closing in at once.
    private void SpawnEncircle()
    {
        GameObject prefab = RandomUnlockedPrefab();
        if (prefab == null) return;
        int count = encircleCount + Mathf.FloorToInt(runTime / 60f) * 2;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            Vector2 pos = (Vector2)player.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * minSpawnRadius;
            GameObject cat = Pool.Spawn(prefab, pos, Quaternion.identity);
            if (cat.TryGetComponent(out EnemyHealth health)) health.ScaleHealth(HealthMultiplier);
            if (cat.TryGetComponent(out EnemyAI ai)) ai.WakeUp();
        }
    }

    // A pack of awake elites (random modifiers) charging in together from one side.
    private void SpawnElitePack()
    {
        GameObject prefab = RandomUnlockedPrefab();
        if (prefab == null) return;
        int count = Mathf.Min(8, 4 + Mathf.FloorToInt(runTime / 240f));
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 center = (Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * minSpawnRadius;
        for (int i = 0; i < count; i++)
        {
            GameObject cat = Pool.Spawn(prefab, center + Random.insideUnitCircle * 1.5f, Quaternion.identity);
            if (cat.TryGetComponent(out EnemyHealth health)) { health.ScaleHealth(HealthMultiplier); health.MakeElite(eliteHealth, eliteXpMultiplier); }
            if (cat.TryGetComponent(out EnemyAI ai)) { ai.MakeElite(); ai.WakeUp(); }
        }
    }

    // A tough gold cat runs past the player and off the far side. Catch it for a pile of XP and a chest.
    private System.Collections.IEnumerator SpawnGoldenCat()
    {
        GameObject prefab = enemyWaves.Count > 0 ? enemyWaves[0].enemyPrefab : null;
        if (prefab == null) yield break;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 side = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 start = (Vector2)player.position + side * maxSpawnRadius + Vector2.Perpendicular(side) * 2f;
        GameObject cat = Instantiate(prefab, start, Quaternion.identity); // not pooled: the tint and loot are one-off
        cat.transform.localScale *= 1.4f;
        if (cat.TryGetComponent(out EnemyHealth health))
        {
            health.Tint(new Color(1f, 0.82f, 0.25f));
            health.ScaleHealth(goldenCatHealth * HealthMultiplier);
            health.xpValue = goldenCatXp + Mathf.FloorToInt(runTime / 60f) * 10;
            health.xpGemCount = 12;
            health.dropsHoloBox = true;
            health.golden = true;
        }
        OffscreenArrows.Track(cat.transform, new Color(1f, 0.82f, 0.25f));
        yield return null; // its Start resets the AI; set the run after that
        if (cat != null && cat.TryGetComponent(out EnemyAI ai)) ai.Swarm(-side, 3f);
    }

    private GameObject RandomUnlockedPrefab()
    {
        var unlocked = enemyWaves.FindAll(w => w.enemyPrefab != null && !w.special && runTime >= w.timeToUnlock);
        return unlocked.Count > 0 ? unlocked[Random.Range(0, unlocked.Count)].enemyPrefab : null;
    }

    // ---- admin tool ----
    public void SpawnNow(GameObject prefab) => SpawnEnemy(prefab);

    public void Advance(float seconds)
    {
        runTime += seconds;
        for (difficultyTimer += seconds; difficultyTimer >= difficultyRampInterval; difficultyTimer -= difficultyRampInterval)
            difficultyMultiplier *= difficultyMultiplierStep;
    }

    // How much tougher cats are by now (also applied to cat-based minibosses by BossTimeline).
    public float HealthMultiplier => (1f + runTime / 60f * healthGrowthPerMinute + (runTime / 60f) * (runTime / 60f) * healthGrowthQuadratic) * difficulty;

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null || player == null) return;

        GameObject enemy = Pool.Spawn(prefab, GetSpawnPositionAroundPlayer(), Quaternion.identity);
        if (enemy.TryGetComponent(out EnemyHealth health))
        {
            health.ScaleHealth(HealthMultiplier);
            float eliteChance = Mathf.Min(eliteChanceMax, (runTime - eliteStartTime) / 60f * eliteChancePerMinute);
            if (eliteChance > 0f && Random.value < eliteChance && enemy.TryGetComponent(out EnemyAI ai))
            {
                health.MakeElite(eliteHealth, eliteXpMultiplier);
                ai.MakeElite();
            }
        }
    }

    private Vector2 GetSpawnPositionAroundPlayer()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minSpawnRadius, maxSpawnRadius);

        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        return (Vector2)player.position + offset;
    }
}
