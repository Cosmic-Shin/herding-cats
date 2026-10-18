using System.Collections.Generic;
using UnityEngine;

public class TeslaCoilsWeapon : OrbsWeapon
{
    private const float ZapEvery = 0.7f;
    private const float ZapRange = 3f;
    private const float ZapDamage = 22f;
    private const float LinkDamage = 10f;
    private const float LinkTick = 0.3f;
    private const float LinkWidth = 0.35f;

    private readonly List<SpriteRenderer> links = new List<SpriteRenderer>();
    private static readonly List<Collider2D> boxHits = new List<Collider2D>(64);
    private float zapTimer, linkTimer;

    protected override void OnLevelChanged()
    {
        base.OnLevelChanged();
        foreach (Transform orb in orbs) orb.GetComponent<SpriteRenderer>().color = new Color(1f, 0.95f, 0.5f);
    }

    protected override void Update()
    {
        base.Update();
        int n = orbs.Count;
        if (n < 2) return;

        linkTimer -= Time.deltaTime;
        bool tick = linkTimer <= 0f;
        if (tick) linkTimer = LinkTick;
        float linkDamage = Dmg(LinkDamage);
        for (int i = 0; i < n; i++)
        {
            Vector2 a = orbs[i].position, b = orbs[(i + 1) % n].position;
            DrawLink(i, a, b);
            if (Random.value < Time.deltaTime * 6f) LightningFx.SpawnBetween(a, b, null, 0.6f);
            if (tick) DamageLink(a, b, linkDamage);
        }

        zapTimer -= Time.deltaTime;
        if (zapTimer > 0f) return;
        zapTimer = ZapEvery / Mathf.Max(0.1f, stats.attackSpeedMultiplier);

        float zap = Dmg(ZapDamage), reach = Area(ZapRange);
        foreach (Transform orb in orbs)
        {
            Transform target = NearestTo(orb.position, reach);
            if (target == null || !target.TryGetComponent(out EnemyHealth e)) continue;
            LightningFx.SpawnBetween(orb.position, target.position);
            Sfx.Play(Sfx.Id.Zap, 0.15f, 1.6f);
            Hit(e, zap, orb.position);
        }
    }

    private void DamageLink(Vector2 a, Vector2 b, float damage)
    {
        Vector2 d = b - a;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = Bomb.EnemyMask, useTriggers = true };
        Physics2D.OverlapBox((a + b) / 2f, new Vector2(d.magnitude, LinkWidth), Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, filter, boxHits);
        foreach (Collider2D c in boxHits)
            if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, a, knockback: false);
    }

    private void DrawLink(int index, Vector2 a, Vector2 b)
    {
        while (links.Count <= index)
        {
            var go = new GameObject("TeslaLink");
            var sr = go.AddComponent<SpriteRenderer>();
            Lighting.Glow(sr);
            sr.sprite = Shapes.Square;
            sr.sortingOrder = 14;
            links.Add(sr);
        }

        Vector2 d = b - a;
        SpriteRenderer link = links[index];
        link.gameObject.SetActive(true);
        link.transform.position = a;
        link.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        link.transform.localScale = new Vector3(d.magnitude, 0.08f + 0.06f * Random.value, 1f);
        link.color = new Color(1f, 1f, 0.6f, 0.5f + 0.4f * Random.value);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        foreach (SpriteRenderer l in links) if (l != null) l.gameObject.SetActive(false);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (SpriteRenderer l in links) if (l != null) Destroy(l.gameObject);
    }
}
