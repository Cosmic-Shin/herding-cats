using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Lighting : MonoBehaviour
{
    public float ambient = 0.72f;
    public Color ambientColor = new Color(0.85f, 0.9f, 1f);
    public Color endlessColor = new Color(1f, 0.45f, 0.45f);
    public Color endlessSky = new Color(0.32f, 0.06f, 0.09f);
    public float playerLightRadius = 6f;
    public float playerLightIntensity = 0.4f;
    public Color playerLightColor = new Color(1f, 0.88f, 0.7f);
    public int maxFlashLights = 12;

    private static Lighting instance;
    private static Material unlit;
    private static Material UnlitMaterial => unlit != null ? unlit : unlit = Resources.Load<Material>("SpriteUnlit");

    public static void Glow(Renderer r)
    {
        if (UnlitMaterial != null) r.sharedMaterial = UnlitMaterial;
    }

    public static void Flash(Vector2 pos, float radius, Color color, float duration = 0.25f)
    {
        if (instance != null) instance.StartFlash(pos, radius, color, duration);
    }

    // A temporary colour wash over the whole scene (e.g. Golden Hour). Null = normal daylight. Endless mode overrides it.
    public static Color? Mood;

    private Light2D global, playerLight;
    private Camera cam;
    private Color daySky;
    private Light2D[] flashes;
    private float[] flashTime, flashLength, flashPeak;

    private void Awake()
    {
        instance = this;
        Mood = null;
    }

    public static void SetDynamic(bool on)
    {
        if (instance == null) return;
        if (instance.playerLight != null) instance.playerLight.enabled = on;
        if (!on && instance.flashes != null) foreach (Light2D l in instance.flashes) l.enabled = false;
    }

    [Tooltip("Ambient light multiplier reached at 15:00")]
    [Range(0.5f, 1f)] public float duskDim = 0.8f;
    private EnemySpawner spawner;

    private void Start()
    {
        cam = GetComponent<Camera>();
        spawner = FindFirstObjectByType<EnemySpawner>();
        gameObject.AddComponent<Ambience>();
        if (MapGenerator.Winter) ambientColor = new Color(0.8f, 0.88f, 1f); // cold daylight
        if (MapGenerator.Volcano) ambientColor = new Color(1f, 0.66f, 0.55f); // smoky red light
        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) global = l;
        if (global != null)
        {
            global.intensity = ambient;
            global.color = ambientColor;
        }
        if (cam != null)
        {
            daySky = cam.backgroundColor * ambientColor * ambient;
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
            flashPeak[i] = 1.2f * Settings.FlashScale;
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

        // Dusk: the world dims a little over the first 15 minutes (fireflies come out, see Ambience).
        if (global != null && spawner != null) global.intensity = Mathf.Lerp(ambient, ambient * duskDim, spawner.RunTime / 900f);

        if (!EnemySpawner.Endless)
        {
            if (global != null) global.color = Color.Lerp(global.color, Mood ?? ambientColor, Time.deltaTime * 2f);
            return;
        }
        float k = Time.deltaTime * 0.6f;
        if (global != null) global.color = Color.Lerp(global.color, endlessColor, k);
        if (cam != null) cam.backgroundColor = Color.Lerp(cam.backgroundColor, endlessSky, k);
    }
}
