using UnityEngine;

// Super: Firebomb + Thunderstorm (bomb super). Every bomb blast calls down crimson lightning on the spot and
// leaves a ring of hellfire around it; Thunderstorm keeps raining on top.
public class HellfireBombsWeapon : SuperWeapon
{
    private static readonly Color Hellfire = new Color(1f, 0.3f, 0.15f);
    private float nextRing;

    public override void OnBombBlast(Vector2 pos, float radius, float damage)
    {
        DamageStats.Source = DisplayName;
        LightningFx.Spawn(pos, Hellfire, 1.8f);
        LightningFx.Spawn(pos + Random.insideUnitCircle * radius, Hellfire, 1f);
        HitArea(pos, radius * 1.25f, Dmg(16f));
        Lighting.Flash(pos, radius * 3f, Hellfire, 0.25f);

        if (Time.time < nextRing) return; // fire rings are heavy: one every 0.3s at most
        nextRing = Time.time + 0.3f;
        for (int i = 0; i < 5; i++)
        {
            Vector2 p = pos + Rotate(Vector2.right, i * 72f + Random.Range(0f, 30f)) * radius;
            BurningGround.Spawn(p, Area(0.8f), 2.5f, Dmg(6f), burns: true);
        }
        Fx(Shapes.Ring, Hellfire, radius * 0.5f, pos, 0.4f, 5f, 44);
        Sfx.Play(Sfx.Id.Zap, 0.35f, 0.7f);
    }
}
