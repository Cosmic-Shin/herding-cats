using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Records which monsters the player has ever defeated (one PlayerPrefs flag per kind, written once).
public static class Bestiary
{
    private static readonly HashSet<string> recorded = new HashSet<string>();

    public static string Key(GameObject go) => go.TryGetComponent(out BossAI b) ? b.bossName : go.name.Replace("(Clone)", "").Trim();
    public static bool Defeated(string key) => recorded.Contains(key) || PlayerPrefs.GetInt("beast_" + key, 0) == 1;

    public static void Record(GameObject go)
    {
        string key = Key(go);
        if (!recorded.Add(key) || PlayerPrefs.GetInt("beast_" + key, 0) == 1) return;
        PlayerPrefs.SetInt("beast_" + key, 1);
        PlayerPrefs.Save();
    }
}

// Main menu wiki: monsters (unlocked by defeating them), weapons, skills, combos and supers.
public partial class MainMenu
{
    private const int WikiCols = 6, WikiRows = 5, WikiPerPage = WikiCols * WikiRows;
    private static readonly string[] WikiTabs = { "Monsters", "Weapons", "Skills", "Combos", "Supers" };

    private class WikiEntry { public Sprite sprite; public Color tint = Color.white; public string iconId, name, sub, body; public bool hidden, dim; }

    private GameObject wikiPage;
    private Button wikiBack, wikiPrev, wikiNext;
    private Button[] wikiTabButtons;
    private HUD.Slot[] wikiCells;
    private Button[] wikiCellButtons;
    private Image wikiIcon;
    private TextMeshProUGUI wikiName, wikiSub, wikiBody, wikiPageText;
    private List<WikiEntry> wikiEntries = new List<WikiEntry>();
    private int wikiTab, wikiPageIndex;
    private GameObject wikiShown;

    private void BuildWiki(Transform parent)
    {
        wikiPage = Page(parent, "Wiki");
        RectTransform panel = Container(wikiPage.transform, new Vector2(1200, 680));
        BuildTitle(panel, "WIKI", 60f, 292f);
        CatTheme.Panel(panel, "Panel", CatTheme.Style.Panel, Vector2.zero, new Vector2(1180, 550));

        wikiTabButtons = new Button[WikiTabs.Length];
        for (int i = 0; i < WikiTabs.Length; i++)
        {
            int tab = i;
            wikiTabButtons[i] = TextButton(panel, WikiTabs[i], 232f, () => SetWikiTab(tab), -440f + i * 220f, 26f);
        }

        // grid on the left (slot positions are from the panel's top-left corner)
        wikiCells = new HUD.Slot[WikiPerPage];
        wikiCellButtons = new Button[WikiPerPage];
        for (int i = 0; i < WikiPerPage; i++)
        {
            HUD.Slot s = HUD.MakeSlot(panel, new Vector2(50 + (i % WikiCols) * 82, -150 - (i / WikiCols) * 82), 72, themed: true);
            s.icon.preserveAspect = true;
            Button b = s.frame.gameObject.AddComponent<Button>();
            b.targetGraphic = s.frame;
            ColorBlock c = b.colors;
            c.highlightedColor = c.selectedColor = new Color(1f, 0.85f, 0.4f);
            b.colors = c;
            b.onClick.AddListener(() => Select(b.gameObject));
            wikiCells[i] = s;
            wikiCellButtons[i] = b;
        }
        wikiPrev = TextButton(panel, "< Prev", -245f, () => SetWikiPage(wikiPageIndex - 1), -500f, 24f);
        wikiPageText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 22, ItemColor, new Vector2(-310f, -245f), new Vector2(200, 40)));
        wikiNext = TextButton(panel, "Next >", -245f, () => SetWikiPage(wikiPageIndex + 1), -120f, 24f);

        // details on the right
        wikiIcon = RuntimeUI.Box(panel, "WikiIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
        wikiIcon.rectTransform.anchoredPosition = new Vector2(40f, 135f);
        wikiIcon.rectTransform.sizeDelta = new Vector2(130f, 130f);
        wikiIcon.preserveAspect = true;
        wikiIcon.raycastTarget = false;
        wikiName = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 34, CatTheme.GoldHi, new Vector2(345f, 165f), new Vector2(440, 50), TextAlignmentOptions.MidlineLeft));
        wikiSub = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 18, CatTheme.Faint, new Vector2(345f, 110f), new Vector2(440, 60), TextAlignmentOptions.TopLeft));
        wikiBody = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 20, Color.white, new Vector2(270f, -95f), new Vector2(590, 300), TextAlignmentOptions.TopLeft));
        wikiBody.enableAutoSizing = true;
        wikiBody.fontSizeMin = 13;
        wikiBody.fontSizeMax = 20;

        wikiBack = TextButton(panel, "Back", -305, ShowMain, 0, 28);
        Width(wikiBack, 170f);
        CatTheme.Button(wikiBack, 10f);
        wikiPage.SetActive(false);
    }

    private void ShowWiki()
    {
        mainPage.SetActive(false);
        wikiPage.SetActive(true);
        SetWikiTab(wikiTab);
    }

    private void UpdateWiki(bool back)
    {
        if (back) { ShowMain(); return; }
        if (GamepadUI.Pressed(g => g.leftShoulder)) SetWikiTab((wikiTab + WikiTabs.Length - 1) % WikiTabs.Length);
        else if (GamepadUI.Pressed(g => g.rightShoulder)) SetWikiTab((wikiTab + 1) % WikiTabs.Length);

        GameObject sel = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        if (sel == wikiShown) return;
        int i = System.Array.FindIndex(wikiCellButtons, b => b.gameObject == sel);
        if (i < 0) return;
        wikiShown = sel;
        for (int k = 0; k < wikiCellButtons.Length; k++) wikiCellButtons[k].transform.localScale = Vector3.one * (k == i ? 1.15f : 1f);
        int index = wikiPageIndex * WikiPerPage + i;
        if (index < wikiEntries.Count) ShowWikiEntry(wikiEntries[index]);
    }

    private void SetWikiTab(int tab)
    {
        wikiTab = tab;
        for (int i = 0; i < wikiTabButtons.Length; i++)
            ((TextMeshProUGUI)wikiTabButtons[i].targetGraphic).text = i == tab ? $"<color=#FFD24A>[{WikiTabs[i]}]</color>" : WikiTabs[i];
        wikiEntries = tab switch
        {
            0 => MonsterEntries(),
            1 => WeaponEntries(),
            2 => SkillEntries(),
            3 => ComboEntries(PlayerCollabs.All),
            _ => ComboEntries(PlayerCollabs.Supers),
        };
        SetWikiPage(0);
    }

    private void SetWikiPage(int p)
    {
        int pages = Mathf.Max(1, (wikiEntries.Count + WikiPerPage - 1) / WikiPerPage);
        wikiPageIndex = Mathf.Clamp(p, 0, pages - 1);
        wikiPageText.text = $"Page {wikiPageIndex + 1} / {pages}";
        SetEnabled(wikiPrev, wikiPageIndex > 0);
        SetEnabled(wikiNext, wikiPageIndex < pages - 1);

        for (int i = 0; i < WikiPerPage; i++)
        {
            int index = wikiPageIndex * WikiPerPage + i;
            HUD.Slot s = wikiCells[i];
            bool on = index < wikiEntries.Count;
            s.frame.gameObject.SetActive(on);
            if (!on) continue;
            WikiEntry e = wikiEntries[index];
            HUD.Fill(s, null, "", gold: false); // resets the slot and keeps its button clickable (Clear disables it)
            s.icon.sprite = EntrySprite(e);
            s.icon.enabled = s.icon.sprite != null;
            s.icon.color = e.hidden ? new Color(0f, 0f, 0f, 0.85f) : new Color(e.tint.r, e.tint.g, e.tint.b, e.dim ? 0.4f : 1f);
            s.level.text = e.hidden ? "?" : "";
        }
        wikiShown = null;
        if (wikiEntries.Count > 0) { Select(wikiCellButtons[0].gameObject); ShowWikiEntry(wikiEntries[wikiPageIndex * WikiPerPage]); }
        else ShowWikiEntry(new WikiEntry { name = "", sub = "", body = "Nothing here yet." });
    }

    private static Sprite EntrySprite(WikiEntry e) => e.iconId != null ? UiIcons.Icon(e.iconId) : e.sprite;

    private void ShowWikiEntry(WikiEntry e)
    {
        wikiIcon.sprite = EntrySprite(e);
        wikiIcon.enabled = wikiIcon.sprite != null;
        wikiIcon.color = e.hidden ? new Color(0f, 0f, 0f, 0.85f) : new Color(e.tint.r, e.tint.g, e.tint.b, e.dim ? 0.5f : 1f);
        wikiName.text = e.hidden ? "???" : e.name;
        wikiSub.text = e.sub;
        wikiBody.text = e.hidden ? "<color=#9AA3C0>Defeat it in a run to unlock this entry.</color>" : e.body;
    }

    // ---------- Monsters ----------

    private static readonly Dictionary<string, (string name, string text)> MonsterInfo = new Dictionary<string, (string, string)>
    {
        { "Cat_brown", ("Brown Cat", "The most common cat. Walks straight at you.") },
        { "Cat_white", ("White Cat", "Fragile, but shows up early and in numbers.") },
        { "Cat_blue", ("Blue Cat", "A bit tougher than the brown ones.") },
        { "Cat_rainbow", ("Rainbow Cat", "Keeps its distance and shoots at you.") },
        { "Cat_black", ("Black Cat", "Slow, but takes a lot of hits.") },
        { "Cat_demon", ("Demon Cat", "Hits hard and leaps at you.") },
        { "SplitterCat", ("Splitter Cat", "Splits into smaller cats when defeated.") },
        { "ToxicSpitter", ("Toxic Spitter", "Spits globs that leave poison puddles on the ground.") },
        { "ShieldCat", ("Shield Cat", "Blocks attacks from the front. Hit it from the side or behind.") },
        { "KamikazeCat", ("Kamikaze Cat", "Runs up to you and explodes after a short fuse.") },
        { "ChargerCat", ("Charger Cat", "Lines up, then charges in a straight line.") },
        { "GemThief", ("Gem Thief", "Grabs XP gems off the ground and runs away from you.") },
        { "HealerCat", ("Healer Cat", "Heals nearby cats every few seconds. Take it out first.") },
        { "SniperCat", ("Sniper Cat", "Aims carefully, then fires long-range shots.") },
        { "MamaCat", ("Mama Cat", "Keeps having litters of kittens until defeated.") },
        { "Mouse", ("Mouse", "Tiny, fast and fragile. Rushes you in packs.") },
        { "Bee", ("Bee", "Fast and fragile. The Queen Bee sends whole lines of them.") },
        { "SlimeBunny", ("Slime Bunny", "Hops quickly toward you. The Big Rabbit drops them as it gets hurt.") },
        { "Penguin", ("Penguin", "Waddles at you in quick groups.") },
        { "SnowGolem", ("Snow Golem", "Slow and sturdy. The Yeti calls them in.") },
        { "Salamander", ("Salamander", "A quick little fire lizard.") },
        { "MagmaSlug", ("Magma Slug", "Slow and tanky. The Magma Slug King calls them in.") },
        { "FatCat", ("Fat Cat", "Mini-boss. Leaps up and belly-flops onto you.") },
        { "HairballLauncher", ("Hairball Launcher", "Mini-boss. Lobs hairballs that burst into smaller ones.") },
        { "Big Black Cat", ("Big Black Cat", "A huge shadow cat. Touching it or getting swiped curses you with bad luck.") },
        { "Big Rabbit", ("Big Rabbit", "Hops, slams and burrows under you. Drops slime bunnies as it gets hurt.") },
        { "Queen Bee", ("Queen Bee", "Rules the meadow with her bees. Gets angrier below 40% health.") },
        { "Emperor Penguin", ("Emperor Penguin", "Slides across the ice and calls down icicles.") },
        { "Yeti", ("Yeti", "Throws snowballs, raises ice spikes and starts avalanches.") },
        { "Big Salamander", ("Big Salamander", "Breathes fire and leaves a trail of lava.") },
        { "Magma Slug King", ("Magma Slug King", "Leaves lava everywhere it goes and makes the ground erupt.") },
    };

    private static readonly Dictionary<BossAI.Special, string> MoveNames = new Dictionary<BossAI.Special, string>
    {
        { BossAI.Special.Spiral, "Bullet Spiral" }, { BossAI.Special.Summon, "Summon Minions" }, { BossAI.Special.Laser, "Eye Laser" },
        { BossAI.Special.Carrots, "Carrot Barrage" }, { BossAI.Special.BeeSwarm, "Bee Line" }, { BossAI.Special.Honey, "Honey Puddles" },
        { BossAI.Special.Pollen, "Pollen Cloud" },
    };

    private static readonly string[] MapNames = { "Meadow", "Winter", "Volcano" };

    private static string Clock(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    private static string SplitWords(string s) => System.Text.RegularExpressions.Regex.Replace(s.Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");

    private List<WikiEntry> MonsterEntries()
    {
        var list = new List<WikiEntry>();
        var keys = new HashSet<string>();
        var spawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        var bosses = FindFirstObjectByType<BossTimeline>(FindObjectsInactive.Include);
        if (spawner != null)
        {
            var waves = new List<EnemySpawner.EnemyWave>(spawner.enemyWaves);
            waves.Sort((a, b) => a.timeToUnlock.CompareTo(b.timeToUnlock));
            foreach (var w in waves) AddMonster(list, keys, w.enemyPrefab, null, "Enemy", w.map, w.timeToUnlock, 1f, BossAI.Special.None);
            AddMonster(list, keys, spawner.mousePrefab, null, "Enemy", -1, spawner.firstMousePackTime, 1f, BossAI.Special.None);
            foreach (var m in spawner.miniBosses) AddMonster(list, keys, m.prefab, null, "Mini-boss", m.map, m.time, 1f, BossAI.Special.None);
        }
        if (bosses != null)
        {
            var events = new List<BossTimeline.BossEvent>(bosses.events);
            events.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.map.CompareTo(b.map));
            foreach (var e in events)
            {
                // an every-map boss replaced on all three maps never shows up
                bool replaced = e.map < 0 && System.Array.TrueForAll(new[] { 0, 1, 2 }, m => bosses.events.Exists(o => o.map == m && Mathf.Approximately(o.time, e.time)));
                if (!replaced) AddMonster(list, keys, e.prefab, e.titleOverride, "Boss", e.map, e.time, e.healthMultiplier * bosses.bossHealth, e.specials);
            }
        }
        return list;
    }

    private static void AddMonster(List<WikiEntry> list, HashSet<string> keys, GameObject prefab, string title, string kind,
                                   int map, float time, float hpScale, BossAI.Special moves)
    {
        if (prefab == null) return;
        bool boss = prefab.TryGetComponent(out BossAI ai);
        string key = !string.IsNullOrEmpty(title) ? title : boss ? ai.bossName : prefab.name;
        if (!keys.Add(key)) return;

        MonsterInfo.TryGetValue(key, out var info);
        prefab.TryGetComponent(out SpriteRenderer r);
        Sprite sprite = boss && ai.idle.down.Length > 0 ? ai.idle.down[0] : r != null ? r.sprite : null;
        Color tint = r != null ? r.color : Color.white;
        // Same look as in battle: special cats share the white frames and get their recolor + accessory at runtime.
        if (!boss && prefab.TryGetComponent(out EnemyAnimator anim))
        {
            if (anim.idle != null && anim.idle.Length > 0) sprite = anim.idle[0];
            sprite = SpriteFx.Skinned(sprite, anim.skin);
        }
        float hp = prefab.TryGetComponent(out EnemyHealth h) ? h.maxHealth * hpScale : 0f;
        bool walker = prefab.TryGetComponent(out EnemyAI e);
        float damage = boss ? ai.contactDamage : walker ? e.contactDamage : 0f;
        float speed = boss ? ai.moveSpeed : walker ? e.moveSpeed : 0f;

        string body = (info.text ?? "") + $"\n\n<color=#FFB36B>Health</color> {hp:N0}    <color=#FFB36B>Contact damage</color> {damage:0}    <color=#FFB36B>Speed</color> {speed:0.#}";
        if (moves != BossAI.Special.None)
        {
            body += "\n\n<color=#D7A6FF>Signature attacks</color>";
            foreach (BossAI.Special flag in System.Enum.GetValues(typeof(BossAI.Special)))
                if (flag != BossAI.Special.None && (moves & flag) == flag)
                    body += "\n- " + (MoveNames.TryGetValue(flag, out string n) ? n : SplitWords(flag.ToString()));
        }
        list.Add(new WikiEntry
        {
            sprite = sprite,
            tint = tint,
            name = info.name ?? (string.IsNullOrEmpty(title) ? SplitWords(key) : title),
            sub = $"{kind}  -  {(map < 0 ? "every map" : MapNames[map])}  -  {(kind == "Boss" ? "at " : "from ")}{Clock(time)}",
            body = body,
            hidden = !Bestiary.Defeated(key),
        });
    }

    // ---------- Weapons, skills, combos, supers ----------

    private static string Unlock(string reward)
    {
        if (!Achievements.IsLocked(reward)) return null;
        if (Shop.WeaponLocked(reward)) return $"<color=#FF8A7A>Locked</color> - buy it in the Shop for <color=#7FE3FF>{Shop.WeaponFor(reward).cost:N0} fish coins</color>";
        Achievements.Def a = System.Array.Find(Achievements.All, d => d.reward == reward);
        return a != null ? $"<color=#FF8A7A>Locked</color> - earn \"{a.name}\": {a.goal}" : "<color=#FF8A7A>Locked</color>";
    }

    private static string Levels(string[] levels)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < levels.Length; i++)
            sb.Append($"<color=#FFD24A>{(i == levels.Length - 1 ? "MAX" : "Lv " + (i + 1))}</color>  {levels[i]}\n");
        return sb.ToString();
    }

    private static string UsedIn(string pieceId)
    {
        var names = new List<string>();
        foreach (PlayerCollabs.Def c in PlayerCollabs.All) if (c.a == pieceId || c.b == pieceId) names.Add(c.name);
        return names.Count == 0 ? "" : "\n<color=#D7A6FF>Combos</color>  " + string.Join(", ", names);
    }

    private static List<WikiEntry> WeaponEntries()
    {
        var list = new List<WikiEntry>();
        foreach (PlayerWeapons.Def d in PlayerWeapons.All)
        {
            string locked = Unlock("weapon:" + d.id);
            list.Add(new WikiEntry { iconId = d.id, name = d.name, sub = locked ?? "Weapon", body = Levels(d.levels) + UsedIn(d.id), dim = locked != null });
        }
        return list;
    }

    private static List<WikiEntry> SkillEntries()
    {
        var list = new List<WikiEntry>();
        foreach (PlayerSkills.Skill s in PlayerSkills.All)
        {
            string locked = Unlock("skill:" + s.id);
            list.Add(new WikiEntry { iconId = "skill:" + s.id, name = s.name, sub = locked ?? "Skill", body = Levels(s.levels) + UsedIn(s.id), dim = locked != null });
        }
        return list;
    }

    private static string PieceName(string id, bool skill, Hero.Def h) =>
        id == null || id == "bombs" ? h.maxPiece.Replace("Max ", "")
        : skill ? Hero.SkillName(h, id)
        : PlayerWeapons.Find(id)?.name ?? (PlayerCollabs.Find(id) != null ? Hero.ComboText(h, id).name : id);

    // Every combo for every hero, not just the equipped one: hero-specific combos get one entry per hero.
    private static List<WikiEntry> ComboEntries(PlayerCollabs.Def[] defs)
    {
        var list = new List<WikiEntry>();
        foreach (PlayerCollabs.Def d in defs)
        {
            bool perHero = System.Array.Exists(Hero.All, h => h.combos != null && h.combos.ContainsKey(d.id));
            if (!perHero) list.Add(ComboEntry(d, Hero.Current, false));
            else foreach (Hero.Def h in Hero.All) list.Add(ComboEntry(d, h, true));
        }
        return list;
    }

    private static WikiEntry ComboEntry(PlayerCollabs.Def d, Hero.Def h, bool perHero)
    {
        bool super = d.kind == PlayerCollabs.Kind.Super;
        var (name, text) = Hero.ComboText(h, d.id);
        string first = PieceName(PlayerCollabs.FirstPiece(d), false, h);
        string second = super ? PieceName(d.b, false, h) : PieceName(d.b, PlayerCollabs.SecondIsSkill(d), h);
        string kind = super ? "Super combine" : d.kind == PlayerCollabs.Kind.Merge ? "Weapon + weapon combo"
                    : d.kind == PlayerCollabs.Kind.Boost ? "Weapon + skill combo" : h.upgradeKind + " combo";
        if (perHero) kind += $"  -  <color=#FFD24A>{h.name}</color>";
        string recipe = super ? $"Combine <color=#FFD24A>{first}</color> + <color=#FFD24A>{second}</color>.\nSuper combines unlock after the Elder Beholder falls."
                              : $"Max <color=#FFD24A>{first}</color> and <color=#FFD24A>{second}</color>, then combine.";
        if (!super)
        {
            var supers = System.Array.FindAll(PlayerCollabs.Supers, s => s.a == d.id || s.b == d.id);
            if (supers.Length > 0) recipe += "\n<color=#FF7AD9>Super</color>  " + string.Join(", ", System.Array.ConvertAll(supers, s => Hero.ComboText(h, s.id).name));
        }
        var entry = new WikiEntry { name = name, sub = kind, body = text + "\n\n" + recipe };
        if (perHero) entry.sprite = UiIcons.ComboIcon(d, h);
        else entry.iconId = d.id;
        return entry;
    }
}
