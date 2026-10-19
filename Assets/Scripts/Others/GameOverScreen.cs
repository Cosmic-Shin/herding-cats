using System.Collections;
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
        yield return new WaitForSecondsRealtime(slowMoTime);
        Time.timeScale = 0f;

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

        Image dim = RuntimeUI.Box(canvas.transform, "Dim", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, 0.8f));
        RectTransform panel = RuntimeUI.Frame(dim.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).rectTransform;
        panel.sizeDelta = new Vector2(1040, 600);

        RuntimeUI.Title(panel, "GAME OVER", 64f, new Vector2(0, 250), 900f);
        string place = rank > 0 ? $"<color=#7CFF9A>Rank #{rank} on the leaderboard!</color>" : "<color=#9AA3C0>Not in the top 50 this time</color>";
        RuntimeUI.Outline(RuntimeUI.Text(panel, $"<color=#FFE08A>SCORE  {entry.score:N0}</color>\n<size=60%>{place}</size>", 34, Color.white,
                                         new Vector2(0, 178), new Vector2(560, 80)));
        TextMeshProUGUI summary = RuntimeUI.Text(panel, Summary(), 22, Color.white, new Vector2(-235, -20), new Vector2(480, 300), TextAlignmentOptions.TopLeft);
        DamageChart(panel, new Vector2(250, -20), new Vector2(440, 320), hud != null ? hud.RunTime : 0f);
        summary.enableAutoSizing = true;
        summary.fontSizeMin = 12;
        summary.fontSizeMax = 22;

        Button restart = RuntimeUI.Button(panel, "Restart", new Vector2(-100, -245), new Vector2(180, 56), Restart);
        RuntimeUI.Button(panel, "Main Menu", new Vector2(100, -245), new Vector2(180, 56), MainMenu.BackToMenu);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(restart.gameObject);
    }

    // One row per damage source: name, total · share · DPS, and a bar sized against the top source.
    private static void DamageChart(Transform parent, Vector2 pos, Vector2 size, float seconds)
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

        float rowH = 1f / (rows.Count + 1);
        Label($"<b><color=#FFE08A>Damage Dealt</color></b>  <color=#9AA3C0>{DamageStats.Short(total)} total</color>", 0f, 1f, 1f - rowH, 1f, TextAlignmentOptions.Left);
        if (rows.Count == 0) { Label("<color=#5A6280>no damage dealt</color>", 0f, 1f, 1f - 2f * rowH, 1f - rowH, TextAlignmentOptions.Left); return; }

        float best = rows[0].Value;
        for (int i = 0; i < rows.Count; i++)
        {
            string name = rows[i].Key;
            float value = rows[i].Value;
            float top = 1f - (i + 1) * rowH, bottom = top - rowH;
            string dps = seconds > 1f ? $"  {DamageStats.Short(value / seconds)}/s" : "";
            const float x0 = 0.08f; // icon column
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
    }

    // Damage is recorded by display name; map it back to a weapon id for the icon.
    private static Sprite SourceIcon(string source)
    {
        if (source == DamageStats.Bombs || source == "Bomb Pickup") return UiIcons.Icon("bomb");
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

    private string Summary()
    {
        string Row(string label, string value) => $"<color=#9AA3C0>{label}</color><pos=48%>{value}\n";

        int seconds = hud != null ? (int)hud.RunTime : 0;
        var sb = new StringBuilder();
        sb.Append(Row("Time survived", $"<color=#FFE08A>{seconds / 60:00}:{seconds % 60:00}</color>" + (EnemySpawner.Endless ? "  <color=#FF7AD9>ENDLESS</color>" : "")));
        sb.Append(Row("Level", stats.level.ToString()));
        sb.Append(Row("Cats defeated", EnemyHealth.Kills.ToString()));
        sb.Append(Row("Bosses defeated", EnemyHealth.BossKills.ToString()));
        if (Companion.Instance != null) sb.Append(Row("Cat Buddy", $"Lv {Companion.Instance.Level}"));

        PlayerWeapons weapons = stats.GetComponent<PlayerWeapons>();
        sb.Append("\n<color=#FFB36B>Weapons</color>\n");
        if (weapons == null || weapons.OwnedInOrder.Count == 0) sb.Append("<color=#5A6280>none</color>\n");
        else foreach (string id in weapons.OwnedInOrder) sb.Append($"  {PlayerWeapons.Find(id).name} {WeaponTag(weapons, id)}\n");

        PlayerCollabs collabs = stats.GetComponent<PlayerCollabs>();
        if (collabs != null && collabs.Taken.Count > 0)
        {
            sb.Append("\n<color=#FFD24A>Collabs</color>\n");
            foreach (string id in collabs.Taken) sb.Append($"  {PlayerCollabs.Find(id).name}\n");
            if (collabs.SuperTaken != null) sb.Append($"  <color=#FF7AD9>SUPER: {PlayerCollabs.Find(collabs.SuperTaken).name}</color>\n");
        }

        PlayerSkills skills = stats.GetComponent<PlayerSkills>();
        sb.Append("\n<color=#D7A6FF>Skills</color>\n");
        if (skills == null || skills.OwnedInOrder.Count == 0) sb.Append("<color=#5A6280>none</color>\n");
        else foreach (string id in skills.OwnedInOrder) sb.Append($"  {PlayerSkills.Find(id).name} <color=#9FD8FF>Lv {skills.LevelOf(id)}</color>\n");

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
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
