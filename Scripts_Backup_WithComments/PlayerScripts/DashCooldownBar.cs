using UnityEngine;

// Thin bar under the player that fills while the dash/blink recharges; hidden when ready.
[RequireComponent(typeof(PlayerController))]
public class DashCooldownBar : MonoBehaviour
{
    public Vector2 offset = new Vector2(0f, -0.75f);
    public Vector2 size = new Vector2(0.8f, 0.08f);

    private PlayerController controller;
    private Transform bg, fill;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        Sprite pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f); // pivot left so it fills rightward

        bg = MakeBar("DashBarBg", pixel, new Color(0f, 0f, 0f, 0.6f), 11);
        fill = MakeBar("DashBarFill", pixel, new Color(0.5f, 0.85f, 1f), 12);
    }

    private Transform MakeBar(string name, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go.transform;
    }

    private void LateUpdate()
    {
        bool charging = controller.CooldownRemaining > 0f;
        bg.gameObject.SetActive(charging);
        fill.gameObject.SetActive(charging);
        if (!charging) return;

        float progress = 1f - controller.CooldownRemaining / Mathf.Max(0.01f, controller.CooldownTotal);
        Vector3 left = new Vector3(offset.x - size.x / 2f, offset.y, 0f);
        bg.localPosition = left;
        bg.localScale = new Vector3(size.x, size.y, 1f);
        fill.localPosition = left;
        fill.localScale = new Vector3(size.x * progress, size.y * 0.6f, 1f);
    }
}
