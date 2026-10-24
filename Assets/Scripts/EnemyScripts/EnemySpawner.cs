using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyWave
    {
        public string name = "Basic Enemy";
        public GameObject enemyPrefab;
        public float spawnInterval = 2f;
        public float timeToUnlock = 0f;
        public bool special;
    }

    [Header("References")]
    public Transform player;

    [Header("Enemy Waves")]
    public List<EnemyWave> enemyWaves = new List<EnemyWave>();

    [Header("Spawn Ring (should be just outside camera view)")]
    public float minSpawnRadius = 11f;
    public float maxSpawnRadius = 13f;

    [Header("Overall difficulty: x enemy HP and x enemy damage (1 = original tuning)")]
    [Range(0.5f, 1.5f)] public float difficulty = 0.61f;

    [Header("Difficulty Scaling")]
    public float difficultyRampInterval = 30f;
    public float difficultyMultiplierStep = 0.88f;
    public float minSpawnInterval = 0.15f;
    public float maxSpeedup = 4f;
    public int maxEnemies = 250;
    public float healthGrowthPerMinute = 0.4f;

    [Header("Late-game pressure (on top of the above)")]
    public float healthGrowthQuadratic = 0.05f;
    public float damageGrowthPerMinute = 0.18f;
    [Tooltip("Seconds after StartLateGame (the 10-min boss kill)")]
    public float lateSpawnStart = 0f;
    public float lateSpawnRampSeconds = 300f;
    public float lateSpawnMaxBoost = 3f;
    public int lateMaxEnemiesBonus = 100;

    [Header("Elites: random tougher cats with a red glow that drop a gem shower")]
    public float eliteStartTime = 120f;
    public float eliteChancePerMinute = 0.012f;
    public float eliteChanceMax = 0.1f;
    public float eliteHealth = 8f;
    public int eliteXpMultiplier = 6;

    [Header("Surge: extra scaling so a maxed build can't just stand still")]
    [Tooltip("Seconds after StartLateGame (the 10-min boss kill)")]
    public float surgeStart = 30f;
    public float surgeHealthQuadratic = 0.1f;
    public float surgeDamagePerMinute = 0.15f;
    public float surgeSpeedPerMinute = 0.05f;
    public float endlessSpeedPerMinute = 0.12f;
    public float maxSpeedScale = 2.2f;
    private float SurgeMinutes => Mathf.Max(0f, (runTime - lateFrom - surgeStart) / 60f);

    // Late spawn boost and surge stay off until this is called (BossTimeline, on the late-game boss kill).
    private float lateFrom = float.PositiveInfinity;
    public void StartLateGame() => lateFrom = Mathf.Min(lateFrom, runTime);

    [Header("Power steps: x enemy HP and damage at 10:00, and again when the 15:00 boss dies")]
    public float powerStep = 1.2f;
    public float firstStepTime = 600f;
    private float stepMul = 1f;
    private bool firstStepDone;

    // Called by BossTimeline when the 15-min boss dies (silently: the super-combo banner shows then).
    public void StepUp(string why)
    {
        stepMul *= powerStep;
        BossTimeline timeline = FindFirstObjectByType<BossTimeline>();
        if (timeline != null && why != null) timeline.Announce("THE CATS GROW STRONGER", why);
    }

    public static float DamageScale { get; private set; } = 1f;
    public static float SpeedScale { get; private set; } = 1f;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => DamageScale = SpeedScale = 1f;

    [Header("Endless (HoloCure 30-min style): when the final boss (BossTimeline, 20:00) dies, every enemy vanishes and only minibosses come")]
    public float endlessWaveInterval = 2.2f;
    public float endlessMinWaveInterval = 0.9f;
    public int endlessRingStart = 14;
    public int endlessRingPerMinute = 4;
    public int endlessRingMax = 36;
    public int endlessMaxAlive = 500;
    public float endlessHealthStart = 1f;
    public float endlessHealthPerMinute = 0.4f;
    [Tooltip("Regular waves keep spawning in endless, this many times faster (grows per minute, capped)")]
    public float endlessHordeStart = 3f;
    public float endlessHordePerMinute = 0.5f;
    public float endlessHordeMax = 8f;
    public int endlessBigCatRoom = 120; // regular cats stop at endlessMaxAlive minus this, so the big-cat rings still fit
    public float endlessSpeedStart = 0.35f;
    public static bool Endless { get; private set; }
    private float endlessStart, endlessTimer;

    public float RunTime => runTime;
    public float EndlessMinutes => Endless ? (runTime - endlessStart) / 60f : 0f;

    private void Awake()
    {
        Endless = false;
        SpeedScale = 1f;
        gameObject.AddComponent<WorldEvents>(); // golden hour, meteor showers, shrines
    }

    public void StartEndless()
    {
        if (Endless) return;
        Endless = true;
        endlessStart = runTime;
        StartLateGame(); // in case the late-game boss was never killed
        endlessTimer = 2.5f;
        StartCoroutine(EndlessShockwave());
        BossTimeline timeline = FindFirstObjectByType<BossTimeline>();
        if (timeline != null) timeline.Announce("ENDLESS MODE", "here come the big cats");
    }

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

    [Header("Endless milestones: every N minutes a boss returns and a new twist kicks in")]
    public float milestoneMinutes = 5f;
    private int milestone;
    private float endlessWaveRate = 1f, endlessDamageMul = 1f, endlessHealthMul = 1f, endlessEliteChance;

    private void CheckMilestone()
    {
        int reached = Mathf.FloorToInt(EndlessMinutes / milestoneMinutes);
        if (reached <= milestone) return;
        milestone = reached;

        string twist;
        switch (milestone)
        {
            case 1: endlessEliteChance = 0.25f; twist = "big cats can now be elite"; break;
            case 2: endlessWaveRate = 2f; twist = "waves come twice as fast"; break;
            case 3: endlessDamageMul = 2f; twist = "cats hit twice as hard"; break;
            default: endlessHealthMul *= 2f; endlessDamageMul *= 1.25f; twist = "the cats got tougher. again"; break;
        }

        int seconds = Mathf.RoundToInt(milestone * milestoneMinutes * 60f);
        BossTimeline timeline = FindFirstObjectByType<BossTimeline>();
        if (timeline != null)
        {
            timeline.Announce($"ENDLESS {seconds / 60}:{seconds % 60:00}", twist + " + a boss returns");
            timeline.SpawnEndlessBoss(milestone);
        }
        Juice.Flash(new Color(1f, 0.3f, 0.3f, 0.4f), 0.5f);
        CameraFollow.Shake(0.25f, 0.4f);
    }

    private void UpdateEndless()
    {
        CheckMilestone();
        if (miniBosses.Count == 0 || (endlessTimer -= Time.deltaTime) > 0f) return;
        float minutes = EndlessMinutes;
        endlessTimer = Mathf.Max(endlessMinWaveInterval, endlessWaveInterval - minutes * 0.25f) / endlessWaveRate;

        int ring = Mathf.Min(endlessRingMax, endlessRingStart + Mathf.FloorToInt(minutes * endlessRingPerMinute));
        ring = Mathf.Min(ring, endlessMaxAlive - EnemyAI.AliveCount);
        float turn = Random.Range(0f, 360f);
        float health = endlessHealthStart + minutes * endlessHealthPerMinute; // endlessHealthMul is already in HealthMultiplier
        for (int i = 0; i < ring; i++)
        {
            float angle = (turn + i * 360f / ring + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
            Vector2 at = (Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(minSpawnRadius, maxSpawnRadius);
            GameObject cat = SpawnMiniBoss(miniBosses[Random.Range(0, miniBosses.Count)], at, health, unstoppable: true);
            if (cat != null && Random.value < endlessEliteChance && cat.TryGetComponent(out EnemyHealth h) && cat.TryGetComponent(out EnemyAI ai))
            {
                h.MakeElite(2f, 2);
                ai.MakeElite();
            }
        }
    }

    private float LateBoost => 1f + Mathf.Clamp(runTime - lateFrom - lateSpawnStart, 0f, lateSpawnRampSeconds * (lateSpawnMaxBoost - 1f)) / lateSpawnRampSeconds;

    [System.Serializable]
    public class MiniBoss
    {
        public float time = 90f;
        public GameObject prefab;
        public float scale = 3f;
        public float healthMultiplier = 1f;
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

    [Header("Mouse packs (a scurry of tiny, fast, fragile mice that rush you together)")]
    public GameObject mousePrefab;
    public float firstMousePackTime = 20f;
    public float mousePackInterval = 15f;
    public int mousePackSizeStart = 10;
    public float mousePackGrowthPerMinute = 2f;
    public int mousePackSizeMax = 30;
    private float mouseTimer;

    [Header("Crates (break for burgers, pickups, rarely a chest)")]
    public float crateInterval = 25f;
    public int maxCrates = 4;
    public float crateHealth = 15f;
    private float crateTimer = 10f;

    [Header("Stage events (one at random every interval)")]
    public float firstEventTime = 100f;
    public float eventInterval = 90f;
    public int encircleCount = 28;
    public float goldenCatHealth = 15f;
    public int goldenCatXp = 60;
    private float eventTimer;

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

    private void Prewarm()
    {
        foreach (var wave in enemyWaves) Pool.Prewarm(wave.enemyPrefab, wave.special ? 6 : 40);
        if (mousePrefab != null) Pool.Prewarm(mousePrefab, mousePackSizeMax);

        if (player != null && player.TryGetComponent(out PlayerAutoAttack attack)) Pool.Prewarm(attack.bombPrefab, 40);
        if (enemyWaves.Count > 0 && enemyWaves[0].enemyPrefab != null &&
            enemyWaves[0].enemyPrefab.TryGetComponent(out EnemyHealth health)) Pool.Prewarm(health.xpGemPrefab, 150);

        DamagePopup.Prewarm(40);
        ExplosionFx.Prewarm(30);
        LavaArt.Prewarm();
        StompFx.Prewarm();
    }

    private void Update()
    {
        if (player == null) return;

        runTime += Time.deltaTime;
        difficultyTimer += Time.deltaTime;
        if (!firstStepDone && runTime >= firstStepTime) { firstStepDone = true; StepUp("10 minutes in"); }
        DamageScale = (1f + runTime / 60f * damageGrowthPerMinute) * difficulty * (1f + SurgeMinutes * surgeDamagePerMinute) * endlessDamageMul * stepMul;
        SpeedScale = Mathf.Min(maxSpeedScale, 1f + SurgeMinutes * surgeSpeedPerMinute + (Endless ? endlessSpeedStart + EndlessMinutes * endlessSpeedPerMinute : 0f));

        if ((crateTimer -= Time.deltaTime) <= 0f)
        {
            crateTimer = crateInterval;
            if (Crate.Count < maxCrates) SpawnCrate();
        }

        if (Endless) UpdateEndless();

        // Endless (HoloCure style): a flood of regular cats on top of the big-cat rings.
        float lateBoost = LateBoost * (Endless ? Mathf.Min(endlessHordeMax, endlessHordeStart + EndlessMinutes * endlessHordePerMinute) * endlessWaveRate : 1f);
        int cap = Endless ? endlessMaxAlive - endlessBigCatRoom : AliveCap;

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

        if (Endless) return; // timed minibosses, swarms, mice and events belong to the main run

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

        if (mousePrefab != null && runTime >= firstMousePackTime && (mouseTimer -= Time.deltaTime) <= 0f)
        {
            mouseTimer = mousePackInterval;
            SpawnMousePack();
        }

        if (runTime >= firstEventTime && (eventTimer -= Time.deltaTime) <= 0f)
        {
            eventTimer = eventInterval;
            StartEvent(Random.Range(0, 4));
        }
    }

    public void SpawnMiniBoss(MiniBoss m) => SpawnMiniBoss(m, GetSpawnPositionAroundPlayer(), 1f, false);

    public GameObject SpawnMiniBoss(MiniBoss m, Vector2 at, float extraHealth, bool unstoppable)
    {
        if (m.prefab == null || player == null) return null;
        GameObject boss = Instantiate(m.prefab, at, Quaternion.identity);
        boss.transform.localScale *= m.scale;
        if (boss.TryGetComponent(out EnemyAI ai))
        {
            ai.contactRange *= m.scale;
            ai.sleepChance = 0f;
            ai.unstoppable = unstoppable;
        }
        if (unstoppable && boss.TryGetComponent(out EnemyHealth loot))
        {
            loot.dropsHoloBox = false; // endless rings spawn dozens; guaranteed chests/burgers flooded the map
            loot.burgerDropChance = 0.02f;
        }
        if (boss.TryGetComponent(out EnemyHealth health))
        {
            health.ScaleHealth(m.healthMultiplier * HealthMultiplier * extraHealth);
            health.xpValue = Mathf.RoundToInt(health.xpValue * m.healthMultiplier);
        }
        return boss;
    }

    public void SpawnSwarm(GameObject prefab = null)
    {
        if (player == null) return;
        if (prefab == null) prefab = RandomUnlockedPrefab();
        if (prefab == null) return;

        int count = Mathf.Min(swarmSizeMax, swarmSizeStart + Mathf.FloorToInt(runTime / 60f * swarmSizeGrowthPerMinute));
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 side = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 start = (Vector2)player.position + side * maxSpawnRadius;
        Vector2 dir = -side;
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

    public void SpawnMousePack()
    {
        if (player == null || mousePrefab == null) return;
        int count = Mathf.Min(mousePackSizeMax, mousePackSizeStart + Mathf.FloorToInt(runTime / 60f * mousePackGrowthPerMinute), AliveCap - EnemyAI.AliveCount);
        Vector2 center = GetSpawnPositionAroundPlayer();
        for (int i = 0; i < count; i++)
        {
            GameObject mouse = Pool.Spawn(mousePrefab, center + Random.insideUnitCircle * 1.2f, Quaternion.identity);
            if (mouse.TryGetComponent(out EnemyHealth health)) health.ScaleHealth(HealthMultiplier);
        }
    }

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

    private System.Collections.IEnumerator SpawnGoldenCat()
    {
        GameObject prefab = enemyWaves.Count > 0 ? enemyWaves[0].enemyPrefab : null;
        if (prefab == null) yield break;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 side = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 start = (Vector2)player.position + side * maxSpawnRadius + Vector2.Perpendicular(side) * 2f;
        GameObject cat = Instantiate(prefab, start, Quaternion.identity);
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
        yield return null;
        if (cat != null && cat.TryGetComponent(out EnemyAI ai)) ai.Swarm(-side, 3f);
    }

    private GameObject RandomUnlockedPrefab()
    {
        var unlocked = enemyWaves.FindAll(w => w.enemyPrefab != null && !w.special && runTime >= w.timeToUnlock);
        return unlocked.Count > 0 ? unlocked[Random.Range(0, unlocked.Count)].enemyPrefab : null;
    }

    public void SpawnNow(GameObject prefab) => SpawnEnemy(prefab);

    // Boss summon: a ring of awake cats around a point.
    public void SpawnAround(Vector2 center, int count, float radius)
    {
        count = Mathf.Min(count, AliveCap - EnemyAI.AliveCount);
        GameObject prefab = RandomUnlockedPrefab();
        if (prefab == null || count <= 0) return;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            GameObject cat = Pool.Spawn(prefab, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, Quaternion.identity);
            Juice.CatPuff(cat.transform.position, new Color(0.6f, 0.8f, 1f));
            if (cat.TryGetComponent(out EnemyHealth health)) health.ScaleHealth(HealthMultiplier);
            if (cat.TryGetComponent(out EnemyAI ai)) ai.WakeUp();
        }
    }

    private int AliveCap => Endless ? endlessMaxAlive
        : maxEnemies + Mathf.RoundToInt(lateMaxEnemiesBonus * (LateBoost - 1f) / Mathf.Max(0.01f, lateSpawnMaxBoost - 1f));

    public void Advance(float seconds)
    {
        runTime += seconds;
        for (difficultyTimer += seconds; difficultyTimer >= difficultyRampInterval; difficultyTimer -= difficultyRampInterval)
            difficultyMultiplier *= difficultyMultiplierStep;
    }

    public float HealthMultiplier => (1f + runTime / 60f * healthGrowthPerMinute + (runTime / 60f) * (runTime / 60f) * healthGrowthQuadratic) * difficulty
                                    * (1f + SurgeMinutes * SurgeMinutes * surgeHealthQuadratic) * stepMul * endlessHealthMul;

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
