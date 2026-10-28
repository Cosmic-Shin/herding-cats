using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BossTimeline : MonoBehaviour
{
    [System.Serializable]
    public class BossEvent
    {
        public float time = 180f;
        public GameObject prefab;
        public float healthMultiplier = 1f;
        public float scale = 1f;
        [Tooltip("1 = prefab as-is. Higher = faster, more attacks, more damage (BossAI.Empower).")]
        public float power = 1f;
        public BossAI.Special specials;
        public Color tint = Color.white;
        public string titleOverride;
        public bool unlocksSupers;
        [Tooltip("Killing this boss starts the spawner's late-game ramp")]
        public bool startsLateGame;
        [System.NonSerialized] public bool warned, spawned;
    }

    public List<BossEvent> events = new List<BossEvent>();
    public float warningLead = 3f;
    public float spawnDistance = 11f;
    [Tooltip("x boss HP on top of each event's multiplier (bosses skip the spawner's difficulty dial)")]
    [Range(0.5f, 1.5f)] public float bossHealth = 0.72f;

    [Header("Companion")]
    public EnemyAnimator companionCat;
    private bool companionDropped;

    [Header("UI")]
    public TextMeshProUGUI warningText;
    public GameObject bossBar;
    public RectTransform bossBarFill;
    public TextMeshProUGUI bossBarName;

    private readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    private readonly List<EnemyHealth> superKeys = new List<EnemyHealth>();
    private Transform player;
    private Camera cam;
    private SpriteRenderer arrow;
    private float runTime;
    private float warningTimer;

    private EnemyHealth finalBoss, lateKey;
    private bool cleared;

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        cam = Camera.main;
        arrow = CreateArrow();
        if (warningText != null) warningText.gameObject.SetActive(false);
        if (bossBar != null) bossBar.SetActive(false);
        StyleBossBar();
    }

    private void StyleBossBar()
    {
        if (bossBar == null) return;
        var rt = (RectTransform)bossBar.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 36f);
        if (bossBar.TryGetComponent(out UnityEngine.UI.Image frame)) CatTheme.BarFrame(frame);
        if (bossBarFill != null)
        {
            bossBarFill.offsetMin = new Vector2(9f, 9f);
            bossBarFill.offsetMax = new Vector2(-9f, -9f);
            if (bossBarFill.TryGetComponent(out UnityEngine.UI.Image fill)) CatTheme.BarFill(fill, CatTheme.BossPurple);
        }
        if (bossBarName != null) bossBarName.color = new Color(0.93f, 0.75f, 1f);
    }

    private void Update()
    {
        if (player == null) return;
        runTime += Time.deltaTime;

        foreach (BossEvent e in events)
        {
            if (EnemySpawner.Endless) break;
            if (!e.warned && runTime >= e.time - warningLead)
            {
                e.warned = true;
                ShowWarning(DisplayName(e));
            }
            if (!e.spawned && runTime >= e.time)
            {
                e.spawned = true;
                Spawn(e);
            }
        }

        UpdateWarning();
        if (!cleared && finalBoss != null && finalBoss.IsDead)
        {
            cleared = true;
            StartCoroutine(StageClear());
        }
        DropCompanionOnFirstKill();
        UnlockSupersOnKill();
        if (lateKey != null && lateKey.IsDead)
        {
            lateKey = null;
            FindFirstObjectByType<EnemySpawner>().StartLateGame();
        }
        alive.RemoveAll(h => h == null || h.IsDead);
        UpdateBossBar();
        UpdateArrow();
    }

    public void Advance(float seconds) => runTime += seconds;
    public void AdminSpawn(BossEvent e) => Spawn(e);

    public void SpawnEndlessBoss(int milestone)
    {
        BossEvent last = null;
        foreach (BossEvent e in events) if (last == null || e.time > last.time) last = e;
        if (last == null) return;
        Spawn(new BossEvent
        {
            prefab = last.prefab,
            scale = last.scale,
            healthMultiplier = last.healthMultiplier * (1f + milestone),
            power = last.power + 0.5f * milestone,
            specials = BossAI.Special.Spiral | BossAI.Special.Summon | BossAI.Special.Laser,
            tint = Color.Lerp(last.tint, new Color(1f, 0.3f, 0.3f), Mathf.Min(1f, milestone * 0.3f)),
            titleOverride = $"{DisplayName(last)} Reborn {ToRoman(milestone)}",
        });
    }

    private static string ToRoman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };
    public bool BossFight => alive.Count > 0;
    public void AdminDropCompanion(Vector2 at)
    {
        if (companionCat == null) return;
        CompanionPickup.Drop(at, companionCat);
        companionDropped = true;
    }

    private void UnlockSupersOnKill()
    {
        for (int i = superKeys.Count - 1; i >= 0; i--)
        {
            if (superKeys[i] == null) { superKeys.RemoveAt(i); continue; }
            if (!superKeys[i].IsDead) continue;
            superKeys.RemoveAt(i);
            FindFirstObjectByType<EnemySpawner>()?.StepUp(null);
            PlayerCollabs.UnlockSupers();
            Announce("SUPER COMBINES", $"unlocked: fuse two combos with {Keybinds.Prompt(Keybinds.Action.Combine)}");
        }
    }

    private void DropCompanionOnFirstKill()
    {
        if (companionDropped || companionCat == null) return;
        foreach (EnemyHealth boss in alive)
        {
            if (boss == null || !boss.IsDead) continue;
            CompanionPickup.Drop(boss.transform.position, companionCat);
            companionDropped = true;
            return;
        }
    }

    private static string DisplayName(BossEvent e)
    {
        if (!string.IsNullOrEmpty(e.titleOverride)) return e.titleOverride;
        return e.prefab != null && e.prefab.TryGetComponent(out BossAI ai) ? ai.bossName : "Boss";
    }

    private bool IsFinal(BossEvent e)
    {
        foreach (BossEvent other in events) if (other.time > e.time) return false;
        return true;
    }

    private System.Collections.IEnumerator StageClear()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null) spawner.StartEndless();
    }

    private void Spawn(BossEvent e)
    {
        if (e.prefab == null) return;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 pos = (Vector2)player.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnDistance;
        GameObject boss = Instantiate(e.prefab, pos, Quaternion.identity);
        boss.transform.localScale *= e.scale;
        if (boss.TryGetComponent(out BossAI ai))
        {
            ai.contactRange *= e.scale;
            ai.Empower(e.power);
            ai.specials = e.specials;
            if (!string.IsNullOrEmpty(e.titleOverride)) ai.bossName = e.titleOverride;
        }
        if (boss.TryGetComponent(out EnemyHealth health))
        {
            health.ScaleHealth(e.healthMultiplier * bossHealth);
            if (e.tint != Color.white && e.tint.a > 0f) health.Tint(e.tint);
            health.xpValue = Mathf.RoundToInt(health.xpValue * Mathf.Sqrt(e.healthMultiplier));
            health.xpGemCount = Mathf.RoundToInt(health.xpGemCount * Mathf.Sqrt(e.healthMultiplier));
            alive.Add(health);
            if (IsFinal(e)) finalBoss = health;
            if (e.unlocksSupers) superKeys.Add(health);
            if (e.startsLateGame) lateKey = health;
        }
    }
    private void ShowWarning(string bossName) => Announce("WARNING!", bossName + " approaches");

    public void Announce(string title, string subtitle)
    {
        if (warningText == null) return;
        warningText.text = $"{title}\n<size=45%>{subtitle.ToUpper()}</size>";
        warningText.gameObject.SetActive(true);
        RuntimeUI.Outline(warningText, 0.22f);
        warningTimer = warningLead + 1f;
        Sfx.Play(Sfx.Id.Alert, 0.8f);
    }

    private void UpdateWarning()
    {
        if (warningText == null || warningTimer <= 0f) return;

        warningTimer -= Time.deltaTime;
        warningText.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
        if (warningTimer <= 0f) warningText.gameObject.SetActive(false);
    }

    private void UpdateBossBar()
    {
        if (bossBar == null) return;

        bool show = alive.Count > 0 && Time.timeScale > 0f;
        if (bossBar.activeSelf != show) bossBar.SetActive(show);
        if (!show) return;

        EnemyHealth boss = alive[0];
        bossBarFill.anchorMax = new Vector2(Mathf.Clamp01(boss.CurrentHealth / boss.maxHealth), 1f);
        string name = boss.TryGetComponent(out BossAI ai) ? ai.bossName : "Boss";
        bossBarName.text = alive.Count > 1 ? $"{name}  (+{alive.Count - 1})" : name;
    }

    private void UpdateArrow()
    {
        EnemyHealth target = alive.Count > 0 ? alive[0] : null;
        Vector3 vp = target != null ? cam.WorldToViewportPoint(target.transform.position) : Vector3.zero;
        bool offScreen = target != null && (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f);
        arrow.enabled = offScreen;
        if (!offScreen) return;

        const float margin = 0.08f;
        Vector3 edge = new Vector3(Mathf.Clamp(vp.x, margin, 1f - margin), Mathf.Clamp(vp.y, margin, 1f - margin), -cam.transform.position.z);
        arrow.transform.position = cam.ViewportToWorldPoint(edge);
        Vector2 d = target.transform.position - arrow.transform.position;
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        arrow.transform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 8f));
    }

    private static SpriteRenderer CreateArrow()
    {
        const int n = 32;
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float half = (n - x) * 0.5f;
                tex.SetPixel(x, y, Mathf.Abs(y + 0.5f - n / 2f) <= half * 0.9f ? Color.white : Color.clear);
            }
        tex.Apply();

        var go = new GameObject("BossArrow");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n * 1.6f);
        sr.color = new Color(1f, 0.2f, 0.15f);
        sr.sortingOrder = 200;
        sr.enabled = false;
        return sr;
    }
}
