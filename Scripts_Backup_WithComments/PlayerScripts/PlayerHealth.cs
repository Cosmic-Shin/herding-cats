using UnityEngine;
using System.Collections;

// Handles the BEHAVIOR of taking damage (i-frames, flashing, death)
// while PlayerStats just holds the numbers. Enemies/hazards call TakeDamage().
[RequireComponent(typeof(PlayerStats))]
public class PlayerHealth : MonoBehaviour
{
    public static int Hits; // hits that landed this run (Achievements: Untouchable); reset with the run counters
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
            Sfx.Play(Sfx.Id.Swish, 0.6f, 1.3f);
            return;
        }

        // Mira's Bubble Shield eats the whole hit; short grace so a crowd can't pop it and still hit the same instant
        if (MiraPassives.Instance != null && MiraPassives.Instance.TryBlock())
        {
            StartCoroutine(ShieldGrace());
            Sfx.Play(Sfx.Id.Shatter, 0.7f, 1.4f); // bubble pops
            Juice.HitStop(0.05f);
            return;
        }

        amount *= (1f - stats.damageReduction) * EnemySpawner.DamageScale; // enemies hit harder the longer the run goes
        PlayerSkills skills = PlayerSkills.Instance;

        // Second Wind: a lethal hit revives instead of killing (once per run)
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
                // red on the first beat, then a see-through blink so you can tell you're untouchable
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

        StopAllCoroutines();

        if (controller != null) controller.enabled = false;
        if (TryGetComponent(out PlayerAutoAttack attack)) attack.enabled = false;
        if (TryGetComponent(out PlayerAnimator anim)) anim.enabled = false;
        if (TryGetComponent(out PlayerUltimate ult)) ult.enabled = false;
        foreach (Weapon weapon in GetComponents<Weapon>()) weapon.enabled = false;
        if (TryGetComponent(out MiraPassives passives)) passives.enabled = false;

        // TODO: trigger death animation, game over screen, etc.
        Debug.Log("Player died!");
    }
}