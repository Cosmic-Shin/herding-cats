using System.Collections.Generic;
using UnityEngine;

public abstract class SuperWeapon : Weapon
{
    public static SuperWeapon Active { get; private set; }

    public static readonly Color SuperPink = new Color(1f, 0.45f, 0.85f);
    private readonly List<Weapon> parts = new List<Weapon>();

    public void Adopt(IEnumerable<Weapon> weapons)
    {
        foreach (Weapon w in weapons)
        {
            if (w == null) continue;
            w.DisplayName = DisplayName;
            w.AddEnhance(Boost.Damage);
            w.AddEnhance(Boost.Damage);
            parts.Add(w);
        }
        Active = this;
    }

    protected override void OnEnhanced(Boost boost)
    {
        foreach (Weapon w in parts) if (w != null) w.AddEnhance(boost);
    }

    public virtual void OnBombBlast(Vector2 pos, float radius, float damage) { }
    public virtual void OnThrow(Vector2 target) { }

    protected virtual void OnDestroy()
    {
        if (Active == this) Active = null;
        foreach (Weapon w in parts) if (w != null) Destroy(w);
    }

    protected static Shot Fx(Sprite sprite, Color color, float size, Vector2 pos, float life, float endScale = 1f, int order = 45, float spin = 0f)
    {
        Shot s = Shot.Spawn(sprite, color, size, pos, order);
        s.life = life;
        s.spin = spin;
        s.onUpdate = shot =>
        {
            float k = shot.age / life;
            shot.transform.localScale = Vector3.one * size * Mathf.Lerp(1f, endScale, k);
            Color c = color;
            c.a *= 1f - k;
            shot.Renderer.color = c;
        };
        return s;
    }

    protected static Shot Beam(Vector2 from, Vector2 dir, float length, float width, Color color, float life, int order = 46)
    {
        Shot s = Shot.Spawn(Shapes.Square, color, 1f, from, order);
        s.life = life;
        s.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        s.transform.localScale = new Vector3(length, width, 1f);
        s.onUpdate = shot =>
        {
            float k = shot.age / life;
            shot.transform.localScale = new Vector3(length, width * (1f - 0.7f * k), 1f);
            Color c = color;
            c.a *= 1f - k;
            shot.Renderer.color = c;
        };
        return s;
    }

    protected static void GlowBeam(Vector2 from, Vector2 dir, float length, float width, Color color, float life)
    {
        Beam(from, dir, length, width * 3f, new Color(color.r, color.g, color.b, 0.3f), life, 45);
        Beam(from, dir, length, width, Color.Lerp(color, Color.white, 0.6f), life, 46);
    }

    protected static List<EnemyHealth> Targets(Vector2 pos, float radius)
    {
        var result = new List<EnemyHealth>();
        foreach (Collider2D c in EnemiesNear(pos, radius))
            if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead) result.Add(e);
        return result;
    }

    protected void HitArea(Vector2 pos, float radius, float damage, System.Action<EnemyHealth> extra = null)
    {
        foreach (EnemyHealth e in Targets(pos, radius))
        {
            Hit(e, damage, pos);
            extra?.Invoke(e);
        }
    }

    protected void HitLine(Vector2 from, Vector2 dir, float length, float width, float damage, System.Action<EnemyHealth> extra = null)
    {
        foreach (EnemyHealth e in Targets(from + dir * length * 0.5f, length * 0.5f + width))
        {
            Vector2 p = e.transform.position;
            float along = Mathf.Clamp(Vector2.Dot(p - from, dir), 0f, length);
            if ((from + dir * along - p).sqrMagnitude > width * width) continue;
            Hit(e, damage, p - dir, knockback: false);
            extra?.Invoke(e);
        }
    }

    protected static EnemyAI Cat(EnemyHealth e) => e.TryGetComponent(out EnemyAI ai) ? ai : null;

    protected Vector2 Crowd(float range)
    {
        Vector2 best = Pos + Facing * 3f;
        int bestCount = -1;
        for (int i = 0; i < 6; i++)
        {
            Transform t = RandomEnemy(range);
            if (t == null) break;
            int count = EnemiesNear(t.position, 2.5f).Count;
            if (count > bestCount) { bestCount = count; best = t.position; }
        }
        return best;
    }

    protected static void Shatter(Vector2 pos, float size)
    {
        Juice.Sparks(pos, new Color(0.75f, 0.95f, 1f), 8, 7f * size, 0.16f);
        Fx(Shapes.Star, new Color(0.85f, 0.97f, 1f, 0.9f), 0.9f * size, pos, 0.25f, 2.2f, 47, 360f);
    }
}
