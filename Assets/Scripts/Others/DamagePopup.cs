using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    private const float Duration = 0.6f;

    private TextMeshPro text;
    private Vector3 velocity;
    private float t;
    private float pop;
    private Color color;
    private bool crit;

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
        float size = (crit ? 5.2f : 4.2f) * Mathf.Clamp(0.85f + Mathf.Log10(Mathf.Max(1f, amount)) * 0.2f, 0.85f, 1.6f);
        string number = Mathf.RoundToInt(amount).ToString();
        DamagePopup popup = SpawnText(pos, crit ? number + "!" : number, crit ? new Color(1f, 0.85f, 0.2f) : SourceColor(DamageStats.Source), size);
        popup.countsTowardCap = true;
        if (!crit) return;
        popup.crit = true;
        popup.pop = 1.9f;
        popup.velocity.y += 1.2f;
        popup.color = Color.white; // the gradient carries the colour
        popup.text.enableVertexGradient = true; // gold on top, hot orange-red underneath
        popup.text.colorGradient = new VertexGradient(new Color(1f, 0.97f, 0.55f), new Color(1f, 0.97f, 0.55f), new Color(1f, 0.4f, 0.15f), new Color(1f, 0.4f, 0.15f));
        Juice.Sparks(pos + Vector2.up * 0.4f, new Color(1f, 0.85f, 0.3f), 3, 4f, 0.09f);
    }

    private static readonly Dictionary<string, Color> sourceColors = new Dictionary<string, Color>();
    public static Color SourceColor(string source)
    {
        if (string.IsNullOrEmpty(source)) return Color.white;
        if (sourceColors.TryGetValue(source, out Color c)) return c;
        bool Has(params string[] words) => System.Array.Exists(words, w => source.Contains(w));
        c = Has("Molotov", "Tar", "Firebomb") ? new Color(1f, 0.6f, 0.25f)
          : Has("Frost", "Glacier") ? new Color(0.6f, 0.9f, 1f)
          : Has("Lightning", "Thunder", "Tesla") ? new Color(1f, 0.95f, 0.45f)
          : Has("Glue", "Sticky") ? new Color(0.55f, 1f, 0.45f)
          : Has("Laser") ? new Color(1f, 0.55f, 0.65f)
          : Has("Black Hole", "Event Horizon", "Gravity") ? new Color(0.8f, 0.6f, 1f)
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
        popup.text.enableVertexGradient = false;
        popup.color = color;
        popup.text.color = Color.white; // flashes white, then settles to its colour
        popup.crit = false;
        popup.velocity = new Vector3(Random.Range(-1.1f, 1.1f), 3.4f, 0f);
        popup.pop = 1.4f;
        popup.t = 0f;
        popup.countsTowardCap = false;
        return popup;
    }

    private static DamagePopup Get()
    {
        while (free.Count > 0)
        {
            DamagePopup p = free.Pop();
            if (p == null) continue;
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
            sharedOutlineMaterial = popup.text.fontMaterial;
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
        float dt = MainMenu.IsOpen ? Time.unscaledDeltaTime : Time.deltaTime; // the menu runs with the clock stopped
        t += dt / Duration;
        transform.position += velocity * dt;
        velocity.y -= 9f * dt;
        velocity.x *= 1f - 2f * dt;

        text.color = t < 0.08f ? Color.white : color;
        text.alpha = t < 0.6f ? 1f : 1f - Mathf.Pow((t - 0.6f) / 0.4f, 2f);

        // Springs in from small with an overshoot, holds, then shrinks away.
        float grow = Mathf.Clamp01(t / 0.18f);
        float scale = Mathf.LerpUnclamped(0.3f, 1f, BackOut(grow)) * Mathf.Lerp(pop, 1f, grow)
                    * (t > 0.7f ? Mathf.Lerp(1f, 0.6f, (t - 0.7f) / 0.3f) : 1f);
        transform.localScale = Vector3.one * scale;

        float tilt = -velocity.x * 5f;
        if (crit && t < 0.25f) tilt += Mathf.Sin(t * 120f) * 9f * (1f - t / 0.25f); // crits shudder
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        if (t >= 1f) Release();
    }

    private static float BackOut(float x)
    {
        const float s = 2.2f;
        x -= 1f;
        return x * x * ((s + 1f) * x + s) + 1f;
    }
}
