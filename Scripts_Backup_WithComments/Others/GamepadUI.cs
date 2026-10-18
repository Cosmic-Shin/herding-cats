using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

// Controller helpers. Menu navigation / A to click / B to cancel come from the EventSystem's UI input module;
// this fills the gap where nothing is selected (e.g. after a mouse click on the background), so a stick or
// d-pad press while a menu is up (game paused) picks the top-left button of the front-most menu.
public class GamepadUI : MonoBehaviour
{
    public static bool Pressed(System.Func<Gamepad, ButtonControl> button) =>
        Gamepad.current != null && button(Gamepad.current).wasPressedThisFrame;

    public static bool Back => Pressed(g => g.buttonEast);
    public static bool Connected => Gamepad.current != null;

    // Stick / d-pad direction from a gamepad, or a generic joystick (flight sticks, old USB pads).
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("GamepadUI");
        DontDestroyOnLoad(go);
        go.AddComponent<GamepadUI>();
    }

    private GameObject lastSelected;

    private void Update()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;
        GameObject sel = es.currentSelectedGameObject;
        if (sel != lastSelected) // the highlight moved: a soft tick (not when a menu just cleared it)
        {
            if (sel != null && lastSelected != null && sel.activeInHierarchy) Sfx.Play(Sfx.Id.Tick, 0.5f);
            lastSelected = sel;
        }
        if (Time.timeScale > 0f) return; // only while a menu holds the game; A is dash in play
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
