using System.Collections;
using UnityEngine;

// Super: Phoenix Rang + Event Horizon. Signature: 2 huge burning wheels fly out and back, each dragging cats
// toward it, spinning 4 fire lasers around itself and leaving a trail of burning ground.
public class InfernoWheelWeapon : SuperWeapon
{
    private const int Wheels = 2;
    private static readonly Color Fire = new Color(1f, 0.45f, 0.12f);

    private void Update()
    {
        if (!Ready(4.5f)) return;
        Sfx.Play(Sfx.Id.Whoosh, 0.8f, 0.7f);
        Vector2 aim = AimDirection(9f);
        for (int i = 0; i < Wheels; i++) StartCoroutine(Wheel(Rotate(aim, i * 360f / Wheels)));
    }

    private IEnumerator Wheel(Vector2 dir)
    {
        float reach = Area(7f), size = Area(2.4f), beamLength = Area(3.5f), pull = Area(3.5f);
        float damage = Dmg(12f), tick = Dmg(8f);
        Shot wheel = Shot.Spawn(Shapes.Art("Icons/boomerang"), new Color(1f, 0.7f, 0.4f), size, Pos, 27);
        wheel.life = 10f;
        wheel.spin = -900f;
        wheel.ghost = new Color(1f, 0.35f, 0.1f, 0.4f);
        wheel.hitRadius = size * 0.4f;
        wheel.onHit = (s, e) => { e.MarkBurning(); Hit(e, Dmg(30f), s.Position); };
        int gen = wheel.Generation;

        var beams = new Shot[8]; // 4 beams, halo + core each
        var beamGens = new int[8];
        for (int b = 0; b < 8; b++)
        {
            bool core = b % 2 == 1;
            beams[b] = Shot.Spawn(Shapes.Square, core ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.4f, 0.1f, 0.4f), 1f, Pos, core ? 46 : 45);
            beams[b].life = 10f;
            beamGens[b] = beams[b].Generation;
        }

        const float outTime = 1f, total = 2.2f;
        float hitTick = 0f, fireTick = 0f;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float k = t < outTime ? Mathf.Sin(t / outTime * Mathf.PI * 0.5f) : 1f - (t - outTime) / (total - outTime);
            Vector2 at = Pos + dir * reach * k;
            if (wheel.IsSame(gen)) wheel.transform.position = at;
            if (Mathf.Abs(t - outTime) < Time.deltaTime && wheel.IsSame(gen)) wheel.ClearHits(); // cuts again on the way home

            // gravity: cats are dragged toward the wheel
            foreach (Collider2D c in EnemiesNear(at, pull))
            {
                if (!c.TryGetComponent(out EnemyAI cat)) continue;
                Vector2 to = at - (Vector2)c.transform.position;
                if (to.sqrMagnitude > 0.04f) cat.Pull(to.normalized * 5f);
            }

            bool hit = (hitTick -= Time.deltaTime) <= 0f;
            if (hit) hitTick = 0.12f;
            float spin = t * 300f;
            for (int side = 0; side < 4; side++)
            {
                Vector2 beamDir = Rotate(Vector2.right, spin + side * 90f);
                float rot = spin + side * 90f;
                for (int layer = 0; layer < 2; layer++)
                {
                    Shot beam = beams[side * 2 + layer];
                    if (!beam.IsSame(beamGens[side * 2 + layer])) continue;
                    beam.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, rot));
                    beam.transform.localScale = new Vector3(beamLength, layer == 1 ? 0.14f : 0.45f, 1f);
                }
                if (hit) HitLine(at, beamDir, beamLength, 0.45f, damage, e => e.MarkBurning());
            }

            if ((fireTick -= Time.deltaTime) <= 0f) // burning trail
            {
                fireTick = 0.18f;
                BurningGround.Spawn(at, Area(0.8f), 2.5f, tick, burns: true);
            }
            Juice.SparksDir(at, Vector2.up, 40f, Random.value < 0.5f ? Fire : new Color(1f, 0.85f, 0.35f), 1, 3f, 0.16f);
            yield return null;
        }

        if (wheel.IsSame(gen)) { Juice.Sparks(wheel.Position, Fire, 6, 3f, 0.12f); wheel.Release(); }
        for (int b = 0; b < 8; b++) if (beams[b].IsSame(beamGens[b])) beams[b].Release();
    }
}
