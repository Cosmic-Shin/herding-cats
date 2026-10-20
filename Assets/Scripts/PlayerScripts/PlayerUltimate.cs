using UnityEngine;
using UnityEngine.InputSystem;

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

    private PlayerAutoAttack attack;
    private SpriteRenderer aura;

    private void Awake()
    {
        attack = GetComponent<PlayerAutoAttack>();
        CooldownRemaining = firstChargeTime;
        aura = CreateAura();
    }

    private void Update()
    {
        if (IsActive)
        {
            ActiveRemaining -= Time.deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            aura.transform.localScale = Vector3.one * (1.6f + 0.15f * pulse);
            aura.color = new Color(1f, 0.6f, 0.15f, 0.25f + 0.15f * pulse);
            if (!IsActive)
            {
                CooldownRemaining = cooldown;
                aura.enabled = false;
            }
        }
        else if (CooldownRemaining > 0f)
        {
            CooldownRemaining -= Time.deltaTime;
        }

        if (IsReady && Time.timeScale > 0f && Pressed()) Activate();
    }

    private static bool Pressed() =>
        Keybinds.Pressed(Keybinds.Action.Special) ||
        (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

    private void Activate()
    {
        ActiveRemaining = duration;
        aura.enabled = true;
        attack.Burst(burstBombs);

        ExplosionFx.Spawn(transform.position, 2.5f, new Color(1f, 0.75f, 0.2f));
        CameraFollow.Shake(0.2f, 0.3f);
        DamagePopup.SpawnText(transform.position + Vector3.up * 0.5f, "ULTIMATE!", new Color(1f, 0.8f, 0.2f), 6f);
        Juice.Flash(new Color(1f, 0.75f, 0.25f, 0.45f), 0.35f);
        Juice.HitStop(0.1f);
        ExplosionFx.Ring(transform.position, 9f, new Color(1f, 0.85f, 0.4f), 0.6f);
        ExplosionFx.Ring(transform.position, 5f, Color.white, 0.4f);
        Juice.Sparks(transform.position, new Color(1f, 0.8f, 0.3f), 24, 12f, 0.2f);
        CameraFollow.ZoomPunch(0.8f);
        Sfx.Play(Sfx.Id.Whoosh, 0.8f, 0.7f);
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
