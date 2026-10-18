using System.Collections.Generic;
using TMPro;
using UnityEngine;

// HoloCure-style floating damage number. Crits are bigger and yellow.
// Pooled internally (TextMeshPro objects are expensive to create), and all popups
// share one outlined material so they batch into a single draw.
public class DamagePopup : MonoBehaviour
{
    private const float Duration = 0.6f;

    private TextMeshPro text;
    private Vector3 velocity;
    private float t;
    private float pop; // starting scale; eases down to 1

    // Late game an explosion can hit dozens of cats at once; past this many live
    // numbers the screen is unreadable, so skip normal hits (crits still show).
    private const int MaxDamageNumbers = 40;
    private static int activeDamageNumbers;
    private bool countsTowardCap;

    private static readonly Stack<DamagePopup> free = new Stack<DamagePopup>();
    private static Material sharedOutlineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeDamageNumbers = 0;
        free.Clear();
        sharedOutlineMaterial = null;
    }

    public static void Prewarm(int count)
    {
        var made = new List<DamagePopup>(count);
        for (int i = 0; i < count; i++) made.Add(Get());
        foreach (DamagePopup p in made) p.Release();
    }

    public static void Spawn(Vector2 pos, float amount, bool crit)
    {
        if (!Settings.DamageNumbers) return;
        if (!crit && activeDamageNumbers >= MaxDamageNumbers) return;
        activeDamageNumbers++;
        // bigger hits get bigger numbers: ~1x at 10 damage, ~1.25x at 100, ~1.45x at 1000
        float size = (crit ? 5f : 3.5f) * Mathf.Clamp(0.85f + Mathf.Log10(Mathf.Max(1f, amount)) * 0.2f, 0.85f, 1.6f);
        DamagePopup popup = SpawnText(pos, Mathf.RoundToInt(amount).ToString(), crit ? new Color(1f, 0.85f, 0.2f) : SourceColor(DamageStats.Source), size);
        popup.countsTowardCap = true;
        if (crit) popup.pop = 2.2f; // crits slam in harder
    }

    // Non-crit numbers are tinted by what dealt them, so you can see which weapon does the work.
    private static readonly Dictionary<string, Color> sourceColors = new Dictionary<string, Color>();
    private static Color SourceColor(string source)
    {
        if (string.IsNullOrEmpty(source)) return Color.white;
        if (sourceColors.TryGetValue(source, out Color c)) return c;
        bool Has(params string[] words) => System.Array.Exists(words, w => source.Contains(w));
        c = Has("Molotov", "Tar", "Firebomb") ? new Color(1f, 0.6f, 0.25f)          // fire
          : Has("Frost", "Glacier") ? new Color(0.6f, 0.9f, 1f)                       // ice
          : Has("Lightning", "Thunder", "Tesla") ? new Color(1f, 0.95f, 0.45f)        // electric
          : Has("Glue", "Sticky") ? new Color(0.55f, 1f, 0.45f)                        // goo
          : Has("Laser") ? new Color(1f, 0.55f, 0.65f)
          : Has("Black Hole", "Event Horizon", "Gravity") ? new Color(0.8f, 0.6f, 1f) // void
          : Has("Aura") ? new Color(0.85f, 0.78f, 1f)
          : Color.white;
        return sourceColors[source] = c;
    }

    public static DamagePopup SpawnText(Vector2 pos, string message, Color color, float size)
    {
        DamagePopup popup = Get();
        popup.transform.position = pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.4f);
        popup.text.text = message;
        popup.text.fontSize = size;
        popup.text.color = color;
        popup.text.alpha = 1f;
        popup.velocity = new Vector3(Random.Range(-0.5f, 0.5f), 3f, 0f);
        popup.pop = 1.6f;
        popup.t = 0f;
        popup.countsTowardCap = false;
        return popup;
    }

    private static DamagePopup Get()
    {
        while (free.Count > 0)
        {
            DamagePopup p = free.Pop();
            if (p == null) continue; // destroyed with an old scene
            p.gameObject.SetActive(true);
            return p;
        }

        var go = new GameObject("DamagePopup");
        var popup = go.AddComponent<DamagePopup>();
        popup.text = go.AddComponent<TextMeshPro>();
        popup.text.fontStyle = FontStyles.Bold;
        popup.text.alignment = TextAlignmentOptions.Center;
        popup.text.sortingOrder = 100;
        if (sharedOutlineMaterial == null)
        {
            popup.text.outlineWidth = 0.25f;
            popup.text.outlineColor = Color.black;
            sharedOutlineMaterial = popup.text.fontMaterial; // first popup's outlined material becomes the shared one
        }
        else
        {
            popup.text.fontSharedMaterial = sharedOutlineMaterial;
        }
        return popup;
    }

    private void Release()
    {
        if (countsTowardCap) activeDamageNumbers--;
        countsTowardCap = false;
        gameObject.SetActive(false);
        free.Push(this);
    }

    private void Update()
    {
        t += Time.deltaTime / Duration;
        transform.position += velocity * Time.deltaTime;
        velocity.y -= 8f * Time.deltaTime; // little hop
        text.alpha = 1f - t * t;
        // pops in big, settles, and shrinks away at the end
        float scale = Mathf.Lerp(pop, 1f, Mathf.Clamp01(t / 0.2f)) * (t > 0.7f ? Mathf.Lerp(1f, 0.6f, (t - 0.7f) / 0.3f) : 1f);
        transform.localScale = Vector3.one * scale;
        if (t >= 1f) Release();
    }
}
