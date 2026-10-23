using UnityEngine;
using UnityEngine.InputSystem;

// The special (ultimate). All heroes get bigger, harder hits and extra bombs/swings/drones while it's active;
// each also has their own opener, look and extra:
//   Mira  - SPECIAL:  bomb burst all around her.
//   Kai   - RAGE:     a full spin; red aura, swings 40% faster, heals a little on every sword hit.
//   Nyx   - OVERRIDE: an EMP that hacks every cat on screen; drones fire 50% faster.
[RequireComponent(typeof(PlayerAutoAttack))]
public class PlayerUltimate : MonoBehaviour
{
    public float cooldown = 60f;
    public float firstChargeTime = 30f;
    public float duration = 10f;

    [Header("While Active")]
    public float damageMultiplier = 1.6f;
    public float radiusMultiplier = 1.5f;
    public int extraBombs = 2;

    [Header("Opening Burst")]
    public int burstBombs = 8;

    public float CooldownRemaining { get; private set; }
    public float ActiveRemaining { get; private set; }
    public bool IsActive => ActiveRemaining > 0f;
    public bool IsReady => !IsActive && CooldownRemaining <= 0f;
    public void Recharge() => CooldownRemaining = 0f;

    public float DamageMultiplier => IsActive ? damageMultiplier : 1f;
    public float RadiusMultiplier => IsActive ? radiusMultiplier : 1f;
    public int ExtraBombs => IsActive ? extraBombs : 0;

    private class Style { public string name, shout; public Color main, light; public float speedBonus; public Color? mood; }
    private static readonly Style Mira = new Style { name = "SPECIAL", shout = "ULTIMATE!", main = new Color(1f, 0.6f, 0.15f), light = new Color(1f, 0.85f, 0.4f) };
    private static readonly Style Kai = new Style { name = "RAGE", shout = "RAGE!", main = new Color(0.95f, 0.12f, 0.1f), light = new Color(1f, 0.45f, 0.35f), speedBonus = 0.4f, mood = new Color(1f, 0.6f, 0.55f) };
    private static readonly Style Nyx = new Style { name = "OVERRIDE", shout = "SYSTEM OVERRIDE!", main = new Color(0.3f, 0.9f, 1f), light = new Color(0.75f, 1f, 1f), speedBonus = 0.5f, mood = new Color(0.7f, 0.88f, 1f) };
    private static Style Current => Hero.Current.id == "Kai" ? Kai : Hero.Current.id == "Nyx" ? Nyx : Mira;

    public string Name => Current.name;
    public static readonly Color RageRed = Kai.main;

    private PlayerAutoAttack attack;
    private PlayerStats stats;
    private SpriteRenderer aura, body;
    private float trailTimer, appliedSpeed;
    private Color? moodBefore;
    private Style active;

    private void Awake()
    {
        attack = GetComponent<PlayerAutoAttack>();
        stats = GetComponent<PlayerStats>();
        CooldownRemaining = firstChargeTime;
        body = GetComponentInChildren<SpriteRenderer>();
        aura = CreateAura();
    }

    private void OnDisable()
    {
        if (IsActive) End();
    }

    private void Update()
    {
        if (IsActive)
        {
            ActiveRemaining -= Time.deltaTime;
            Style s = active ?? Current;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            aura.transform.localScale = Vector3.one * (1.6f + 0.15f * pulse);
            aura.color = new Color(s.main.r, s.main.g, s.main.b, 0.25f + 0.15f * pulse);
            // While active: tinted afterimages + rising sparks (and a glitch flicker for Nyx).
            if ((trailTimer -= Time.deltaTime) <= 0f)
            {
                trailTimer = 0.07f;
                if (body != null && !Settings.LowParticles) AfterImage.Spawn(body, new Color(s.light.r, s.light.g, s.light.b, 0.85f));
                Juice.SparksDir((Vector2)transform.position + Random.insideUnitCircle * 0.5f, Vector2.up, 30f, s.main, 2, 2.5f, 0.1f);
                if (s == Nyx && Random.value < 0.3f) HeroFx.Glitch(body, 0.08f);
            }
            if (!IsActive) End();
        }
        else if (CooldownRemaining > 0f)
        {
            CooldownRemaining -= Time.deltaTime;
        }

        if (IsReady && Time.timeScale > 0f && Pressed()) Activate();
    }

    private static bool Pressed() =>
        Keybinds.Pressed(Keybinds.Action.Special) ||
        Keybinds.PadPressed(Keybinds.Action.Special);

    private void Activate()
    {
        Style s = active = Current;
        ActiveRemaining = duration;
        aura.enabled = true;
        if (TryGetComponent(out KaiSword sword) && sword.enabled) sword.Spin(transform.position, 2.5f);
        else if (TryGetComponent(out NyxDrones drones) && drones.enabled)
        {
            drones.Barrage();
            if (NyxPassives.Active != null) NyxPassives.Active.HackAll(3f);
        }
        else attack.Burst(burstBombs);

        appliedSpeed = s.speedBonus;
        stats.attackSpeedMultiplier += appliedSpeed;
        if (s.mood != null) { moodBefore = Lighting.Mood; Lighting.Mood = s.mood; }

        Vector2 at = transform.position;
        ExplosionFx.Spawn(at, 2.5f, s.main);
        CameraFollow.Shake(0.2f, 0.3f);
        DamagePopup.SpawnText(at + Vector2.up * 0.5f, s.shout, s.light, 6f);
        Juice.Flash(new Color(s.main.r, s.main.g, s.main.b, 0.45f), 0.35f);
        Juice.HitStop(0.1f);
        ExplosionFx.Ring(at, 9f, s.light, 0.6f);
        ExplosionFx.Ring(at, 5f, Color.white, 0.4f);
        Juice.Sparks(at, s.light, 24, 12f, 0.2f);
        if (s == Nyx) HeroFx.Bits(at, s.main, NyxPassives.Magenta, 24, 9f, 0.12f);
        CameraFollow.ZoomPunch(0.8f);
        GamepadUI.Rumble(0.6f, 0.3f, 0.35f);
        Sfx.Play(Sfx.Id.Whoosh, 0.8f, 0.7f);
    }

    private void End()
    {
        ActiveRemaining = 0f;
        CooldownRemaining = cooldown;
        aura.enabled = false;
        stats.attackSpeedMultiplier -= appliedSpeed;
        appliedSpeed = 0f;
        if (active != null && active.mood != null && Lighting.Mood == active.mood) Lighting.Mood = moodBefore;
        active = null;
    }

    private SpriteRenderer CreateAura()
    {
        var go = new GameObject("UltimateAura");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(sr);
        sr.sprite = ExplosionFx.GetCircle();
        sr.sortingOrder = 9;
        sr.enabled = false;
        return sr;
    }
}
