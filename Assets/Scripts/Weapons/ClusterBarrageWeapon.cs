using UnityEngine;

public class ClusterBarrageWeapon : MissileWeapon
{
    private const float MineDamage = 30f;
    private const float MineRadius = 1.8f;

    protected override void OnBlast(Vector2 pos, float radius, float damage)
    {
        for (int i = 0; i < 3; i++)
        {
            Vector2 at = pos + Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.6f);
            MineWeapon.SpawnMine(at, Dmg(MineDamage), Area(MineRadius), Explode);
        }
    }
}
