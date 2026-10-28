using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFade : MonoBehaviour
{
    private const float OpenSize = 3.2f;
    private static SceneFade I;

    private Canvas canvas;
    private RectTransform paw;
    private readonly RectTransform[] sides = new RectTransform[4];
    private float size = OpenSize;
    private Vector2 center = new Vector2(0.5f, 0.5f);
    private Coroutine running;

    private static SceneFade Get()
    {
        if (I != null) return I;
        var go = new GameObject("SceneFade");
        DontDestroyOnLoad(go);
        I = go.AddComponent<SceneFade>();
        I.Build();
        return I;
    }

    public static void Reload() => Get().Run(Get().ReloadRoutine());

    public static void Close(System.Action then, float seconds = 0.35f) => Get().Run(Get().To(0f, seconds, new Vector2(0.5f, 0.5f), 1000, then));

    public static void Open(float seconds = 0.5f) => Get().Run(Get().To(OpenSize, seconds, new Vector2(0.5f, 0.5f), 1000, null));

    public static void FocusOn(Vector2 world, float holeSize, float seconds)
    {
        Camera cam = Camera.main;
        Vector2 at = cam != null ? (Vector2)cam.WorldToViewportPoint(world) : new Vector2(0.5f, 0.5f);
        Get().Run(Get().To(holeSize, seconds, at, 190, null));
    }

    private void Run(IEnumerator routine)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(routine);
    }

    private IEnumerator ReloadRoutine()
    {
        yield return To(0f, 0.35f, new Vector2(0.5f, 0.5f), 1000, null);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        yield return null;
        yield return null;
        yield return To(OpenSize, 0.5f, new Vector2(0.5f, 0.5f), 1000, null);
    }

    private IEnumerator To(float target, float seconds, Vector2 at, int order, System.Action then)
    {
        canvas.sortingOrder = order;
        canvas.enabled = true;
        float from = size;
        Vector2 fromAt = center;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            k = target < from ? k * k : 1f - (1f - k) * (1f - k);
            size = Mathf.Lerp(from, target, k);
            center = Vector2.Lerp(fromAt, at, k);
            Layout();
            yield return null;
        }
        size = target;
        center = at;
        Layout();
        if (size >= OpenSize) canvas.enabled = false;
        then?.Invoke();
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvas.enabled = false;
        gameObject.AddComponent<GraphicRaycaster>();

        paw = Box("Paw", PawSprite());
        for (int i = 0; i < 4; i++) sides[i] = Box("Side", null);
    }

    private RectTransform Box(string name, Sprite sprite)
    {
        var img = new GameObject(name).AddComponent<Image>();
        img.transform.SetParent(transform, false);
        img.sprite = sprite;
        img.color = Color.black;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        return rt;
    }

    private void Layout()
    {
        float sw = Screen.width, sh = Screen.height, d = Mathf.Max(1f, size * sh);
        float cx = center.x * sw, cy = center.y * sh;
        float x0 = cx - d / 2f, y0 = cy - d / 2f, x1 = x0 + d, y1 = y0 + d;
        Set(paw, x0, y0, d, d);
        Set(sides[0], 0f, 0f, Mathf.Max(0f, x0), sh);
        Set(sides[1], x1, 0f, Mathf.Max(0f, sw - x1), sh);
        Set(sides[2], x0, 0f, d, Mathf.Max(0f, y0));
        Set(sides[3], x0, y1, d, Mathf.Max(0f, sh - y1));
    }

    private static void Set(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Sprite PawSprite()
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2[] toes = { new Vector2(0.25f, 0.64f), new Vector2(0.41f, 0.77f), new Vector2(0.59f, 0.77f), new Vector2(0.75f, 0.64f) };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                Vector2 q = new Vector2((p.x - 0.5f) / 0.27f, (p.y - 0.38f) / 0.23f);
                float inside = 1f - q.magnitude;
                foreach (Vector2 t in toes) inside = Mathf.Max(inside, 1f - Vector2.Distance(p, t) / 0.105f);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inside * 18f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
}
