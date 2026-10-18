using UnityEngine;
using UnityEngine.Rendering.Universal;

// 2D lighting mood (the project already renders with URP's 2D Renderer):
//   - the world sits a little darker and cooler than full bright, Mira carries a warm light
//   - explosions flash real light on the ground and cats around them
//   - effects, projectiles and gems use an unlit material (Glow), so they shine against the dimmer world (and bloom)
//   - endless mode slowly turns everything blood red
// Sits on the Main Camera next to CameraFollow.
public class Lighting : MonoBehaviour
{
    public float ambient = 0.72f;
    public Color ambientColor = new Color(0.85f, 0.9f, 1f);
    public Color endlessColor = new Color(1f, 0.45f, 0.45f);
    public Color endlessSky = new Color(0.32f, 0.06f, 0.09f);
    public float playerLightRadius = 6f;
    public float playerLightIntensity = 0.4f;
    public Color playerLightColor = new Color(1f, 0.88f, 0.7f);
    public int maxFlashLights = 12; // ponytail: fixed pool, flashes past this are skipped; raise if big chains look dark

    private static Lighting instance;
    private static Material unlit;
    private static Material UnlitMaterial => unlit != null ? unlit : unlit = Resources.Load<Material>("SpriteUnlit");

    // For anything that should glow: effects, projectiles, gems.
    public static void Glow(Renderer r)
    {
        if (UnlitMaterial != null) r.sharedMaterial = UnlitMaterial;
    }

    public static void Flash(Vector2 pos, float radius, Color color, float duration = 0.25f)
    {
        if (instance != null) instance.StartFlash(pos, radius, color, duration);
    }

    private Light2D global, playerLight;
    private Camera cam;
    private Color daySky;
    private Light2D[] flashes;
    private float[] flashTime, flashLength, flashPeak;

    private void Awake() => instance = this;

    // Settings > Dynamic lights: off keeps the ambient mood but drops Mira's light and the explosion flashes.
    public static void SetDynamic(bool on)
    {
        if (instance == null) return;
        if (instance.playerLight != null) instance.playerLight.enabled = on;
        if (!on && instance.flashes != null) foreach (Light2D l in instance.flashes) l.enabled = false;
    }

    private void Start()
    {
        cam = GetComponent<Camera>();
        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) global = l;
        if (global != null)
        {
            global.intensity = ambient;
            global.color = ambientColor;
        }
        if (cam != null)
        {
            daySky = cam.backgroundColor * ambientColor * ambient; // the clear color isn't lit, so dim it to match
            daySky.a = 1f;
            cam.backgroundColor = daySky;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerLight = PointLight("PlayerLight", player.transform);
            playerLight.pointLightOuterRadius = playerLightRadius;
            playerLight.pointLightInnerRadius = 0.5f;
            playerLight.intensity = playerLightIntensity;
            playerLight.color = playerLightColor;
            playerLight.enabled = Settings.Lights;
        }

        flashes = new Light2D[maxFlashLights];
        flashTime = new float[maxFlashLights];
        flashLength = new float[maxFlashLights];
        flashPeak = new float[maxFlashLights];
        for (int i = 0; i < maxFlashLights; i++)
        {
            flashes[i] = PointLight("FlashLight", transform.parent);
            flashes[i].enabled = false;
        }
    }

    private static Light2D PointLight(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Light2D l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.falloffIntensity = 0.6f;
        return l;
    }

    private void StartFlash(Vector2 pos, float radius, Color color, float duration)
    {
        if (flashes == null || !Settings.Lights || Settings.FlashScale <= 0f) return;
        for (int i = 0; i < flashes.Length; i++)
        {
            if (flashes[i].enabled) continue;
            Light2D l = flashes[i];
            l.transform.position = pos;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.2f;
            l.color = color;
            flashTime[i] = flashLength[i] = duration;
            flashPeak[i] = 1.2f * Settings.FlashScale; // Settings > Screen flashes
            l.intensity = flashPeak[i];
            l.enabled = true;
            return;
        }
    }

    private void Update()
    {
        for (int i = 0; flashes != null && i < flashes.Length; i++)
        {
            if (!flashes[i].enabled) continue;
            flashTime[i] -= Time.deltaTime;
            if (flashTime[i] <= 0f) flashes[i].enabled = false;
            else flashes[i].intensity = flashPeak[i] * flashTime[i] / flashLength[i];
        }

        if (!EnemySpawner.Endless) return;
        float k = Time.deltaTime * 0.6f; // a couple of seconds to bleed into red
        if (global != null) global.color = Color.Lerp(global.color, endlessColor, k);
        if (cam != null) cam.backgroundColor = Color.Lerp(cam.backgroundColor, endlessSky, k);
    }
}
