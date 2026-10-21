using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Paw stamp, ring and sparkles on mouse click. Runs on unscaled time so it works in paused menus.
public class ClickFx : MonoBehaviour
{
    private static readonly Color Pink = new Color(1f, 0.55f, 0.68f);
    private static readonly Color Cream = new Color(1f, 0.95f, 0.85f);

    private RectTransform root;
    private Sprite paw;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("ClickFx");
        DontDestroyOnLoad(go);
        go.AddComponent<ClickFx>();
    }

    private void Awake()
    {
        var canvas = new GameObject("ClickFxCanvas").AddComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above every menu
        root = (RectTransform)canvas.transform;
        paw = Resources.Load<Sprite>("UI/paw");
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) Spawn(mouse.position.ReadValue());
    }

    private void Spawn(Vector2 pos)
    {
        float scale = Screen.height / 1080f;

        StartCoroutine(Animate(Make(paw, Color.white), pos, 46f * scale, Random.Range(-20f, 20f), 0.45f, pop: true));
        StartCoroutine(Animate(Make(Shapes.Ring, Pink), pos, 70f * scale, 0f, 0.3f, pop: false));
        for (int i = 0; i < 5; i++)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, i * 72f + Random.Range(-20f, 20f)) * Vector2.up;
            StartCoroutine(Fly(Make(Shapes.Plus, i % 2 == 0 ? Cream : Pink), pos, dir * Random.Range(90f, 150f) * scale, 12f * scale));
        }
    }

    private Image Make(Sprite sprite, Color color)
    {
        var img = new GameObject("Fx", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(root, false);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Pop: overshoot then settle (the stamp). Otherwise: grow outward (the ring). Both fade out.
    private IEnumerator Animate(Image img, Vector2 pos, float size, float angle, float duration, bool pop)
    {
        RectTransform rt = img.rectTransform;
        rt.position = pos;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        Color c = img.color;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            float s = pop ? (t < 0.25f ? Mathf.Lerp(0.5f, 1.15f, t / 0.25f) : Mathf.Lerp(1.15f, 1f, (t - 0.25f) / 0.75f))
                          : Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t));
            rt.sizeDelta = Vector2.one * size * s;
            img.color = new Color(c.r, c.g, c.b, c.a * (pop ? Mathf.Clamp01((1f - t) * 2.5f) : 1f - t));
            yield return null;
        }
        Destroy(img.gameObject);
    }

    private IEnumerator Fly(Image img, Vector2 pos, Vector2 velocity, float size)
    {
        RectTransform rt = img.rectTransform;
        Color c = img.color;
        const float duration = 0.35f;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            rt.position = pos + velocity * duration * (1f - (1f - t) * (1f - t));
            rt.sizeDelta = Vector2.one * size * (1f - t * 0.6f);
            img.color = new Color(c.r, c.g, c.b, 1f - t);
            yield return null;
        }
        Destroy(img.gameObject);
    }
}
