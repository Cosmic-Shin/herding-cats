using UnityEngine;
using UnityEngine.UI;

// Run-select screen: a little live window of the picked map: its sky and ground, with that map's ambience
// drifting through (meadow leaves and petals, winter snow and flakes, volcano embers, ash and a glowing lava strip).
// The menu pauses the game, so this runs on unscaled time.
public class MapPreviewUI : MonoBehaviour
{
    private class Mote { public RectTransform rt; public Image img; public Vector2 vel; public float age, life, spin, phase; }

    private const int Max = 40;
    private readonly Mote[] motes = new Mote[Max];
    private RectTransform area;
    private Image sky, ground, lava;
    private readonly Image[] trees = new Image[4];
    private int biome = -1;
    private float spawnTimer, spawnTimer2;

    public static MapPreviewUI Create(Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject("MapPreview", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.gameObject.AddComponent<RectMask2D>();
        var p = rt.gameObject.AddComponent<MapPreviewUI>();
        p.area = rt;
        p.sky = Fill(rt, "Sky", Vector2.zero, Vector2.one);
        p.ground = Fill(rt, "Ground", Vector2.zero, new Vector2(1f, 0.3f));
        p.lava = Fill(rt, "Lava", new Vector2(0.15f, 0.06f), new Vector2(0.55f, 0.18f));
        // A few tree silhouettes on the horizon (the map's own tree art, tinted per map).
        MapGenerator map = FindFirstObjectByType<MapGenerator>();
        float[] xs = { -190f, -95f, 120f, 205f };
        for (int i = 0; map != null && map.trees != null && map.trees.Length > 0 && i < xs.Length; i++)
        {
            var tree = new GameObject("Tree").AddComponent<Image>();
            tree.transform.SetParent(rt, false);
            tree.sprite = map.trees[i % map.trees.Length];
            tree.preserveAspect = true;
            tree.raycastTarget = false;
            tree.rectTransform.anchoredPosition = new Vector2(xs[i], -size.y * 0.5f + 52f + (i % 2) * 6f);
            tree.rectTransform.sizeDelta = new Vector2(70f, 80f + (i % 2) * 16f);
            p.trees[i] = tree;
        }
        for (int i = 0; i < Max; i++)
        {
            var img = new GameObject("Mote").AddComponent<Image>();
            img.transform.SetParent(rt, false);
            img.raycastTarget = false;
            img.gameObject.SetActive(false);
            p.motes[i] = new Mote { rt = img.rectTransform, img = img };
        }
        return p;
    }

    private static Image Fill(RectTransform parent, string name, Vector2 min, Vector2 max)
    {
        var img = new GameObject(name).AddComponent<Image>();
        img.transform.SetParent(parent, false);
        img.rectTransform.anchorMin = min;
        img.rectTransform.anchorMax = max;
        img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        img.raycastTarget = false;
        return img;
    }

    public void Show(int b)
    {
        if (b == biome) return;
        biome = b;
        foreach (Mote m in motes) m.img.gameObject.SetActive(false);
        sky.color = b == 1 ? new Color(0.62f, 0.72f, 0.86f) : b == 2 ? new Color(0.22f, 0.08f, 0.06f) : new Color(0.45f, 0.68f, 0.85f);
        ground.color = b == 1 ? new Color(0.86f, 0.9f, 0.96f) : b == 2 ? new Color(0.16f, 0.13f, 0.12f) : new Color(0.38f, 0.62f, 0.28f);
        lava.gameObject.SetActive(b == 2);
        Color silhouette = b == 1 ? new Color(0.45f, 0.52f, 0.66f) : b == 2 ? new Color(0.07f, 0.05f, 0.05f) : new Color(0.16f, 0.3f, 0.14f);
        foreach (Image t in trees) if (t != null) t.color = silhouette;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float w = area.rect.width, h = area.rect.height;
        if (biome == 2) lava.color = Color.Lerp(new Color(1f, 0.35f, 0.08f), new Color(1f, 0.7f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f));

        if ((spawnTimer -= dt) <= 0f)
        {
            spawnTimer = biome == 1 ? 0.06f : biome == 2 ? 0.1f : 0.22f;
            Spawn(false, w, h);
        }
        if ((spawnTimer2 -= dt) <= 0f)
        {
            spawnTimer2 = biome == 1 ? 0.45f : biome == 2 ? 0.08f : 0.35f;
            Spawn(true, w, h);
        }

        foreach (Mote m in motes)
        {
            if (!m.img.gameObject.activeSelf) continue;
            m.age += dt;
            float k = m.age / m.life;
            if (k >= 1f) { m.img.gameObject.SetActive(false); continue; }
            m.rt.anchoredPosition += (m.vel + new Vector2(Mathf.Sin(m.age * 2.5f + m.phase) * 12f, 0f)) * dt;
            m.rt.Rotate(0f, 0f, m.spin * dt);
            Color c = m.img.color;
            c.a = Mathf.Min(1f, k * 5f, (1f - k) * 4f) * (biome == 2 && m.spin == 0f ? 0.6f + 0.4f * Mathf.Sin(m.age * 12f + m.phase) : 0.9f);
            m.img.color = c;
        }
    }

    // `alt` is the second kind per map: petals / big flakes / ash.
    private void Spawn(bool alt, float w, float h)
    {
        Mote m = System.Array.Find(motes, x => !x.img.gameObject.activeSelf);
        if (m == null) return;
        m.age = 0f;
        m.phase = Random.value * 10f;
        m.rt.localRotation = Quaternion.identity;
        m.spin = Random.Range(-120f, 120f);
        Vector2 top = new Vector2(Random.Range(-w * 0.6f, w * 0.45f), h / 2f + 10f);
        if (biome == 2 && !alt) // embers rise from the lava
        {
            m.img.sprite = Shapes.Circle;
            m.rt.sizeDelta = Vector2.one * Random.Range(4f, 7f);
            m.img.color = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.85f, 0.35f), Random.value);
            m.rt.anchoredPosition = new Vector2(Random.Range(-w * 0.4f, w * 0.1f), -h / 2f + 15f);
            m.vel = new Vector2(Random.Range(-8f, 8f), Random.Range(35f, 60f));
            m.spin = 0f;
            m.life = h / m.vel.y;
        }
        else
        {
            m.rt.anchoredPosition = top;
            if (biome == 1)
            {
                m.img.sprite = alt ? Ambience.Flake : Shapes.Circle;
                m.rt.sizeDelta = alt ? Vector2.one * Random.Range(14f, 22f) : Vector2.one * Random.Range(3f, 6f);
                m.img.color = new Color(0.97f, 0.98f, 1f);
                m.vel = new Vector2(Random.Range(10f, 30f), Random.Range(-45f, -25f));
                if (!alt) m.spin = 0f;
            }
            else if (biome == 2) // ash
            {
                m.img.sprite = Shapes.Circle;
                m.rt.sizeDelta = Vector2.one * Random.Range(3f, 6f);
                m.img.color = new Color(0.55f, 0.5f, 0.48f);
                m.vel = new Vector2(Random.Range(5f, 20f), Random.Range(-30f, -15f));
            }
            else
            {
                m.img.sprite = alt ? Ambience.Petal : Ambience.Leaf;
                m.rt.sizeDelta = alt ? new Vector2(9f, 9f) : new Vector2(15f, 9f);
                m.img.color = alt ? new Color(1f, 0.78f, 0.86f) : Color.Lerp(new Color(0.45f, 0.75f, 0.3f), new Color(0.95f, 0.7f, 0.25f), Random.value);
                m.vel = new Vector2(Random.Range(15f, 35f), Random.Range(-35f, -20f));
            }
            m.life = (h + 20f) / -m.vel.y;
        }
        m.img.gameObject.SetActive(true);
    }
}
