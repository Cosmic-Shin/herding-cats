using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerStats))]
public class PlayerHealth : MonoBehaviour
{
    public static int Hits;
    private PlayerStats stats;
    private PlayerController controller;

    [Header("Invincibility")]
    public float invincibilityDuration = 1f;
    private bool isInvincible = false;

    [Header("Feedback")]
    public SpriteRenderer spriteRenderer;
    public float flashInterval = 0.1f;
    public Color damageFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

    private bool isDead = false;
    private Color originalSpriteColor;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            originalSpriteColor = spriteRenderer.color;
        }
    }

    private void OnEnable()
    {
        stats.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        stats.OnDeath -= HandleDeath;
    }

    public void TakeDamage(float amount)
    {
        if (isInvincible || isDead || (controller != null && controller.IsInvulnerable) || AdminTool.GodMode) return;

        if (Random.value < stats.dodgeChance)
        {
            DamagePopup.SpawnText(transform.position, "DODGE", new Color(0.6f, 0.9f, 1f), 3.5f);
            if (spriteRenderer != null) AfterImage.Spawn(spriteRenderer, new Color(0.6f, 0.9f, 1f, 0.6f));
            Sfx.Play(Sfx.Id.Swish, 0.6f, 1.3f);
            return;
        }

        if (MiraPassives.Active != null && MiraPassives.Active.TryBlock())
        {
            StartCoroutine(ShieldGrace());
            GamepadUI.Rumble(0.1f, 0.4f, 0.1f);
            Sfx.Play(Sfx.Id.Shatter, 0.7f, 1.4f);
            Juice.HitStop(0.05f);
            return;
        }

        amount *= (1f - stats.damageReduction) * EnemySpawner.DamageScale;
        PlayerSkills skills = PlayerSkills.Instance;

        if (stats.currentHealth - amount <= 0f && skills != null && skills.TryRevive())
        {
            StartCoroutine(InvincibilityFrames());
            return;
        }

        stats.ModifyHealth(-amount);
        Hits++;
        Sfx.Play(Sfx.Id.Hurt, 0.8f);

        if (!isDead)
        {
            if (skills != null) skills.OnPlayerHit();
            Juice.HitStop(0.08f);
            Juice.Flash(new Color(1f, 0.15f, 0.15f, 0.25f), 0.25f);
            Vector2 at = transform.position;
            Juice.Sparks(at, new Color(1f, 0.3f, 0.3f), 8, 6f, 0.13f);
            ExplosionFx.Ring(at, 0.9f, new Color(1f, 0.35f, 0.35f), 0.2f);
            DamagePopup.SpawnText(at + Vector2.up * 0.8f, "-" + Mathf.CeilToInt(amount), new Color(1f, 0.3f, 0.3f), 4.2f);
            GamepadUI.Rumble(0.35f, 0.6f, 0.18f);
            CameraFollow.Shake(0.12f, 0.15f);
            if (TryGetComponent(out PlayerAnimator anim)) anim.Squash(new Vector2(0.8f, 1.2f), 0.15f);
            StartCoroutine(InvincibilityFrames());
        }
    }

    private IEnumerator ShieldGrace()
    {
        isInvincible = true;
        yield return new WaitForSeconds(0.5f);
        isInvincible = false;
    }

    private IEnumerator InvincibilityFrames()
    {
        isInvincible = true;
        float timer = 0f;

        while (timer < invincibilityDuration)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = timer < flashInterval ? damageFlashColor
                                     : new Color(originalSpriteColor.r, originalSpriteColor.g, originalSpriteColor.b, 0.3f);
            }

            yield return new WaitForSeconds(flashInterval);
            timer += flashInterval;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalSpriteColor;
            }

            yield return new WaitForSeconds(flashInterval);
            timer += flashInterval;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalSpriteColor;
        }

        isInvincible = false;
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        GamepadUI.Rumble(0.8f, 0.8f, 0.6f);

        StopAllCoroutines();

        if (controller != null) controller.enabled = false;
        if (TryGetComponent(out PlayerAutoAttack attack)) attack.enabled = false;
        if (TryGetComponent(out KaiSword sword)) sword.enabled = false;
        if (TryGetComponent(out NyxDrones drones)) drones.enabled = false;
        if (TryGetComponent(out PlayerAnimator anim)) anim.enabled = false;
        if (TryGetComponent(out PlayerUltimate ult)) ult.enabled = false;
        foreach (Weapon weapon in GetComponents<Weapon>()) weapon.enabled = false;
        foreach (HeroPassives passives in GetComponents<HeroPassives>()) passives.enabled = false;

        StartCoroutine(DeathFx());
    }

    // Death animation (unscaled time, since the game slows to a stop): burst, fall over, grey out, ghost rises.
    private IEnumerator DeathFx()
    {
        Vector2 at = transform.position;
        Juice.Sparks(at, new Color(0.45f, 0.95f, 0.9f), 24, 9f, 0.16f);
        Juice.Sparks(at, Color.white, 10, 5f, 0.12f);
        ExplosionFx.Ring(at, 2.5f, new Color(1f, 0.4f, 0.4f), 0.5f);
        Lighting.Flash(at, 6f, new Color(1f, 0.4f, 0.4f), 0.8f);
        CameraFollow.ZoomPunch(0.5f);
        if (spriteRenderer == null) yield break;
        Juice.Ghost(spriteRenderer, 1.8f, 1.4f);

        Transform body = spriteRenderer.transform;
        Quaternion start = body.localRotation;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.35f;
            body.localRotation = start * Quaternion.Euler(0f, 0f, -90f * (1f - (1f - k) * (1f - k)));
            spriteRenderer.color = Color.Lerp(originalSpriteColor, new Color(0.45f, 0.45f, 0.5f), k);
            yield return null;
        }
        body.localRotation = start * Quaternion.Euler(0f, 0f, -90f);
    }
}
