using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// On death: a short slow-motion beat, then a run summary (score, rank, time, level, kills, build)
// with Restart / Main Menu. The run is saved to the Leaderboard here. The game is endless, so death is the only ending.
// Builds its own UI at runtime, so it only needs to sit on any object in the scene.
public class GameOverScreen : MonoBehaviour
{
    public float slowMoTime = 1.2f; // real seconds of slow motion before the screen appears

    private PlayerStats stats;
    private HUD hud;
    private GameObject screen;

    public static bool IsShowing { get; private set; }

    private void Awake()
    {
        EnemyHealth.ResetRunCounters(); // statics survive a scene reload (Restart)
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
        IsShowing = true; // blocks pause / level-up during the slow-mo beat
        Juice.CancelHitStop();
        Time.timeScale = 0.3f;
        Juice.Flash(new Color(1f, 0.2f, 0.15f, 0.5f), 0.6f);
        yield return new WaitForSecondsRealtime(slowMoTime);
        Time.timeScale = 0f;

        Leaderboard.Entry entry = Leaderboard.FromRun(stats, hud != null ? hud.RunTime : 0f);
        int rank = Leaderboard.Submit(entry);
        MainMenu.LastRank = rank;
        MainMenu menu = FindFirstObjectByType<MainMenu>();
        if (rank > 0 && menu != null) menu.ShowRanking(rank, () => Build(entry, rank)); // made the board: show where first
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
        TextMeshProUGUI damage = RuntimeUI.Text(panel, DamageStats.Report().TrimStart(), 22, Color.white, new Vector2(250, -20), new Vector2(440, 300), TextAlignmentOptions.TopLeft);
        damage.enableAutoSizing = true;
        damage.fontSizeMin = 12;
        damage.fontSizeMax = 22;
        summary.enableAutoSizing = true;
        summary.fontSizeMin = 12;
        summary.fontSizeMax = 22;

        Button restart = RuntimeUI.Button(panel, "Restart", new Vector2(-100, -245), new Vector2(180, 56), Restart);
        RuntimeUI.Button(panel, "Main Menu", new Vector2(100, -245), new Vector2(180, 56), MainMenu.BackToMenu);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(restart.gameObject);
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
