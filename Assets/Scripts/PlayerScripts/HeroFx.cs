using UnityEngine;

// Small visual building blocks for Kai's and Nyx's effects: ribbon trails, impact slash marks,
// digital "data bit" bursts, glitch ghosts, a quick bright flash core and a lock-on reticle sprite.
public static class HeroFx
{
    private static Sprite reticle, shard;

    // A ribbon trail (Unity TrailRenderer, unlit) fading from `color` to clear. Starts off; toggle `emitting`.
    public static TrailRenderer Trail(Transform parent, Vector3 localPos, float width, Color color, float time, int order)
    {
        var go = new GameObject("Trail");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var t = go.AddComponent<TrailRenderer>();
        Lighting.Glow(t);
        t.time = time;
        t.minVertexDistance = 0.04f;
        t.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        t.widthMultiplier = width;
        t.colorGradient = Fade(color);
        t.sortingOrder = order;
        t.numCapVertices = 2;
        t.emitting = false;
        return t;
    }

    public static Gradient Fade(Color color)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.6f), 0f), new GradientColorKey(color, 0.4f), new GradientColorKey(color, 1f) },
                  new[] { new GradientAlphaKey(Settings.EffectAlpha, 0f), new GradientAlphaKey(0.6f * Settings.EffectAlpha, 0.4f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // A thin bright cut across a hit point that flares and thins out.
    public static void SlashMark(Vector2 at, Vector2 dir, Color color, float length = 0.9f, float thickness = 0.09f)
    {
        const float life = 0.16f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        Shot s = Shot.Spawn(Shapes.Square, color, 1f, at - dir * length * 0.5f, 66);
        s.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        s.transform.localScale = new Vector3(length * 0.6f, thickness, 1f); // set now: Shot only applies onUpdate from next frame
        s.life = life;
        s.onUpdate = shot =>
        {
            float k = shot.age / life;
            shot.transform.localScale = new Vector3(length * (0.6f + 0.6f * k), thickness * (1f - k), 1f); // Shapes sprites are 1 unit
            shot.Renderer.color = new Color(color.r, color.g, color.b, Settings.EffectAlpha * (1f - k * k));
        };
    }

    // Little square "data bits" that burst out and shrink.
    public static void Bits(Vector2 at, Color a, Color b, int count, float speed = 4f, float size = 0.09f)
    {
        if (Settings.LowParticles) count = Mathf.Max(1, count / 2);
        for (int i = 0; i < count; i++)
        {
            Color c = Random.value < 0.5f ? a : b;
            float life = Random.Range(0.25f, 0.45f), s0 = size * Random.Range(0.7f, 1.3f);
            Shot s = Shot.Spawn(Shapes.Square, c, s0, at, 64);
            s.velocity = Random.insideUnitCircle.normalized * speed * Random.Range(0.4f, 1f);
            s.life = life;
            s.onUpdate = shot =>
            {
                float k = shot.age / life;
                shot.velocity *= 1f - 4f * Time.deltaTime;
                shot.transform.localScale = Vector3.one * s0 * (1f - k);
                // flicker like a bad signal
                shot.Renderer.color = new Color(c.r, c.g, c.b, Settings.EffectAlpha * (Random.value < 0.15f ? 0.2f : 1f));
            };
        }
    }

    // A bright core that pops and shrinks (the centre of a zap or impact).
    public static void Pop(Vector2 at, Color color, float size)
    {
        const float life = 0.12f;
        Shot s = Shot.Spawn(Shapes.Circle, color, size, at, 63);
        s.life = life;
        s.onUpdate = shot =>
        {
            float k = shot.age / life;
            shot.transform.localScale = Vector3.one * size * (1f + 0.4f * k);
            shot.Renderer.color = new Color(1f, 1f, 1f, Settings.EffectAlpha * (1f - k));
        };
    }

    // RGB-split glitch: two tinted copies of a sprite jittered sideways for a frame or two.
    public static void Glitch(SpriteRenderer src, float offset = 0.07f)
    {
        if (src == null || src.sprite == null || Settings.LowParticles) return;
        foreach ((Color c, float side) in new[] { (new Color(1f, 0.2f, 0.8f, 0.6f), -1f), (new Color(0.2f, 0.95f, 1f, 0.6f), 1f) })
        {
            Shot s = Shot.Spawn(src.sprite, c, 1f, (Vector2)src.transform.position + new Vector2(side * offset * Random.Range(0.6f, 1.4f), Random.Range(-0.02f, 0.02f)), src.sortingOrder + 1);
            s.transform.localScale = src.transform.lossyScale;
            s.Renderer.flipX = src.flipX;
            s.life = 0.07f;
        }
    }

    // Lock-on crosshair: four corner brackets and a centre dot. White, so it can be tinted.
    public static Sprite Reticle
    {
        get
        {
            if (reticle != null) return reticle;
            const int n = 15;
            var tex = new Texture2D(n, n) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool edgeX = x == 0 || x == n - 1, edgeY = y == 0 || y == n - 1;
                    bool nearX = x < 4 || x > n - 5, nearY = y < 4 || y > n - 5;
                    bool bracket = (edgeX && nearY) || (edgeY && nearX);
                    bool dot = Mathf.Abs(x - n / 2) <= 0 && Mathf.Abs(y - n / 2) <= 0;
                    bool tick = (x == n / 2 && (y == 2 || y == n - 3)) || (y == n / 2 && (x == 2 || x == n - 3));
                    tex.SetPixel(x, y, bracket || dot || tick ? Color.white : Color.clear);
                }
            tex.Apply();
            return reticle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 16f);
        }
    }

    // A small steel shard (Kai's Shrapnel).
    public static Sprite Shard
    {
        get
        {
            if (shard != null) return shard;
            string[] rows = { "..#", ".#+", "#+." };
            var tex = new Texture2D(3, 3) { filterMode = FilterMode.Point };
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    tex.SetPixel(x, 2 - y, rows[y][x] == '#' ? Color.white : rows[y][x] == '+' ? new Color(0.7f, 0.74f, 0.82f) : Color.clear);
            tex.Apply();
            return shard = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 20f);
        }
    }
}
