using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class GamepadUI : MonoBehaviour
{
    public static bool Pressed(System.Func<Gamepad, ButtonControl> button) =>
        Gamepad.current != null && button(Gamepad.current).wasPressedThisFrame;

    public static bool Back => Pressed(g => g.buttonEast);
    public static bool Connected => Gamepad.current != null;

    private static int navXFrame = -1, navX, stickXWas;
    public static int NavX
    {
        get
        {
            if (navXFrame == Time.frameCount) return navX;
            navXFrame = Time.frameCount;
            Keyboard k = Keyboard.current;
            int key = k == null ? 0
                : k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame ? -1
                : k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame ? 1 : 0;
            float x = Move().x;
            int stick = x < -0.5f ? -1 : x > 0.5f ? 1 : 0;
            navX = key != 0 ? key : stick != stickXWas ? stick : 0;
            stickXWas = stick;
            return navX;
        }
    }

    public static Vector2 Move()
    {
        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 v = gp.leftStick.ReadValue();
            return v.sqrMagnitude > 0.04f ? v : gp.dpad.ReadValue();
        }
        Joystick js = Joystick.current;
        if (js == null) return Vector2.zero;
        Vector2 s = js.stick.ReadValue();
        return s.sqrMagnitude > 0.04f || js.hatswitch == null ? s : js.hatswitch.ReadValue();
    }

    private static bool AnyButton(Gamepad g) =>
        g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.buttonWest.wasPressedThisFrame ||
        g.buttonNorth.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.selectButton.wasPressedThisFrame ||
        g.leftShoulder.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame;

    private static bool KeyPoke()
    {
        Keyboard k = Keyboard.current;
        return k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                             k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame ||
                             k.wKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame ||
                             k.dKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame ||
                             k.tabKey.wasPressedThisFrame);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("GamepadUI");
        DontDestroyOnLoad(go);
        go.AddComponent<GamepadUI>();
    }

    private static float rumbleUntil, rumbleLow, rumbleHigh;
    public static void Rumble(float low, float high, float seconds)
    {
        if (Gamepad.current == null) return;
        if (Time.unscaledTime < rumbleUntil && low + high < rumbleLow + rumbleHigh) return;
        rumbleLow = low;
        rumbleHigh = high;
        rumbleUntil = Time.unscaledTime + seconds;
        Gamepad.current.SetMotorSpeeds(low, high);
    }

    private void OnApplicationFocus(bool focus) { if (!focus && Gamepad.current != null) Gamepad.current.ResetHaptics(); }
    private void OnDestroy() { if (Gamepad.current != null) Gamepad.current.ResetHaptics(); }

    private GameObject lastSelected, lastGood;

    private static bool Usable(GameObject g) =>
        g != null && g.activeInHierarchy && g.TryGetComponent(out Selectable s) && s.IsInteractable();

    private static int Order(Component c)
    {
        Canvas canvas = c.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.sortingOrder : 0;
    }

    private void Update()
    {
        if (rumbleUntil > 0f && Time.unscaledTime >= rumbleUntil)
        {
            rumbleUntil = 0f;
            if (Gamepad.current != null) Gamepad.current.ResetHaptics();
        }

        Gamepad pad = Gamepad.current;
        if (pad != null && Cursor.visible && (Move().sqrMagnitude > 0.04f || AnyButton(pad))) Cursor.visible = false;
        else if (!Cursor.visible && Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 4f) Cursor.visible = true;

        EventSystem es = EventSystem.current;
        if (es == null) { UpdateFrame(null); return; }
        if (es.currentInputModule is InputSystemUIInputModule ui && ui.deselectOnBackgroundClick)
        {
            ui.deselectOnBackgroundClick = false;
            ui.moveRepeatDelay = 0.35f;
            ui.moveRepeatRate = 0.09f;
        }

        GameObject sel = es.currentSelectedGameObject;
        if (sel != null && sel != lastGood && Time.timeScale == 0f && Usable(lastGood) && Order(sel.transform) < Order(lastGood.transform))
        {
            es.SetSelectedGameObject(lastGood);
            sel = lastGood;
        }
        if (sel != lastSelected)
        {
            if (sel != null && lastSelected != null && sel.activeInHierarchy) Sfx.Play(Sfx.Id.Tick, 0.5f);
            lastSelected = sel;
        }
        bool ok = Usable(sel);
        if (!ok && Time.timeScale == 0f && !Settings.Busy && lastGood != null && lastGood.activeInHierarchy && !Usable(lastGood))
        {
            Selectable near = Nearest(lastGood);
            if (near != null) { es.SetSelectedGameObject(near.gameObject); sel = lastSelected = near.gameObject; ok = true; }
        }
        if (ok) lastGood = sel;
        UpdateFrame(ok && Time.timeScale == 0f ? sel : null);

        if (ok && Time.timeScale == 0f && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame &&
            (sel.TryGetComponent(out Button _) || sel.TryGetComponent(out Toggle _)))
            ExecuteEvents.Execute(sel, new BaseEventData(es), ExecuteEvents.submitHandler);

        if (Time.timeScale > 0f || ok || Settings.Busy) return;
        bool poked = Move().sqrMagnitude > 0.25f || Pressed(g => g.buttonSouth) || KeyPoke() ||
                     (Joystick.current != null && Joystick.current.trigger.wasPressedThisFrame);
        if (!poked) return;

        Selectable best = null;
        int bestOrder = int.MinValue;
        foreach (Selectable c in Selectable.allSelectablesArray)
        {
            if (!c.IsInteractable() || !c.gameObject.activeInHierarchy || c.navigation.mode == Navigation.Mode.None) continue;
            int order = Order(c);
            Vector3 p = c.transform.position, b = best != null ? best.transform.position : Vector3.zero;
            if (order > bestOrder || (order == bestOrder && (p.y > b.y + 1f || (Mathf.Abs(p.y - b.y) <= 1f && p.x < b.x))))
            {
                best = c;
                bestOrder = order;
            }
        }
        if (Usable(lastGood) && Order(lastGood.transform) >= bestOrder) es.SetSelectedGameObject(lastGood);
        else if (best != null) es.SetSelectedGameObject(best.gameObject);
    }

    private static Selectable Nearest(GameObject from)
    {
        Selectable best = null;
        float bestDist = float.MaxValue;
        int order = Order(from.transform);
        foreach (Selectable c in Selectable.allSelectablesArray)
        {
            if (c.gameObject == from || !c.IsInteractable() || !c.gameObject.activeInHierarchy || c.navigation.mode == Navigation.Mode.None) continue;
            if (Order(c) != order) continue;
            float d = (c.transform.position - from.transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    private RectTransform arrow;
    private bool arrowOn;

    private static Sprite ArrowSprite()
    {
        const int w = 7, h = 11;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        bool Fill(int x, int y) => x >= 1 && x <= 5 && Mathf.Abs(y - 5) <= 5 - x;
        Color gold = new(1f, 0.82f, 0.3f), dark = new(0.05f, 0.03f, 0.1f), clear = new(0, 0, 0, 0);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool edge = false;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) edge |= Fill(x + dx, y + dy);
                tex.SetPixel(x, y, Fill(x, y) ? gold : edge ? dark : clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void UpdateFrame(GameObject target)
    {
        if (arrow == null)
        {
            var canvasGo = new GameObject("SelectArrow", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            Canvas c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 30000;
            Image img = new GameObject("Arrow", typeof(RectTransform)).AddComponent<Image>();
            img.sprite = ArrowSprite();
            img.raycastTarget = false;
            arrow = img.rectTransform;
            arrow.SetParent(canvasGo.transform, false);
            arrow.anchorMin = arrow.anchorMax = Vector2.zero;
            arrow.pivot = new Vector2(1f, 0.5f);
        }

        bool show = target != null && target.transform is RectTransform;
        arrow.gameObject.SetActive(show);
        if (!show) { arrowOn = false; return; }

        RectTransform rt = (RectTransform)target.transform;
        Rect local = rt.rect;
        if (target.TryGetComponent(out TMPro.TextMeshProUGUI tmp) && tmp.textBounds.size.x > 1f)
            local = new Rect(tmp.textBounds.min, tmp.textBounds.size);
        Canvas owner = target.GetComponentInParent<Canvas>();
        Camera cam = owner != null && owner.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? owner.rootCanvas.worldCamera : null;
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(local.min));
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(local.max));

        float scale = Screen.height / 1080f;
        Vector2 pos = new(Mathf.Min(a.x, b.x) - 18f * scale, (a.y + b.y) * 0.5f);
        float k = arrowOn ? 1f - Mathf.Exp(-Time.unscaledDeltaTime * 20f) : 1f;
        Vector2 slid = Vector2.Lerp(arrow.anchoredPosition + Vector2.right * bob, pos, k);
        bob = -Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 8f * scale;
        arrow.anchoredPosition = slid - Vector2.right * bob;
        arrow.sizeDelta = new Vector2(7f, 11f) * 3.5f * scale;
        arrowOn = true;
    }

    private float bob;
}
