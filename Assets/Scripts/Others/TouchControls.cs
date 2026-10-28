using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// On-screen joystick and buttons for phones. Only exists on mobile (incl. mobile browsers and the Device Simulator).
[DefaultExecutionOrder(-1000)]
public class TouchControls : MonoBehaviour
{
    public static bool On => Application.isMobilePlatform;
    public static Vector2 Move { get; private set; }

    private static readonly int Count = System.Enum.GetValues(typeof(Keybinds.Action)).Length;
    private static readonly bool[] queued = new bool[Count], live = new bool[Count];

    // Taps arrive during EventSystem.Update; promoting them here (runs first) gives every reader one full frame to see them.
    public static bool Pressed(Keybinds.Action a) => live[(int)a];
    internal static void Queue(Keybinds.Action a) => queued[(int)a] = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (!On) return;
        var go = new GameObject("TouchControls");
        DontDestroyOnLoad(go);
        go.AddComponent<TouchControls>();
    }

    private const float Margin = 40f;
    private RectTransform root, stickBase, knob;
    private CanvasGroup group;
    private TouchButton dash, ult, use;
    private Image ultFill, dashShade;
    private PlayerController player;
    private PlayerUltimate ultimate;
    private Rect safe;

    private void Awake()
    {
        Canvas canvas = RuntimeUI.Canvas("TouchCanvas", 140);
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        root = RuntimeUI.Box(canvas.transform, "Safe", Vector2.zero, Vector2.one, Color.clear).rectTransform;
        root.GetComponent<Image>().raycastTarget = false;

        Image zone = RuntimeUI.Box(root, "StickZone", Vector2.zero, new Vector2(0.45f, 0.6f), Color.clear);
        stickBase = CatTheme.Panel(root, "Stick", CatTheme.Style.Slot, Vector2.zero, new Vector2(240, 240)).rectTransform;
        stickBase.anchorMin = stickBase.anchorMax = Vector2.zero;
        knob = CatTheme.Panel(stickBase, "Knob", CatTheme.Style.Pink, Vector2.zero, new Vector2(108, 108)).rectTransform;
        CatTheme.Ears(knob, "ear_gold");
        zone.gameObject.AddComponent<TouchStick>().Init(this);
        RestStick();

        dash = MakeButton("DASH", CatTheme.Style.Slime, Keybinds.Action.Dash, new Vector2(-Margin - 20, Margin + 20), 200);
        dashShade = RuntimeUI.Box(dash.transform, "Cooldown", Vector2.zero, Vector2.one, new Color(0.05f, 0.03f, 0.1f, 0.6f));
        dashShade.raycastTarget = false;
        dashShade.transform.SetSiblingIndex(0);

        ult = MakeButton("ULT", CatTheme.Style.Pink, Keybinds.Action.Special, new Vector2(-Margin - 250, Margin + 20), 150);
        ultFill = RuntimeUI.Box(ult.transform, "Charge", Vector2.zero, Vector2.one, Color.white);
        ultFill.raycastTarget = false;
        ultFill.sprite = ExplosionFx.GetCircle();
        ultFill.type = Image.Type.Filled;
        ultFill.fillMethod = Image.FillMethod.Radial360;
        ultFill.fillOrigin = (int)Image.Origin360.Top;
        ultFill.rectTransform.offsetMin = Vector2.one * 14f;
        ultFill.rectTransform.offsetMax = -Vector2.one * 14f;
        ultFill.transform.SetSiblingIndex(0);

        use = MakeButton("USE", CatTheme.Style.PanelGold, Keybinds.Action.Interact, new Vector2(-Margin - 50, Margin + 250), 140);
        CatTheme.Ears((RectTransform)use.transform, "ear_gold");

        TouchButton pause = MakeButton("II", CatTheme.Style.Panel, Keybinds.Action.Pause, Vector2.zero, 84);
        var prt = (RectTransform)pause.transform;
        prt.anchorMin = prt.anchorMax = prt.pivot = Vector2.one;
        prt.anchoredPosition = new Vector2(-24, -150);
    }

    private TouchButton MakeButton(string label, CatTheme.Style style, Keybinds.Action action, Vector2 pos, float size)
    {
        Image img = CatTheme.Panel(root, label + "Button", style, Vector2.zero, new Vector2(size, size));
        img.raycastTarget = true;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = pos;
        TextMeshProUGUI t = RuntimeUI.Outline(RuntimeUI.Text(rt, label, size * 0.24f, CatTheme.Cream, Vector2.zero, new Vector2(size, size)), 0.25f);
        t.fontStyle = FontStyles.Bold;
        var b = img.gameObject.AddComponent<TouchButton>();
        b.action = action;
        return b;
    }

    internal void StickDown(Vector2 local) { stickBase.anchoredPosition = local; knob.anchoredPosition = Vector2.zero; Move = Vector2.zero; }

    internal void StickDrag(Vector2 local)
    {
        const float radius = 90f;
        Vector2 d = Vector2.ClampMagnitude(local - stickBase.anchoredPosition, radius);
        knob.anchoredPosition = d;
        Move = d.magnitude < radius * 0.2f ? Vector2.zero : d / radius;
    }

    internal void RestStick()
    {
        stickBase.anchoredPosition = new Vector2(Margin + 170, Margin + 170);
        knob.anchoredPosition = Vector2.zero;
        Move = Vector2.zero;
    }

    internal RectTransform Root => root;

    private void Update()
    {
        for (int i = 0; i < Count; i++) { live[i] = queued[i]; queued[i] = false; }

        if (Screen.safeArea != safe)
        {
            safe = Screen.safeArea;
            root.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        }

        bool playing = MainMenu.Started && !MainMenu.IsOpen && Time.timeScale > 0f && !GameOverScreen.IsShowing && !CombineMenu.IsOpen && !RewardMenu.IsOpen;
        if (playing && player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            ultimate = player != null ? player.GetComponent<PlayerUltimate>() : null;
        }
        playing &= player != null;
        group.alpha = playing ? 1f : 0f;
        group.blocksRaycasts = playing;
        if (!playing) { RestStick(); return; }

        stickBase.GetComponent<Image>().color = new Color(1f, 1f, 1f, Move == Vector2.zero ? 0.55f : 0.9f);
        dashShade.rectTransform.anchorMax = new Vector2(1f, player.CooldownTotal > 0f ? Mathf.Clamp01(player.CooldownRemaining / player.CooldownTotal) : 0f);

        if (ultimate != null)
        {
            bool ready = ultimate.IsReady;
            ultFill.fillAmount = ultimate.IsActive ? ultimate.ActiveRemaining / ultimate.duration
                               : ready ? 1f : 1f - ultimate.CooldownRemaining / ultimate.cooldown;
            ultFill.color = ultimate.IsActive ? new Color(1f, 0.55f, 0.15f, 0.8f) : ready ? new Color(1f, 0.82f, 0.3f, 0.8f) : new Color(0.45f, 0.5f, 0.65f, 0.6f);
            ult.transform.localScale = Vector3.one * (ready ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 6f) : 1f);
            ult.GetComponent<Image>().sprite = CatTheme.Get(ready ? CatTheme.Style.SlotGold : CatTheme.Style.Pink);
        }

        use.gameObject.SetActive(Pickup.NearFrame >= Time.frameCount - 2);
    }
}

public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Keybinds.Action action;

    public void OnPointerDown(PointerEventData e)
    {
        TouchControls.Queue(action);
        transform.localScale = new Vector3(1.08f, 0.9f, 1f);
    }

    public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
}

public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private TouchControls owner;
    private int finger = int.MinValue;

    public void Init(TouchControls c) => owner = c;

    private Vector2 Local(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.Root, e.position, null, out Vector2 p);
        return p + owner.Root.rect.size * owner.Root.pivot;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (finger != int.MinValue) return;
        finger = e.pointerId;
        owner.StickDown(Local(e));
    }

    public void OnDrag(PointerEventData e) { if (e.pointerId == finger) owner.StickDrag(Local(e)); }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != finger) return;
        finger = int.MinValue;
        owner.RestStick();
    }

    private void OnDisable() => finger = int.MinValue;
}
