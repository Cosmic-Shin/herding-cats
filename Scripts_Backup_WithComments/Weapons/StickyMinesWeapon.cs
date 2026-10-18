using UnityEngine;

// Collab: Proximity Mines + Glue Bomb. Glue-covered mines are thrown at cats, stick to whatever they
// hit and hold it in place, then explode (a real explosion, so explosion skills trigger) and leave
// a small glue puddle. A mine that hits nothing goes off where it lands.
public class StickyMinesWeapon : Weapon
{
    private const float Cooldown = 1.3f;
    private const int Count = 4;
    private const float Speed = 9f;
    private const float Fuse = 0.9f;
    private const float Damage = 40f;
    private const float Radius = 2f;
    private static readonly Color Glue = new Color(0.55f, 1f, 0.35f);

    private void Update()
    {
        if (!Ready(Cooldown)) return;
        Sfx.Play(Sfx.Id.Swish, 0.3f, 0.8f);
        for (int i = 0; i < Count; i++)
        {
            Transform target = i == 0 ? NearestEnemy(8f) : RandomEnemy(8f);
            Vector2 dir = target != null ? ((Vector2)target.position - Pos).normalized : Rotate(Facing, Random.Range(-50f, 50f));
            Throw(dir);
        }
    }

    private void Throw(Vector2 dir)
    {
        Shot s = Shot.Spawn(Shapes.Art("landmine"), new Color(0.8f, 1f, 0.7f), 0.7f, Pos);
        s.velocity = dir * Speed;
        s.life = 0.8f;
        s.hitRadius = 0.35f;
        s.spin = 540f;
        s.trail = Glue;

        float damage = Dmg(Damage), radius = Area(Radius);
        EnemyHealth stuckTo = null;
        int stuckLife = 0;
        Vector2 offset = Vector2.zero;

        void Blow(Shot shot)
        {
            Explode(shot.Position, radius, damage);
            BurningGround.Spawn(shot.Position, radius * 0.6f, 2f, 0f, Glue, 0.6f);
        }

        s.onHit = (shot, e) =>
        {
            if (stuckTo != null) return;
            stuckTo = e;
            stuckLife = e.Life;
            offset = shot.Position - (Vector2)e.transform.position;
            shot.velocity = Vector2.zero;
            shot.spin = 0f;
            shot.hitRadius = 0f;
            shot.life = shot.age + Fuse;
            Juice.Sparks(shot.Position, Glue, 4, 2f, 0.1f);
        };
        s.onUpdate = shot =>
        {
            if (stuckTo == null) return;
            if (stuckTo.IsDead || stuckTo.Life != stuckLife) { Blow(shot); shot.Release(); return; } // target died: go off now
            shot.transform.position = (Vector2)stuckTo.transform.position + offset;
            if (stuckTo.TryGetComponent(out IEnemyBrain brain)) brain.Slow(0.9f, 0.2f); // glued in place
            bool blink = Mathf.Repeat(shot.age * 8f, 1f) > 0.5f;
            shot.Renderer.color = blink ? new Color(1f, 0.5f, 0.5f) : new Color(0.8f, 1f, 0.7f);
        };
        s.onExpire = Blow;
    }
}
