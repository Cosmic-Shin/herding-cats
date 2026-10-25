using UnityEngine;

public class BlackHoleWeapon : Weapon
{
    private static readonly float[] Cooldown = { 6f, 6f, 5.5f, 5.5f, 5f, 5f, 4f };
    private static readonly float[] PullRadius = { 4f, 4f, 4.5f, 4.5f, 5f, 5f, 6.5f };
    private static readonly float[] PopDamage = { 30, 36, 36, 44, 50, 58, 75 };
    private static readonly float[] Duration = { 2f, 2f, 2f, 2.5f, 2.5f, 3f, 3f };

    protected virtual void Update()
    {
        if (!Ready(Cooldown[L])) return;
        Sfx.Play(Sfx.Id.Whoosh, 0.5f, 0.5f);

        Transform target = NearestEnemy(9f);
        Vector2 at = target != null ? (Vector2)target.position : Pos + Facing * 4f;
        float popRadius = Area(Level >= MaxLevel ? 3f : 2.2f);
        OnOpened(BlackHole.Open(at, Area(PullRadius[L]), Duration[L], popRadius, Dmg(PopDamage[L]), this));

        if (Level < MaxLevel) return;
        Transform other = RandomEnemy(9f);
        Vector2 at2 = other != null ? (Vector2)other.position : Pos - Facing * 4f;
        OnOpened(BlackHole.Open(at2, Area(PullRadius[L]), Duration[L], popRadius, Dmg(PopDamage[L]), this));
    }

    protected virtual void OnOpened(BlackHole hole) { }

    public virtual void Pop(Vector2 at, float radius, float damage)
    {
        bool supernova = PlayerCollabs.Has("supernova");
        Explode(at, radius * (supernova ? 1.4f : 1f), damage);
        if (!supernova) return;
        if (PlayerSkills.Instance != null) PlayerSkills.Instance.ScatterMinis(at, 8, damage * 0.35f, radius * 0.6f);
        ExplosionFx.Ring(at, radius * 2f, new Color(1f, 0.85f, 0.5f), 0.4f);
        Lighting.Flash(at, radius * 3f, new Color(1f, 0.8f, 0.4f), 0.4f);
        Juice.Sparks(at, Color.white, 14, 9f, 0.15f);
    }
}
