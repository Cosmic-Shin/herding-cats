using System.Collections;
using UnityEngine;

public class LaserDiscsWeapon : SuperWeapon
{
    private const int Discs = 4;
    private static readonly Color Magenta = new Color(1f, 0.35f, 0.75f);

    private void Update()
    {
        if (!Ready(4.5f)) return;
        Sfx.Play(Sfx.Id.Swish, 0.8f, 0.8f);
        float turn = Random.Range(0f, 90f);
        for (int i = 0; i < Discs; i++) StartCoroutine(Disc(Rotate(Vector2.right, turn + i * 360f / Discs)));
    }

    private IEnumerator Disc(Vector2 dir)
    {
        float reach = Area(6.5f), beamLength = Area(3.8f), damage = Dmg(10f);
        Shot disc = Shot.Spawn(Shapes.LaserDisc, Color.white, Area(0.95f), Pos, 47);
        disc.life = 10f;
        disc.spin = 720f;
        disc.trail = new Color(1f, 0.5f, 0.85f, 0.6f);
        disc.trailSize = 0.25f;
        disc.ghost = new Color(1f, 0.4f, 0.8f, 0.35f);
        int gen = disc.Generation;
        Shot glow = Shot.Spawn(Shapes.Circle, new Color(1f, 0.5f, 0.85f, 0.3f), Area(1.5f), Pos, 26);
        glow.life = 10f;
        int glowGen = glow.Generation;
        var beams = new Shot[4];
        var beamGens = new int[4];
        for (int b = 0; b < 4; b++)
        {
            bool core = b % 2 == 1;
            beams[b] = Shot.Spawn(Shapes.Square, core ? new Color(1f, 0.85f, 0.95f) : new Color(1f, 0.35f, 0.75f, 0.35f), 1f, Pos, core ? 46 : 45);
            beams[b].life = 10f;
            beamGens[b] = beams[b].Generation;
        }

        const float outTime = 0.9f, total = 1.9f;
        float tick = 0f;
        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            float k = t < outTime ? Mathf.Sin(t / outTime * Mathf.PI * 0.5f) : 1f - (t - outTime) / (total - outTime);
            Vector2 at = Pos + dir * reach * k;
            if (disc.IsSame(gen)) disc.transform.position = at;
            if (glow.IsSame(glowGen)) glow.transform.position = at;

            float angle = t * 400f;
            bool hit = (tick -= Time.deltaTime) <= 0f;
            if (hit) tick = 0.12f;
            for (int side = 0; side < 2; side++)
            {
                Vector2 beamDir = Rotate(Vector2.right, angle + side * 180f);
                float rot = Mathf.Atan2(beamDir.y, beamDir.x) * Mathf.Rad2Deg;
                for (int layer = 0; layer < 2; layer++)
                {
                    Shot beam = beams[side * 2 + layer];
                    if (!beam.IsSame(beamGens[side * 2 + layer])) continue;
                    beam.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, rot));
                    beam.transform.localScale = new Vector3(beamLength, layer == 1 ? 0.14f : 0.5f, 1f);
                }
                if (hit) HitLine(at, beamDir, beamLength, 0.45f, damage);
            }
            yield return null;
        }

        if (disc.IsSame(gen)) disc.Release();
        if (glow.IsSame(glowGen)) glow.Release();
        for (int b = 0; b < 4; b++) if (beams[b].IsSame(beamGens[b])) beams[b].Release();
    }
}
