using UnityEngine;

public class GroundFx : MonoBehaviour
{
    private const int Max = 48;
    private static GroundFx instance;
    private static Sprite printSprite;

    private readonly SpriteRenderer[] prints = new SpriteRenderer[Max];
    private readonly float[] life = new float[Max], age = new float[Max], alpha = new float[Max];
    private int next;

    public static void Step(Vector2 feet, Vector2 facing, bool left)
    {
        if (MapGenerator.OnLava(feet)) return;
        if (instance == null) instance = new GameObject("GroundFx").AddComponent<GroundFx>();
        Vector2 dir = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector2.down;
        Vector2 at = feet + Vector2.Perpendicular(dir) * (left ? 0.07f : -0.07f);

        if (MapGenerator.Winter)
        {
            instance.Print(at, dir, new Color(0.5f, 0.58f, 0.75f), 0.6f, 6f);
            Juice.Sparks(feet, Color.white, 2, 1.2f, 0.06f);
        }
        else if (MapGenerator.Volcano)
        {
            instance.Print(at, dir, new Color(0.12f, 0.1f, 0.1f), 0.5f, 3.5f);
            Juice.Sparks(feet, new Color(0.45f, 0.4f, 0.38f), 2, 1.4f, 0.09f);
        }
        else
        {
            instance.Print(at, dir, new Color(0.22f, 0.38f, 0.14f), 0.3f, 1.5f);
            Juice.SparksDir(feet, Vector2.up, 55f, new Color(0.45f, 0.75f, 0.3f), 2, 1.8f, 0.07f);
        }
    }

    private void Print(Vector2 at, Vector2 dir, Color color, float a, float seconds)
    {
        int i = next;
        next = (next + 1) % Max;
        if (prints[i] == null)
        {
            prints[i] = new GameObject("Print").AddComponent<SpriteRenderer>();
            prints[i].transform.SetParent(transform, false);
            prints[i].sprite = PrintSprite;
            prints[i].sortingOrder = -95;
        }
        SpriteRenderer sr = prints[i];
        sr.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f));
        sr.color = new Color(color.r, color.g, color.b, a);
        sr.gameObject.SetActive(true);
        life[i] = seconds;
        age[i] = 0f;
        alpha[i] = a;
    }

    private void Update()
    {
        for (int i = 0; i < Max; i++)
        {
            SpriteRenderer sr = prints[i];
            if (sr == null || !sr.gameObject.activeSelf) continue;
            age[i] += Time.deltaTime;
            float k = age[i] / life[i];
            if (k >= 1f) { sr.gameObject.SetActive(false); continue; }
            Color c = sr.color;
            c.a = alpha[i] * (1f - k * k);
            sr.color = c;
        }
    }

    private static Sprite PrintSprite
    {
        get
        {
            if (printSprite != null) return printSprite;
            string[] rows = { ".#.", "###", "###", ".#." };
            var tex = new Texture2D(3, 4) { filterMode = FilterMode.Point };
            for (int y = 0; y < 4; y++) for (int x = 0; x < 3; x++) tex.SetPixel(x, 3 - y, rows[y][x] == '#' ? Color.white : Color.clear);
            tex.Apply();
            return printSprite = Sprite.Create(tex, new Rect(0, 0, 3, 4), new Vector2(0.5f, 0.5f), 20f);
        }
    }
}
