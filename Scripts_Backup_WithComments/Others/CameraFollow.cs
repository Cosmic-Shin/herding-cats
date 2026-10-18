using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Attach to the Main Camera (NOT parented to the player). Smoothly follows
// the player, keeps Z fixed, and supports a small screen shake.
// Also: leads a little in the direction you move, zooms out as the screen fills with cats,
// and sets up post-processing (bloom, vignette, menu dimming, ultimate tint; untick postProcessing to turn it off).
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // auto-found by "Player" tag if left empty

    [Header("Follow Settings")]
    public float smoothTime = 0.1f;
    public Vector2 offset = Vector2.zero;

    [Header("Feel")]
    public float leadDistance = 1.2f;     // how far ahead of a moving player the camera looks
    public float crowdZoom = 1.2f;        // extra orthographic size when the screen is packed
    public int crowdForFullZoom = 180;    // cats alive for the full zoom-out
    public bool postProcessing = true;    // bloom, vignette, dimmed world behind menus, warm tint during the ultimate

    private static CameraFollow instance;
    private Vector3 velocity = Vector3.zero;
    private Vector3 followPos;
    private float shakeAmount;
    private float shakeTimer;
    private Camera cam;
    private float baseSize;
    private Vector2 lead, leadVelocity, lastTargetPos;
    private float crowdSize, zoomPunch;
    private ColorAdjustments colorAdjust;
    private Volume volume;
    private PlayerUltimate ult;

    // Briefly zooms in by `amount` (orthographic size), easing back out.
    public static void ZoomPunch(float amount)
    {
        if (instance != null) instance.zoomPunch = Mathf.Max(instance.zoomPunch, amount);
    }

    public static void Shake(float amount, float duration)
    {
        if (instance == null || !Settings.ScreenShake) return;
        instance.shakeAmount = Mathf.Max(instance.shakeAmount, amount);
        instance.shakeTimer = Mathf.Max(instance.shakeTimer, duration);
    }

    private void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
        if (cam != null) baseSize = cam.orthographicSize;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }

        followPos = transform.position;
        if (target != null) { lastTargetPos = target.position; ult = target.GetComponent<PlayerUltimate>(); }
        crowdSize = baseSize;
        if (postProcessing && Settings.PostProcessing) SetUpPostProcessing();
        else if (TryGetComponent(out UniversalAdditionalCameraData data)) data.renderPostProcessing = false;
    }

    private void LateUpdate()
    {
        // title screen: look at the little menu map (its own spot far from the arena) in full color
        if (!MainMenu.Started && MapGenerator.MenuCenter is Vector2 menu)
        {
            transform.position = new Vector3(menu.x, menu.y, transform.position.z);
            if (cam != null) cam.orthographicSize = baseSize;
            if (colorAdjust != null) { colorAdjust.saturation.value = 0f; colorAdjust.postExposure.value = 0f; }
            return;
        }
        if (target == null) return;

        // lead toward where the player is heading (from their actual movement, so dashes and knockback count)
        Vector2 targetPos = target.position;
        Vector2 moved = Time.deltaTime > 0f ? (targetPos - lastTargetPos) / Time.deltaTime : Vector2.zero;
        lastTargetPos = targetPos;
        lead = Vector2.SmoothDamp(lead, Vector2.ClampMagnitude(moved * 0.25f, leadDistance), ref leadVelocity, 0.35f);

        Vector3 desired = new Vector3(targetPos.x + offset.x + lead.x, targetPos.y + offset.y + lead.y, followPos.z);
        followPos = Vector3.SmoothDamp(followPos, desired, ref velocity, smoothTime);

        if (cam != null && cam.orthographic)
        {
            float crowd = Mathf.Clamp01((EnemyAI.AliveCount - 30f) / Mathf.Max(1f, crowdForFullZoom - 30f));
            crowdSize = Mathf.Lerp(crowdSize, baseSize + crowdZoom * crowd, Time.deltaTime * 0.5f);
            zoomPunch = Mathf.MoveTowards(zoomPunch, 0f, Time.unscaledDeltaTime * 1.5f);
            cam.orthographicSize = crowdSize - zoomPunch;
        }

        // stop at the map edges so the view never shows past the wall (each axis on its own, so corners hold too)
        if (MapGenerator.Active && cam != null)
        {
            Rect b = MapGenerator.Bounds;
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            followPos.x = b.width > hw * 2f ? Mathf.Clamp(followPos.x, b.xMin + hw, b.xMax - hw) : b.center.x;
            followPos.y = b.height > hh * 2f ? Mathf.Clamp(followPos.y, b.yMin + hh, b.yMax - hh) : b.center.y;
        }

        if (colorAdjust != null)
        {
            // menus: desaturate and darken the game behind them; ultimate: warm tint while it lasts
            float k = Time.unscaledDeltaTime * 8f;
            bool paused = Time.timeScale == 0f;
            bool ultOn = ult != null && ult.IsActive;
            colorAdjust.saturation.value = Mathf.Lerp(colorAdjust.saturation.value, paused ? -70f : 0f, k);
            colorAdjust.postExposure.value = Mathf.Lerp(colorAdjust.postExposure.value, paused ? -0.8f : 0f, k);
            colorAdjust.colorFilter.value = Color.Lerp(colorAdjust.colorFilter.value, ultOn ? new Color(1f, 0.95f, 0.87f) : Color.white, k);
        }

        Vector3 shake = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            shake = (Vector3)(Random.insideUnitCircle * shakeAmount);
            if (shakeTimer <= 0f) shakeAmount = 0f;
        }

        transform.position = followPos + shake;
    }

    // Settings > Bloom & vignette: built on first use, then just switched on/off.
    public static void SetPostProcessing(bool on)
    {
        if (instance == null || !instance.postProcessing) return;
        if (on && instance.volume == null) instance.SetUpPostProcessing();
        if (instance.volume != null) instance.volume.enabled = on;
        if (instance.TryGetComponent(out UniversalAdditionalCameraData data)) data.renderPostProcessing = on;
    }

    // Bloom makes explosions, lasers, flashes and sparks glow; the vignette frames the screen.
    // Built at runtime so no volume asset needs setting up.
    private void SetUpPostProcessing()
    {
        if (TryGetComponent(out UniversalAdditionalCameraData data)) data.renderPostProcessing = true;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.6f);
        bloom.scatter.Override(0.65f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.45f);
        colorAdjust = profile.Add<ColorAdjustments>(true);
        colorAdjust.saturation.Override(0f);
        colorAdjust.postExposure.Override(0f);
        colorAdjust.colorFilter.Override(Color.white);

        volume = new GameObject("PostProcessing").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }
}
