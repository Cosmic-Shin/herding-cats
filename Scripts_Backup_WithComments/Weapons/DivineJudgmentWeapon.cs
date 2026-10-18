using System.Collections;
using UnityEngine;

// Super: Halo + Thunder Star. Signature: a golden sigil marks the toughest cat on screen, then a pillar of
// holy lightning smites it and 8 golden stars burst out, ricocheting and calling lightning on every hit.
public class DivineJudgmentWeapon : SuperWeapon
{
    private static readonly Color Holy = new Color(1f, 0.85f, 0.35f);

    private void Update()
    {
        if (!Ready(3f)) return;
        EnemyHealth target = Toughest(11f);
        if (target != null) StartCoroutine(Judgment(target));
    }

    private EnemyHealth Toughest(float range)
    {
        EnemyHealth best = null;
        foreach (EnemyHealth e in Targets(Pos, range))
            if (best == null || e.CurrentHealth > best.CurrentHealth) best = e;
        return best;
    }

    private IEnumerator Judgment(EnemyHealth target)
    {
        int life = target.Life;
        Vector2 at = target.transform.position;
        float radius = Area(2.6f);

        // sigil: a spinning star inside a ring that tightens onto the target
        Fx(Shapes.Ring, Holy, radius * 2.6f, at, 0.6f, 0.4f, 4);
        Fx(Shapes.Star, new Color(1f, 0.9f, 0.5f, 0.7f), radius * 1.6f, at, 0.6f, 0.6f, 5, 240f);
        Sfx.Play(Sfx.Id.Ready, 0.6f, 1.4f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            if (target != null && !target.IsDead && target.Life == life) at = target.transform.position; // follows its mark
            yield return null;
        }

        // the pillar
        Beam(at + Vector2.down * 0.3f, Vector2.up, 14f, 3.2f, new Color(1f, 0.85f, 0.35f, 0.35f), 0.5f, 44);
        Beam(at + Vector2.down * 0.3f, Vector2.up, 14f, 1.2f, new Color(1f, 0.97f, 0.8f, 1f), 0.4f, 58);
        for (int i = 0; i < 3; i++) LightningFx.Spawn(at + Random.insideUnitCircle * 0.4f, Holy, 2.5f);
        HitArea(at, radius, Dmg(110f), e => { EnemyAI cat = Cat(e); if (cat != null) cat.Stun(0.8f); });
        Fx(Shapes.Circle, new Color(1f, 0.95f, 0.7f, 0.9f), radius, at, 0.35f, 2.4f, 57);
        Fx(Shapes.Ring, Holy, 1f, at, 0.45f, radius * 4f, 56);
        Lighting.Flash(at, 8f, Holy, 0.5f);
        CameraFollow.Shake(0.16f, 0.2f);
        Sfx.Play(Sfx.Id.Zap, 1f, 0.6f);

        // 8 ricocheting stars
        float starDamage = Dmg(26f);
        for (int i = 0; i < 8; i++) Star(at, Rotate(Vector2.right, i * 45f + 22.5f), starDamage);
    }

    private void Star(Vector2 from, Vector2 dir, float damage)
    {
        Shot s = Shot.Spawn(Shapes.Star, Holy, 0.7f, from, 27);
        s.velocity = dir * 11f;
        s.life = 1.6f;
        s.spin = 900f;
        s.hitRadius = 0.45f;
        s.trail = new Color(1f, 0.9f, 0.5f, 0.7f);
        s.ghost = new Color(1f, 0.85f, 0.4f, 0.4f); // golden blur
        int bounces = 2;
        s.onHit = (shot, e) =>
        {
            Hit(e, damage, shot.Position);
            LightningFx.Spawn(e.transform.position, Holy, 1.2f);
            HitArea(e.transform.position, Area(1f), damage * 0.6f);
            if (bounces-- <= 0) { shot.Release(); return; }
            Transform next = null;
            float bestDist = float.MaxValue;
            foreach (EnemyHealth other in Targets(shot.Position, 5f))
            {
                if (shot.HasHit(other)) continue;
                float d = ((Vector2)other.transform.position - shot.Position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; next = other.transform; }
            }
            if (next != null) shot.velocity = ((Vector2)next.position - shot.Position).normalized * 13f;
        };
    }
}
