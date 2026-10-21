using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class GamepadUI : MonoBehaviour
{
    public static bool Pressed(System.Func<Gamepad, ButtonControl> button) =>
        Gamepad.current != null && button(Gamepad.current).wasPressedThisFrame;

    public static bool Back => Pressed(g => g.buttonEast);
    public static bool Connected => Gamepad.current != null;

    public static Vector2 Move()
    {
        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 v = gp.leftStick.ReadValue();
            return v.sqrMagnitude > 0.04f ? v : gp.dpad.ReadValue();
        }
        Joystick js = Joystick.current;
        return js != null ? js.stick.ReadValue() : Vector2.zero;
    }

    private static bool AnyButton(Gamepad g) =>
        g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.buttonWest.wasPressedThisFrame ||
        g.buttonNorth.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.selectButton.wasPressedThisFrame ||
        g.leftShoulder.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("GamepadUI");
        DontDestroyOnLoad(go);
        go.AddComponent<GamepadUI>();
    }

    // Controller rumble. Strongest request wins until it runs out; stops on its own.
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

    private GameObject lastSelected;

    private void Update()
    {
        if (rumbleUntil > 0f && Time.unscaledTime >= rumbleUntil)
        {
            rumbleUntil = 0f;
            if (Gamepad.current != null) Gamepad.current.ResetHaptics();
        }

        // Hide the mouse cursor while playing on a pad; any mouse movement brings it back.
        Gamepad pad = Gamepad.current;
        if (pad != null && Cursor.visible && (Move().sqrMagnitude > 0.04f || AnyButton(pad))) Cursor.visible = false;
        else if (!Cursor.visible && Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 4f) Cursor.visible = true;

        EventSystem es = EventSystem.current;
        if (es == null) return;
        GameObject sel = es.currentSelectedGameObject;
        if (sel != lastSelected)
        {
            if (sel != null && lastSelected != null && sel.activeInHierarchy) Sfx.Play(Sfx.Id.Tick, 0.5f);
            lastSelected = sel;
        }
        if (Time.timeScale > 0f) return;
        if (sel != null && sel.activeInHierarchy && sel.TryGetComponent(out Selectable s) && s.IsInteractable()) return;
        bool poked = Move().sqrMagnitude > 0.25f || Pressed(g => g.buttonSouth) ||
                     (Joystick.current != null && Joystick.current.trigger.wasPressedThisFrame);
        if (!poked) return;

        Selectable best = null;
        int bestOrder = int.MinValue;
        foreach (Selectable c in Selectable.allSelectablesArray)
        {
            if (!c.IsInteractable() || !c.gameObject.activeInHierarchy) continue;
            Canvas canvas = c.GetComponentInParent<Canvas>();
            int order = canvas != null ? canvas.rootCanvas.sortingOrder : 0;
            Vector3 p = c.transform.position, b = best != null ? best.transform.position : Vector3.zero;
            if (order > bestOrder || (order == bestOrder && (p.y > b.y + 1f || (Mathf.Abs(p.y - b.y) <= 1f && p.x < b.x))))
            {
                best = c;
                bestOrder = order;
            }
        }
        if (best != null) es.SetSelectedGameObject(best.gameObject);
    }
}
