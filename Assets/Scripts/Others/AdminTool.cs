using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AdminTool : MonoBehaviour
{
    public static bool GodMode { get; private set; }

    private static readonly float[] Speeds = { 0.5f, 1f, 2f, 4f, 8f };

    private bool open;
    private Vector2 scroll;
    private Rect window = new Rect(10, 70, 400, 600);
    private readonly HashSet<string> expanded = new HashSet<string> { "Player" };
    private float speed = 1f;

    private PlayerStats stats;
    private PlayerWeapons weapons;
    private PlayerSkills skills;
    private PlayerCollabs collabs;
    private PlayerUltimate ult;
    private MiraPassives passives;
    private EnemySpawner spawner;
    private BossTimeline bosses;
    private HUD hud;

    private void Awake()
    {
        GodMode = false;
        if (!Application.isEditor && !Debug.isDebugBuild) Destroy(this);
    }

    private void Start()
    {
        stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            weapons = stats.GetComponent<PlayerWeapons>();
            skills = stats.GetComponent<PlayerSkills>();
            collabs = stats.GetComponent<PlayerCollabs>();
            ult = stats.GetComponent<PlayerUltimate>();
            passives = stats.GetComponent<MiraPassives>();
        }
        spawner = FindFirstObjectByType<EnemySpawner>();
        bosses = FindFirstObjectByType<BossTimeline>();
        hud = FindFirstObjectByType<HUD>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) open = !open;
        if (speed != 1f && Time.timeScale == 1f) Time.timeScale = speed;
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        if (!open)
        {
            GUI.Label(new Rect(10, Screen.height / scale - 22, 200, 20), "F1: admin tool");
            return;
        }
        window = GUILayout.Window(918273, window, DrawWindow, "Admin Tool  (F1 to close)");
    }

    private void DrawWindow(int id)
    {
        if (stats == null) { GUILayout.Label("No player found."); GUI.DragWindow(); return; }

        scroll = GUILayout.BeginScrollView(scroll);
        if (Section("Player")) PlayerSection();
        if (Section("Run")) RunSection();
        if (Section("Weapons")) WeaponSection();
        if (Section("Skills")) SkillSection();
        if (Section("Combos")) ComboSection();
        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    private bool Section(string name)
    {
        bool isOpen = expanded.Contains(name);
        if (GUILayout.Button((isOpen ? "▼ " : "► ") + name, GUI.skin.box, GUILayout.ExpandWidth(true)))
        {
            if (isOpen) expanded.Remove(name); else expanded.Add(name);
            isOpen = !isOpen;
        }
        return isOpen;
    }

    private void PlayerSection()
    {
        GodMode = GUILayout.Toggle(GodMode, " God mode (take no damage)");
        GUILayout.Label($"HP {Mathf.CeilToInt(stats.currentHealth)}/{Mathf.CeilToInt(stats.maxHealth)}   Lv {stats.level}   Bombs/throw {stats.projectileCount}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Full heal")) stats.Heal(stats.maxHealth);
        if (GUILayout.Button("HP to 25%")) stats.ModifyHealth(stats.maxHealth * 0.25f - stats.currentHealth);
        if (GUILayout.Button("Die")) { GodMode = false; stats.ModifyHealth(-stats.currentHealth); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 level")) Levels(1);
        if (GUILayout.Button("+5 levels")) Levels(5);
        if (GUILayout.Button("+1 bomb/throw") && stats.projectileCount < PlayerCollabs.MaxBombsPerThrow) stats.IncreaseProjectileCount(1);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (ult != null && GUILayout.Button("Ultimate ready")) ult.Recharge();
        if (passives != null && passives.enabled && GUILayout.Button("Refill shield")) passives.RechargeShield();
        if (bosses != null && Companion.Instance == null && GUILayout.Button("Drop Cat Buddy"))
            bosses.AdminDropCompanion((Vector2)stats.transform.position + Vector2.right * 1.5f);
        GUILayout.EndHorizontal();
    }

    private void Levels(int count)
    {
        for (int i = 0; i < count; i++) stats.AddExp(Mathf.CeilToInt((stats.expToNextLevel - stats.currentExp) / Mathf.Max(0.01f, stats.expMultiplier)) + 1);
    }

    private void RunSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Speed", GUILayout.Width(50));
        foreach (float s in Speeds)
        {
            if (GUILayout.Toggle(Mathf.Approximately(speed, s), $"{s}x", GUI.skin.button) && !Mathf.Approximately(speed, s))
            {
                speed = s;
                if (Time.timeScale > 0f) Time.timeScale = s;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Skip +1 min")) Skip(60f);
        if (GUILayout.Button("Skip +5 min")) Skip(300f);
        GUILayout.EndHorizontal();
        GUILayout.Label("<size=10>Skipping makes cats tougher and spawn faster, and starts any boss whose time passed.</size>", Rich());

        if (spawner != null)
        {
            GUI.enabled = !EnemySpawner.Endless;
            if (GUILayout.Button(EnemySpawner.Endless ? "Endless mode is on" : "Skip to endless (jumps to 20:00 first)")) SkipToEndless();
            GUI.enabled = true;
        }
        if (spawner != null) spawner.enabled = GUILayout.Toggle(spawner.enabled, " Enemy spawning on");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Kill all cats")) KillAll(bossesToo: false);
        if (GUILayout.Button("Kill everything")) KillAll(bossesToo: true);
        GUILayout.EndHorizontal();

        if (spawner != null)
        {
            GUILayout.Label("Spawn 10 off-screen:");
            int perRow = 0;
            GUILayout.BeginHorizontal();
            foreach (EnemySpawner.EnemyWave wave in spawner.enemyWaves)
            {
                if (GUILayout.Button(wave.name)) for (int i = 0; i < 10; i++) spawner.SpawnNow(wave.enemyPrefab);
                if (++perRow % 3 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
        }

        if (spawner != null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Swarm")) spawner.SpawnSwarm();
            var shown = new HashSet<GameObject>();
            foreach (EnemySpawner.MiniBoss m in spawner.miniBosses)
                if (m.prefab != null && shown.Add(m.prefab) && GUILayout.Button(m.prefab.name)) spawner.SpawnMiniBoss(m);
            GUILayout.EndHorizontal();

            GUILayout.Label("Start event:");
            GUILayout.BeginHorizontal();
            foreach (EnemySpawner.StageEvent ev in System.Enum.GetValues(typeof(EnemySpawner.StageEvent)))
                if (GUILayout.Button(ev.ToString())) spawner.StartEvent((int)ev);
            GUILayout.EndHorizontal();
        }

        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Unlock all achievements"))
            {
                foreach (Achievements.Def d in Achievements.All) PlayerPrefs.SetInt("ach_" + d.id, 1);
                PlayerPrefs.Save();
            }
            if (GUILayout.Button("Reset achievements")) Achievements.ResetAll();
            GUILayout.EndHorizontal();
            GUILayout.Label("<size=10>Reroll / eliminate bonuses apply from the next run.</size>", Rich());
        }

        if (bosses != null)
        {
            GUILayout.Label("Spawn boss now:");
            foreach (BossTimeline.BossEvent e in bosses.events)
            {
                string name = !string.IsNullOrEmpty(e.titleOverride) ? e.titleOverride
                            : e.prefab != null && e.prefab.TryGetComponent(out BossAI ai) ? ai.bossName : "Boss";
                if (GUILayout.Button($"{name}  ({e.time / 60f:0.#} min)")) bosses.AdminSpawn(e);
            }
        }
    }

    private void Skip(float seconds)
    {
        if (spawner != null) spawner.Advance(seconds);
        if (bosses != null) bosses.Advance(seconds);
        if (hud != null) hud.Advance(seconds);
    }

    private void SkipToEndless()
    {
        float finalBoss = 0f;
        if (bosses != null) foreach (BossTimeline.BossEvent e in bosses.events) finalBoss = Mathf.Max(finalBoss, e.time);
        float behind = finalBoss - spawner.RunTime;
        if (behind > 0f) Skip(behind);
        spawner.StartEndless();
    }

    private static void KillAll(bool bossesToo)
    {
        foreach (EnemyHealth e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (e.IsDead || (!bossesToo && e.GetComponent<BossAI>() != null)) continue;
            DamageStats.Source = "Admin";
            e.TakeDamage(1e9f, e.transform.position);
        }
    }

    private void WeaponSection()
    {
        if (GUILayout.Button("Max every weapon (ignores the 5-slot limit)"))
            foreach (PlayerWeapons.Def d in PlayerWeapons.All) MaxWeapon(d.id);

        foreach (PlayerWeapons.Def d in PlayerWeapons.All)
        {
            int level = weapons.LevelOf(d.id);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{d.name}  {(PlayerCollabs.IsConsumed(d.id) ? "(combined)" : level == 0 ? "-" : level >= Weapon.MaxLevel ? "MAX" : "Lv " + level)}");
            GUI.enabled = level < Weapon.MaxLevel;
            if (GUILayout.Button("+1", GUILayout.Width(40))) weapons.LevelUp(d);
            if (GUILayout.Button("MAX", GUILayout.Width(50))) MaxWeapon(d.id);
            GUI.enabled = level > 0;
            if (GUILayout.Button("X", GUILayout.Width(26))) weapons.Remove(d.id);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    private void SkillSection()
    {
        foreach (PlayerSkills.Skill s in PlayerSkills.All)
        {
            int level = skills.LevelOf(s.id);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{s.name}  {(level == 0 ? "-" : level >= s.levels.Length ? "MAX" : "Lv " + level)}");
            GUI.enabled = level < s.levels.Length;
            if (GUILayout.Button("+1", GUILayout.Width(40))) skills.LevelUp(s);
            if (GUILayout.Button("MAX", GUILayout.Width(50))) MaxSkill(s.id);
            GUI.enabled = level > 0;
            if (GUILayout.Button("X", GUILayout.Width(26))) skills.Remove(s.id);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    private void ComboSection()
    {
        if (collabs == null) return;
        GUILayout.Label($"Combined {collabs.Taken.Count}/{PlayerCollabs.MaxCollabs}. \"Ready\" maxes the pieces; then press C to combine.");
        foreach (PlayerCollabs.Def d in PlayerCollabs.All)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{d.name}\n<size=10>{PlayerCollabs.Recipe(d)}</size>", Rich());
            if (collabs.InSuper(d.id)) GUILayout.Label("in super", GUILayout.Width(70));
            else if (PlayerCollabs.Has(d.id)) { if (GUILayout.Button("Remove", GUILayout.Width(70))) collabs.Remove(d); }
            else if (GUILayout.Button("Ready", GUILayout.Width(70))) MakeReady(d);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        GUILayout.Label($"Super combines: {(collabs.SuperUnlocked ? "unlocked" : "locked")}. \"Give\" combines both combos, then the super.");
        if (!collabs.SuperUnlocked && GUILayout.Button("Unlock super combines")) PlayerCollabs.UnlockSupers();
        foreach (PlayerCollabs.Def s in PlayerCollabs.Supers)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{s.name}\n<size=10>{PlayerCollabs.Recipe(s)}</size>", Rich());
            if (PlayerCollabs.Has(s.id)) { if (GUILayout.Button("Remove", GUILayout.Width(70))) collabs.Remove(s); }
            else if (collabs.SuperTaken == null && GUILayout.Button("Give", GUILayout.Width(70))) GiveSuper(s);
            GUILayout.EndHorizontal();
        }
    }

    private void GiveSuper(PlayerCollabs.Def super)
    {
        foreach (string comboId in new[] { super.a, super.b })
        {
            if (PlayerCollabs.Has(comboId)) continue;
            PlayerCollabs.Def combo = PlayerCollabs.Find(comboId);
            MakeReady(combo);
            collabs.Take(combo);
        }
        PlayerCollabs.UnlockSupers();
        collabs.Take(super);
    }

    private void MakeReady(PlayerCollabs.Def d)
    {
        if (d.kind == PlayerCollabs.Kind.Bomb)
            while (stats.projectileCount < PlayerCollabs.MaxBombsPerThrow) stats.IncreaseProjectileCount(1);
        else MaxWeapon(d.a);

        if (d.kind == PlayerCollabs.Kind.Merge) MaxWeapon(d.b);
        else MaxSkill(d.b);
    }

    private void MaxWeapon(string id)
    {
        PlayerWeapons.Def def = PlayerWeapons.Find(id);
        while (weapons.LevelOf(id) < Weapon.MaxLevel) weapons.LevelUp(def);
    }

    private void MaxSkill(string id)
    {
        PlayerSkills.Skill s = PlayerSkills.Find(id);
        while (skills.LevelOf(id) < s.levels.Length) skills.LevelUp(s);
    }

    private static GUIStyle rich;
    private static GUIStyle Rich() => rich ??= new GUIStyle(GUI.skin.label) { richText = true };
}
