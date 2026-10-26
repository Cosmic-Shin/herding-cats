using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Rebindable keyboard keys and controller buttons, saved in PlayerPrefs. Arrow keys / the stick always move and
// Esc / Start always pause, so a bad binding can never lock the player out.
public static class Keybinds
{
    public enum Action { Up, Down, Left, Right, Dash, Special, Combine, Pause, Interact }

    // Action.Special is the hero's ultimate; it's labelled that way so players can find it in Controls.
    public static readonly string[] Names = { "Move up", "Move down", "Move left", "Move right", "Dash", "Ultimate", "Combine", "Pause", "Open chest/anvil" };
    private static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.Space, Key.F, Key.C, Key.P, Key.E };
    private static readonly Key[] keys = (Key[])Defaults.Clone();

    // Movement stays on the stick/d-pad and pause on Start; only these can move on a pad.
    private static readonly GamepadButton[] PadDefaults =
    {
        GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
        GamepadButton.RightShoulder, GamepadButton.North, GamepadButton.Select, GamepadButton.Start, GamepadButton.West,
    };
    private static readonly GamepadButton[] pad = (GamepadButton[])PadDefaults.Clone();

    // Buttons a pad action may be bound to (not the d-pad, which moves, or Start, which pauses).
    public static readonly GamepadButton[] PadChoices =
    {
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        GamepadButton.LeftShoulder, GamepadButton.RightShoulder, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
        GamepadButton.LeftStick, GamepadButton.RightStick, GamepadButton.Select,
    };

    public static bool PadRebindable(Action a) => a == Action.Dash || a == Action.Special || a == Action.Combine || a == Action.Interact;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        for (int i = 0; i < keys.Length; i++) keys[i] = (Key)PlayerPrefs.GetInt("key_" + (Action)i, (int)Defaults[i]);
        for (int i = 0; i < pad.Length; i++)
            if (PadRebindable((Action)i)) pad[i] = (GamepadButton)PlayerPrefs.GetInt("pad_" + (Action)i, (int)PadDefaults[i]);
    }

    public static Key Get(Action a) => keys[(int)a];
    public static GamepadButton GetPad(Action a) => pad[(int)a];

    // What to show in on-screen prompts: the controller button when a pad is plugged in, else the key.
    public static string Prompt(Action a) =>
        Gamepad.current != null ? PadLabel(GetPad(a))
        : Joystick.current != null && JoystickButton(a) is string b ? (b == "trigger" ? "Btn 1" : "Btn " + b.Substring(6))
        : Label(a);

    // Assigning a key that another action uses swaps the two, so nothing ends up unbound.
    public static void Set(Action a, Key key)
    {
        for (int i = 0; i < keys.Length; i++)
            if (keys[i] == key && i != (int)a) { keys[i] = keys[(int)a]; Save((Action)i); }
        keys[(int)a] = key;
        Save(a);
    }

    public static void SetPad(Action a, GamepadButton b)
    {
        for (int i = 0; i < pad.Length; i++)
            if (pad[i] == b && i != (int)a) { pad[i] = pad[(int)a]; Save((Action)i); }
        pad[(int)a] = b;
        Save(a);
    }

    public static void ResetAll()
    {
        Defaults.CopyTo(keys, 0);
        PadDefaults.CopyTo(pad, 0);
        for (int i = 0; i < keys.Length; i++) Save((Action)i);
    }

    private static void Save(Action a)
    {
        PlayerPrefs.SetInt("key_" + a, (int)keys[(int)a]);
        if (PadRebindable(a)) PlayerPrefs.SetInt("pad_" + a, (int)pad[(int)a]);
        PlayerPrefs.Save();
    }

    public static bool Held(Action a) => Keyboard.current != null && Keyboard.current[Get(a)].isPressed;
    public static bool Pressed(Action a) => Keyboard.current != null && Keyboard.current[Get(a)].wasPressedThisFrame;
    public static bool PadPressed(Action a) =>
        Gamepad.current != null ? Gamepad.current[GetPad(a)].wasPressedThisFrame
        : JoystickButton(a) is string b && Joystick.current?.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>(b) is { } c && c.wasPressedThisFrame;

    // Generic joysticks have no named face buttons, only "trigger", "button2", "button3"...
    private static string JoystickButton(Action a) => a switch
    {
        Action.Dash => "trigger", Action.Special => "button2", Action.Combine => "button3", Action.Interact => "button4", _ => null,
    };

    // A fixed extra key (e.g. Shift for dash) that works unless the player bound it to something else.
    public static bool AltPressed(Key key) =>
        Keyboard.current != null && System.Array.IndexOf(keys, key) < 0 && Keyboard.current[key].wasPressedThisFrame;

    public static string Label(Action a) => Label(Get(a));

    public static string Label(Key key)
    {
        string name = Keyboard.current != null ? Keyboard.current[key].displayName : null;
        return string.IsNullOrEmpty(name) ? key.ToString() : name.ToUpperInvariant();
    }

    // Button names for the connected pad's family. Unity maps buttons by position, so South is A on Xbox,
    // Cross on PlayStation and B on Switch.
    private enum Family { Xbox, PlayStation, Switch }

    private static Family PadFamily
    {
        get
        {
            Gamepad g = Gamepad.current;
            if (g is UnityEngine.InputSystem.DualShock.DualShockGamepad) return Family.PlayStation;
            if (g != null && g.layout.Contains("Switch")) return Family.Switch;
            return Family.Xbox;
        }
    }

    public static string PadLabel(GamepadButton b)
    {
        int f = (int)PadFamily;
        string[] n = b switch
        {
            GamepadButton.South => new[] { "A", "Cross", "B" },
            GamepadButton.East => new[] { "B", "Circle", "A" },
            GamepadButton.West => new[] { "X", "Square", "Y" },
            GamepadButton.North => new[] { "Y", "Triangle", "X" },
            GamepadButton.LeftShoulder => new[] { "LB", "L1", "L" },
            GamepadButton.RightShoulder => new[] { "RB", "R1", "R" },
            GamepadButton.LeftTrigger => new[] { "LT", "L2", "ZL" },
            GamepadButton.RightTrigger => new[] { "RT", "R2", "ZR" },
            GamepadButton.LeftStick => new[] { "LS", "L3", "LS" },
            GamepadButton.RightStick => new[] { "RS", "R3", "RS" },
            GamepadButton.Select => new[] { "View", "Share", "-" },
            GamepadButton.Start => new[] { "Menu", "Options", "+" },
            _ => new[] { "D-pad", "D-pad", "D-pad" },
        };
        return n[f];
    }
}
