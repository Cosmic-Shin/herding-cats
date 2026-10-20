using UnityEngine;
using UnityEngine.InputSystem;

// Rebindable keyboard keys, saved in PlayerPrefs. Arrow keys always move and Esc always pauses,
// so a bad binding can never lock the player out.
public static class Keybinds
{
    public enum Action { Up, Down, Left, Right, Dash, Special, Combine, Pause }

    public static readonly string[] Names = { "Move up", "Move down", "Move left", "Move right", "Dash", "Special", "Combine", "Pause" };
    private static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.Space, Key.F, Key.C, Key.P };
    private static readonly Key[] keys = (Key[])Defaults.Clone();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        for (int i = 0; i < keys.Length; i++) keys[i] = (Key)PlayerPrefs.GetInt("key_" + (Action)i, (int)Defaults[i]);
    }

    public static Key Get(Action a) => keys[(int)a];

    // Assigning a key that another action uses swaps the two, so nothing ends up unbound.
    public static void Set(Action a, Key key)
    {
        for (int i = 0; i < keys.Length; i++)
            if (keys[i] == key && i != (int)a) { keys[i] = keys[(int)a]; Save((Action)i); }
        keys[(int)a] = key;
        Save(a);
    }

    public static void ResetAll()
    {
        Defaults.CopyTo(keys, 0);
        for (int i = 0; i < keys.Length; i++) Save((Action)i);
    }

    private static void Save(Action a)
    {
        PlayerPrefs.SetInt("key_" + a, (int)keys[(int)a]);
        PlayerPrefs.Save();
    }

    public static bool Held(Action a) => Keyboard.current != null && Keyboard.current[Get(a)].isPressed;
    public static bool Pressed(Action a) => Keyboard.current != null && Keyboard.current[Get(a)].wasPressedThisFrame;

    // A fixed extra key (e.g. Shift for dash) that works unless the player bound it to something else.
    public static bool AltPressed(Key key) =>
        Keyboard.current != null && System.Array.IndexOf(keys, key) < 0 && Keyboard.current[key].wasPressedThisFrame;

    public static string Label(Action a) => Label(Get(a));

    public static string Label(Key key)
    {
        string name = Keyboard.current != null ? Keyboard.current[key].displayName : null;
        return string.IsNullOrEmpty(name) ? key.ToString() : name.ToUpperInvariant();
    }
}
