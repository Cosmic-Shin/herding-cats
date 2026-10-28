using System.Collections;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverScreen : MonoBehaviour
{
    public float slowMoTime = 1.2f;

    private PlayerStats stats;
    private HUD hud;
    private GameObject screen;

    public static bool IsShowing { get; private set; }

    private void Awake()
    {
        EnemyHealth.ResetRunCounters();
        IsShowing = false;
    }

    private void Start()
    {
        stats = FindFirstObjectByType<PlayerStats>();
        hud = FindFirstObjectByType<HUD>();
        if (stats != null) stats.OnDeath += OnDeath;
    }

    private void OnDestroy()
    {
        if (stats != null) stats.OnDeath -= OnDeath;
    }

    private void OnDeath()
    {
        if (!IsShowing) StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        IsShowing = true;
        Juice.CancelHitStop();
        Time.timeScale = 0.3f;
        Juice.Flash(new Color(1f, 0.2f, 0.15f, 0.5f), 0.6f);
        if (stats != null) SceneFade.FocusOn(stats.transform.position, 0.75f, slowMoTime);
        yield return new WaitForSecondsRealtime(slowMoTime);
        Time.timeScale = 0f;

        if (Debug.isDebugBuild) Debug.Log(DamageStats.Report(Hero.Current.name, hud != null ? hud.RunTime : 0f));
        Leaderboard.Entry entry = Leaderboard.FromRun(stats, hud != null ? hud.RunTime : 0f);
        int rank = Leaderboard.Submit(entry);
        MainMenu.LastRank = rank;
        MainMenu menu = FindFirstObjectByType<MainMenu>();
        if (rank > 0 && menu != null) menu.ShowRanking(rank, () => Build(entry, rank));
        else Build(entry, rank);
    }

    private void Build(Leaderboard.Entry entry, int rank)
    {
        Canvas canvas = RuntimeUI.Canvas("GameOverCanvas", 200);
        screen = canvas.gameObject;

        Image dim = RuntimeUI.Box(canvas.transform, "Dim", Vector2.zero, Vector2.one, CatTheme.DimColor);
        RectTransform panel = CatTheme.Panel(dim.transform, "Panel", CatTheme.Style.Panel, new Vector2(0, -5), new Vector2(1000, 590)).rectTransform;
        Image cat = CatTheme.Cat(panel, "white", new Vector2(-150f, -6f), 64f, new Vector2(1f, 1f));
        cat.rectTransform.pivot = new Vector2(0.5f, 0f);

        RuntimeUI.Title(panel, "GAME OVER", 64f, new Vector2(0, 214), 900f);

        int best = rank > 0 ? 0 : Leaderboard.RankOf(entry.name);
        string place = rank > 0 ? $"<color=#7CFF9A>Rank #{rank} on the leaderboard!</color>"
                     : best > 0 ? $"<color=#968AB9>Not a new best. You're still #{best}</color>"
                     : "<color=#968AB9>Not in the top 50 this time</color>";
        CatTheme.Panel(panel, "ScorePlate", CatTheme.Style.PanelGold, new Vector2(0, 119), new Vector2(400, 64));
        TextMeshProUGUI score = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 34, Color.white, new Vector2(0, 119), new Vector2(400, 56)));
        TextMeshProUGUI placeText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 18, Color.white, new Vector2(0, 65), new Vector2(700, 28)));

        RectTransform left = CatTheme.Panel(panel, "RunInset", CatTheme.Style.Slot, new Vector2(-250, -90), new Vector2(420, 258)).rectTransform;
        Image face = CatTheme.Panel(left, "Portrait", CatTheme.Style.Slime, new Vector2(-160, 82), new Vector2(64, 64));
        face.gameObject.AddComponent<RectMask2D>().padding = Vector4.one * 6f;
        Image hero = CatTheme.Img(face.transform, "Face", Hero.Portrait(Hero.Current.id), new Vector2(0f, -20f + Hero.Current.portraitDrop * 147f / 32f), new Vector2(147f, 147f));
        hero.enabled = hero.sprite != null;
        RuntimeUI.Outline(RuntimeUI.Text(left, Hero.Current.name.ToUpper(), 30, CatTheme.Cream, new Vector2(-50, 94), new Vector2(140, 36), TextAlignmentOptions.Left));
        RuntimeUI.Outline(RuntimeUI.Text(left, MapGenerator.Names[Mathf.Clamp(MapGenerator.Biome, 0, MapGenerator.Names.Length - 1)], 18, CatTheme.Mint, new Vector2(-50, 64), new Vector2(140, 24), TextAlignmentOptions.Left));
        SummaryText(left, RunStats(), new Vector2(110, 76), new Vector2(180, 84), 14);
        SummaryText(left, Gear(), new Vector2(-98, -48), new Vector2(190, 150), 14);
        SummaryText(left, SkillList(), new Vector2(98, -48), new Vector2(190, 150), 14);

        RectTransform right = CatTheme.Panel(panel, "DamageInset", CatTheme.Style.Slot, new Vector2(225, -90), new Vector2(470, 258)).rectTransform;
        DamageChart(right, Vector2.zero, new Vector2(430, 226), hud != null ? hud.RunTime : 0f);

        Button restart = RuntimeUI.Button(panel, "Restart", new Vector2(-130, -259), new Vector2(200, 44), Restart);
        Button menu = RuntimeUI.Button(panel, "Main Menu", new Vector2(130, -259), new Vector2(200, 44), MainMenu.BackToMenu);
        CatTheme.Button(restart, 10f);
        CatTheme.Button(menu, 10f);
        StartCoroutine(Reveal(dim, panel, score, placeText, entry.score, place, new Component[] { left, right, placeText }, new[] { restart, menu }));
    }

    private static CanvasGroup Group(Component c)
    {
        CanvasGroup g = c.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        return g;
    }

    private IEnumerator Reveal(Image dim, RectTransform panel, TextMeshProUGUI score, TextMeshProUGUI placeText, int finalScore, string place, Component[] details, Button[] buttons)
    {
        CanvasGroup[] detailGroups = System.Array.ConvertAll(details, Group);
        CanvasGroup[] buttonGroups = System.Array.ConvertAll(buttons, b => Group(b));
        foreach (CanvasGroup g in buttonGroups) g.interactable = false;
        Color dimColor = dim.color;
        Vector2 home = panel.anchoredPosition;

        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.5f, back = k - 1f;
            dim.color = new Color(dimColor.r, dimColor.g, dimColor.b, dimColor.a * Mathf.Min(1f, k * 2f));
            panel.anchoredPosition = home + Vector2.up * 700f * (1f - (back * back * (3.2f * back + 2.2f) + 1f));
            yield return null;
        }
        dim.color = dimColor;
        panel.anchoredPosition = home;

        int shown = -1;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
        {
            int value = Mathf.RoundToInt(finalScore * (1f - Mathf.Pow(1f - t, 3f)));
            if (value != shown) { shown = value; score.text = $"<color=#FFE08A>SCORE  {value:N0}</color>"; }
            if (Time.frameCount % 3 == 0) Sfx.Play(Sfx.Id.Tick, 0.25f, 1f + t);
            yield return null;
        }
        score.text = $"<color=#FFE08A>SCORE  {finalScore:N0}</color>";
        placeText.text = place;
        Sfx.Play(Sfx.Id.Ready, 0.6f);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            score.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(t / 0.25f * Mathf.PI));
            yield return null;
        }
        score.transform.localScale = Vector3.one;

        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            foreach (CanvasGroup g in detailGroups) g.alpha = t / 0.35f;
            yield return null;
        }
        foreach (CanvasGroup g in detailGroups) g.alpha = 1f;
        foreach (CanvasGroup g in buttonGroups) { g.alpha = 1f; g.interactable = true; }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }

    public static RectTransform DamageChart(Transform parent, Vector2 pos, Vector2 size, float seconds)
    {
        var box = new GameObject("DamageChart", typeof(RectTransform)).GetComponent<RectTransform>();
        box.SetParent(parent, false);
        box.anchoredPosition = pos;
        box.sizeDelta = size;

        var rows = DamageStats.Top(10);
        float total = Mathf.Max(1f, DamageStats.Total);
        TextMeshProUGUI Label(string text, float x0, float x1, float y0, float y1, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = RuntimeUI.Text(box, text, 18, Color.white, Vector2.zero, Vector2.zero, align);
            t.rectTransform.anchorMin = new Vector2(x0, y0);
            t.rectTransform.anchorMax = new Vector2(x1, y1);
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.enableAutoSizing = true;
            t.fontSizeMin = 10;
            t.fontSizeMax = 20;
            return t;
        }

        float rowH = 1f / Mathf.Max(rows.Count + 1, 6);
        Label($"<b><color=#FFE08A>Damage Dealt</color></b>  <color=#9AA3C0>{DamageStats.Short(total)} total</color>", 0f, 1f, 1f - rowH, 1f, TextAlignmentOptions.Left);
        if (rows.Count == 0) { Label("<color=#5A6280>no damage dealt</color>", 0f, 1f, 1f - 2f * rowH, 1f - rowH, TextAlignmentOptions.Left); return box; }

        float best = rows[0].Value;
        for (int i = 0; i < rows.Count; i++)
        {
            string name = rows[i].Key;
            float value = rows[i].Value;
            float top = 1f - (i + 1) * rowH, bottom = top - rowH;
            string dps = seconds > 1f ? $"  {DamageStats.Short(value / seconds)}/s" : "";
            const float x0 = 0.08f;
            Sprite icon = SourceIcon(name);
            if (icon != null)
            {
                Image img = RuntimeUI.Box(box, "Icon", new Vector2(0f, bottom + rowH * 0.05f), new Vector2(x0 - 0.01f, top - rowH * 0.05f), Color.white);
                img.sprite = icon;
                img.preserveAspect = true;
            }
            Label(name, x0, 0.55f, bottom + rowH * 0.42f, top, TextAlignmentOptions.BottomLeft);
            Label($"{DamageStats.Short(value)}  <color=#9AA3C0>{value / total:P0}{dps}</color>", 0.4f, 1f, bottom + rowH * 0.42f, top, TextAlignmentOptions.BottomRight);
            RuntimeUI.Box(box, "Track", new Vector2(x0, bottom + rowH * 0.1f), new Vector2(1f, bottom + rowH * 0.38f), new Color(1f, 1f, 1f, 0.08f));
            RuntimeUI.Box(box, "Bar", new Vector2(x0, bottom + rowH * 0.1f), new Vector2(x0 + (1f - x0) * Mathf.Max(0.01f, value / best), bottom + rowH * 0.38f), BarColor(name));
        }
        return box;
    }

    private static Sprite SourceIcon(string source)
    {
        if (source == DamageStats.Bombs || source == "Bomb Pickup") return UiIcons.Icon("bomb");
        if (source == KaiSword.Source) return UiIcons.Icon("sword");
        if (source == NyxDrones.Source) return UiIcons.Icon("drone");
        PlayerWeapons.Def d = System.Array.Find(PlayerWeapons.All, w => w.name == source) ?? System.Array.Find(PlayerWeapons.CollabDefs, w => w.name == source);
        return d != null ? UiIcons.Icon(d.id) : null;
    }

    private static Color BarColor(string source)
    {
        Color c = DamagePopup.SourceColor(source);
        if (c != Color.white) return c;
        int hash = 0;
        foreach (char ch in source) hash = hash * 31 + ch;
        return Color.HSVToRGB(Mathf.Abs(hash % 360) / 360f, 0.5f, 1f);
    }

    private static void SummaryText(RectTransform parent, string text, Vector2 pos, Vector2 size, float maxSize)
    {
        TextMeshProUGUI t = RuntimeUI.Text(parent, text, maxSize, Color.white, pos, size, TextAlignmentOptions.TopLeft);
        t.enableAutoSizing = true;
        t.fontSizeMin = 9;
        t.fontSizeMax = maxSize;
    }

    private string RunStats()
    {
        string Row(string label, string value) => $"<color=#9AA3C0>{label}</color><pos=62%>{value}\n";
        int seconds = hud != null ? (int)hud.RunTime : 0;
        var sb = new StringBuilder();
        sb.Append(Row("Time", $"<color=#FFE08A>{seconds / 60:00}:{seconds % 60:00}</color>" + (EnemySpawner.Endless ? " <color=#FF7AD9>END</color>" : "")));
        sb.Append(Row("Level", stats.level.ToString()));
        sb.Append(Row("Cats", EnemyHealth.Kills.ToString("N0")));
        sb.Append(Row("Bosses", EnemyHealth.BossKills.ToString()));
        if (Companion.Instance != null) sb.Append(Row("Buddy", $"Lv {Companion.Instance.Level}"));
        return sb.ToString();
    }

    private string Gear()
    {
        var sb = new StringBuilder("<color=#FFB36B>Weapons</color>\n");
        PlayerWeapons weapons = stats.GetComponent<PlayerWeapons>();
        if (weapons == null || weapons.OwnedInOrder.Count == 0) sb.Append("<color=#5A6280>none</color>\n");
        else foreach (string id in weapons.OwnedInOrder) sb.Append($"{PlayerWeapons.Find(id).name} {WeaponTag(weapons, id)}\n");

        PlayerCollabs collabs = stats.GetComponent<PlayerCollabs>();
        if (collabs == null) return sb.ToString();
        var boosts = collabs.Taken.Where(id => weapons == null || !weapons.OwnedInOrder.Contains(id)).ToList();
        if (boosts.Count > 0)
        {
            sb.Append("\n<color=#FFD24A>Boosts</color>\n");
            foreach (string id in boosts) sb.Append(PlayerCollabs.Find(id).name + "\n");
        }
        return sb.ToString();
    }

    private string SkillList()
    {
        var sb = new StringBuilder("<color=#D7A6FF>Skills</color>\n");
        PlayerSkills skills = stats.GetComponent<PlayerSkills>();
        if (skills == null || skills.OwnedInOrder.Count == 0) sb.Append("<color=#5A6280>none</color>\n");
        else foreach (string id in skills.OwnedInOrder) sb.Append($"{PlayerSkills.Find(id).name} <color=#9FD8FF>Lv {skills.LevelOf(id)}</color>\n");
        return sb.ToString();
    }

    private static string WeaponTag(PlayerWeapons weapons, string id)
    {
        int enhance = weapons.Get(id) != null ? weapons.Get(id).Enhance : 0;
        string plus = enhance > 0 ? $" <color=#FFD24A>+{enhance}</color>" : "";
        return (PlayerWeapons.IsSuper(id) ? "<color=#FF7AD9>SUPER</color>" : PlayerWeapons.IsCollab(id) ? "<color=#FFD24A>COLLAB</color>" : $"<color=#9FD8FF>Lv {weapons.LevelOf(id)}</color>") + plus;
    }

    private static void Restart()
    {
        Time.timeScale = 1f;
        SceneFade.Reload();
    }
}
