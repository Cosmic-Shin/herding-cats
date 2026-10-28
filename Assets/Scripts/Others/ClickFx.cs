using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        canvas.sortingOrder = 1000;
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

        Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) * Vector2.up;
        float facing = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        StartCoroutine(Animate(Make(paw, Color.white), pos, 46f * scale, facing, 0.45f, pop: true));
        StartCoroutine(Animate(Make(Shapes.Ring, Pink), pos, 70f * scale, 0f, 0.3f, pop: false));
        Vector2 side = Vector2.Perpendicular(dir);
        for (int i = 0; i < 4; i++)
        {
            Vector2 at = pos + (dir * (44f + i * 34f) + side * (i % 2 == 0 ? 11f : -11f)) * scale;
            StartCoroutine(Step(0.08f + i * 0.08f, at, (32f - i * 3f) * scale, facing, i % 2 == 0 ? Pink : Cream));
        }
    }

    private IEnumerator Step(float delay, Vector2 at, float size, float angle, Color color)
    {
        yield return new WaitForSecondsRealtime(delay);
        StartCoroutine(Animate(Make(paw, color), at, size, angle, 0.55f, pop: true));
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

}
