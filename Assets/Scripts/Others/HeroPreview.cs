using System.Collections.Generic;
using UnityEngine;

// Menu showcase: the picked hero stands to the right of the menu, idling, and every couple of seconds
// shows off their attack (Mira slashes inside her bubble, Kai sweeps the greatsword, Nyx's drones zap).
// The menu pauses the game, so this runs on unscaled time with its own renderers instead of the in-game fx.
public class HeroPreview : MonoBehaviour
{
    private const float Scale = 1.6f, AttackEvery = 2.4f, AttackFps = 14f;

    private SpriteRenderer body, glow, shadow, bubble, sword, slash, zap;
    private readonly List<SpriteRenderer> drones = new List<SpriteRenderer>();
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[4];
    private readonly float[] ghostAngles = new float[4];
    private Sprite idle;
    private Sprite[] attack;
    private string hero;
    private float t, attackT = -1f, nextAttack = 1f;
    private Camera cam;
    public Vector2 screen = new Vector2(0.68f, -0.3f); // where it stands, as a fraction of the half-screen from centre

    public static HeroPreview Create() => new GameObject("HeroPreview").AddComponent<HeroPreview>();

    private SpriteRenderer Part(string name, Sprite sprite, int order, bool unlit = true)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.sprite = sprite;
        sr.sortingOrder = order;
        if (unlit) Lighting.Glow(sr);
        return sr;
    }

    public void Show(string id)
    {
        if (id == hero) return;
        hero = id;
        foreach (Transform child in transform) Destroy(child.gameObject);
        drones.Clear();
        cam = Camera.main;
        transform.localScale = Vector3.one * Scale;

        var frames = new Dictionary<string, Sprite>();
        foreach (Sprite s in Resources.LoadAll<Sprite>("Heroes/" + id)) frames[s.name.Substring(s.name.IndexOf('_'))] = s;
        idle = frames.TryGetValue("_Down_0", out Sprite d) ? d : null;
        attack = new Sprite[5];
        for (int i = 0; i < 5; i++) frames.TryGetValue("_SlashDownLeft_" + i, out attack[i]);

        Color tint = id == "Kai" ? new Color(1f, 0.35f, 0.3f) : id == "Nyx" ? NyxDrones.Cyan : new Color(1f, 0.6f, 0.85f);
        glow = Part("Glow", Shapes.Circle, 100);
        glow.transform.localPosition = new Vector3(0f, -0.6f, 0f);
        glow.transform.localScale = new Vector3(1.15f, 0.38f, 1f);
        glow.color = new Color(tint.r, tint.g, tint.b, 0.35f);
        shadow = Part("Shadow", Shapes.Circle, 101, false);
        shadow.transform.localPosition = new Vector3(0f, -0.6f, 0f);
        shadow.transform.localScale = new Vector3(0.8f, 0.25f, 1f);
        shadow.color = new Color(0f, 0f, 0f, 0.35f);
        body = Part("Hero", idle, 110);

        bubble = sword = slash = zap = null;
        if (id == "Mira")
        {
            bubble = Part("Bubble", Shapes.Bubble, 112);
            slash = Part("Slash", Shapes.Crescent, 113);
            slash.enabled = false;
        }
        else if (id == "Kai")
        {
            sword = Part("Sword", KaiSword.BladeSprite, 109, false);
            for (int i = 0; i < ghosts.Length; i++)
            {
                ghosts[i] = Part("Ghost", KaiSword.BladeSprite, 108);
                ghosts[i].enabled = false;
            }
        }
        else if (id == "Nyx")
        {
            for (int i = 0; i < 3; i++) drones.Add(Part("Drone", NyxDrones.DroneSprites[0], 111, false));
            zap = Part("Zap", Shapes.Square, 114);
            zap.enabled = false;
        }
        t = 0f;
        attackT = -1f;
        nextAttack = 0.8f;
    }

    private void LateUpdate()
    {
        if (cam == null || body == null) return;
        float dt = Time.unscaledDeltaTime;
        t += dt;

        // Stand to the right of the menu.
        float hh = cam.orthographicSize, hw = hh * cam.aspect;
        transform.position = new Vector3(cam.transform.position.x + hw * screen.x, cam.transform.position.y + hh * screen.y, 0f);

        // Idle breathing; attack plays the slash frames.
        if ((nextAttack -= dt) <= 0f) { nextAttack = AttackEvery; attackT = 0f; }
        float breathe = 0.03f * Mathf.Sin(t * 3f);
        body.transform.localScale = new Vector3(1f - breathe * 0.5f, 1f + breathe, 1f);
        if (attackT >= 0f)
        {
            int f = (int)(attackT * AttackFps);
            body.sprite = f < attack.Length && attack[f] != null ? attack[f] : idle;
            attackT += dt;
            if (f >= attack.Length + 2) attackT = -1f;
        }
        else body.sprite = idle;
        float k = attackT >= 0f ? attackT : -1f;
        glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, 0.28f + 0.08f * Mathf.Sin(t * 2f));

        if (hero == "Mira") Mira(k);
        else if (hero == "Kai") Kai(k);
        else if (hero == "Nyx") Nyx(k, dt);
    }

    private void Mira(float k)
    {
        float wob = 0.035f * Mathf.Sin(t * 5f);
        bubble.transform.localScale = new Vector3(1f + wob, 1f - wob, 1f) * 1.25f;
        Color tint = Color.Lerp(new Color(0.55f, 0.9f, 1f), new Color(0.85f, 0.7f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 1.3f));
        bubble.color = new Color(tint.r, tint.g, tint.b, 0.45f);
        // A quick crescent slash out to the left.
        bool on = k >= 0.08f && k < 0.3f;
        slash.enabled = on;
        if (!on) return;
        float a = (k - 0.08f) / 0.22f;
        slash.transform.localPosition = new Vector3(-0.75f, -0.05f, 0f);
        slash.transform.localRotation = Quaternion.Euler(0f, 0f, 180f - 211f);
        slash.transform.localScale = Vector3.one * (1.1f + 0.3f * a);
        slash.color = new Color(1f, 1f, 1f, 0.9f * (1f - a));
    }

    private void Kai(float k)
    {
        // Rest: over the shoulder. Attack: wind up, then a heavy sweep across the left side with ghost blades.
        float sweep = k < 0f ? -1f : Mathf.Clamp01((k - 0.06f) / 0.2f);
        float angle;
        if (k < 0f || k > 0.45f) angle = Mathf.LerpAngle(sword.transform.localEulerAngles.z + 90f, 235f, 0.2f);
        else if (k < 0.06f) angle = 265f;
        else angle = Mathf.LerpAngle(255f, 135f, 1f - Mathf.Pow(1f - sweep, 3f)); // low sweep across the left, stays clear of the menu
        bool swinging = k >= 0.06f && k <= 0.45f;
        sword.transform.localPosition = swinging ? new Vector3(0f, -0.05f, 0f) : new Vector3(-0.08f, 0.3f, 0f);
        sword.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        sword.transform.localScale = Vector3.one; // same size relative to Kai as in game
        sword.sortingOrder = swinging ? 112 : 109;

        for (int i = ghosts.Length - 1; i > 0; i--) ghostAngles[i] = ghostAngles[i - 1];
        ghostAngles[0] = angle;
        for (int i = 0; i < ghosts.Length; i++)
        {
            ghosts[i].enabled = swinging && sweep < 1f;
            if (!ghosts[i].enabled) continue;
            ghosts[i].transform.localPosition = sword.transform.localPosition;
            ghosts[i].transform.localRotation = Quaternion.Euler(0f, 0f, ghostAngles[i] - 90f);
            ghosts[i].transform.localScale = sword.transform.localScale;
            ghosts[i].color = new Color(0.85f, 0.9f, 1f, 0.45f * (1f - i / (float)ghosts.Length));
        }
    }

    private void Nyx(float k, float dt)
    {
        for (int i = 0; i < drones.Count; i++)
        {
            float a = t * 1.9f + i * Mathf.PI * 2f / drones.Count;
            Vector3 home = new Vector3(Mathf.Cos(a) * 0.95f, 0.3f + Mathf.Sin(a) * 0.35f + 0.04f * Mathf.Sin(t * 4f + i), 0f);
            Vector3 pos = home;
            if (i == 0 && k >= 0f && k < 0.5f) // the first drone darts out to zap
            {
                Vector3 target = new Vector3(-1.7f, -0.1f, 0f);
                pos = Vector3.Lerp(home, target, Mathf.Sin(Mathf.Clamp01(k / 0.5f) * Mathf.PI));
            }
            Transform d = drones[i].transform;
            d.localPosition = pos;
            d.localScale = Vector3.one;
            drones[i].sprite = NyxDrones.DroneSprites[(int)(t * 20f + i) % 2];
            drones[i].sortingOrder = pos.y < 0.2f ? 111 : 108;
        }
        bool zapping = k >= 0.2f && k < 0.32f;
        zap.enabled = zapping;
        if (!zapping) return;
        Vector3 from = drones[0].transform.localPosition, to = new Vector3(-2.3f, -0.25f, 0f);
        Vector3 v = to - from;
        zap.transform.localPosition = from;
        zap.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
        zap.transform.localScale = new Vector3(v.magnitude, Random.Range(0.03f, 0.06f), 1f);
        zap.color = Random.value < 0.5f ? NyxDrones.Cyan : Color.white;
    }
}
