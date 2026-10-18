using UnityEngine;

public class SeismicFieldWeapon : AuraWeapon
{
    private const float QuakeEvery = 2.5f;
    private const float QuakeDamage = 40f;
    private const float Push = 9f;
    private static readonly Color Dust = new Color(0.85f, 0.72f, 0.5f);

    private float quakeTimer = 1f;

    protected override Color FillTint => new Color(1f, 0.85f, 0.65f);

    protected override void Update()
    {
        base.Update();

        if ((quakeTimer -= Time.deltaTime) > 0f) return;
        quakeTimer = QuakeEvery / Mathf.Max(0.1f, stats.attackSpeedMultiplier);

        float radius = CurrentRadius * 1.2f, damage = Dmg(QuakeDamage);
        foreach (Collider2D c in EnemiesNear(Pos, radius))
        {
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, Pos, knockback: false);
            if (c.TryGetComponent(out IEnemyBrain brain)) brain.Knockback(((Vector2)c.transform.position - Pos).normalized * Push);
            if (c.TryGetComponent(out EnemyAI cat)) cat.Stun(0.6f);
        }

        Sfx.Play(Sfx.Id.Boom, 0.55f, 0.5f);
        StompFx.Spawn(Pos, radius);
        for (int i = 0; i < 14; i++)
            Juice.CatPuff(Pos + Rotate(Vector2.right, i * 360f / 14) * radius * Random.Range(0.4f, 0.9f), Dust);
        Juice.Sparks(Pos, new Color(0.6f, 0.45f, 0.3f), 14, 10f, 0.15f);
        CameraFollow.Shake(0.15f, 0.2f);
        if (TryGetComponent(out PlayerAnimator anim)) anim.Squash(new Vector2(1.3f, 0.7f), 0.2f);
    }
}
