using UnityEngine;

public class CompanionPickup : MonoBehaviour
{
    private const float PickupRadius = 0.8f;

    private Sprite[] sleep, walk, idle;
    private SpriteRenderer sr;
    private Transform player;
    private Vector3 basePos;
    private float t;

    public static void Drop(Vector2 at, EnemyAnimator catSprites)
    {
        at = MapGenerator.PlaceDrop(at);
        var go = new GameObject("CatBuddyPickup");
        go.transform.position = at;
        var p = go.AddComponent<CompanionPickup>();
        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.color = Companion.Gold;
        p.sr.sortingOrder = 8;
        p.sleep = catSprites.sleep;
        p.walk = catSprites.walk;
        p.idle = catSprites.idle;
        p.basePos = at;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) p.player = player.transform;
        DamagePopup.SpawnText(at + Vector2.up * 0.6f, "?", Companion.Gold, 6f);
    }

    private void Update()
    {
        t += Time.deltaTime;
        transform.position = basePos + Vector3.up * Mathf.Sin(t * 3f) * 0.05f;
        if (sleep != null && sleep.Length > 0) sr.sprite = sleep[(int)(t * 4f) % sleep.Length];

        if (Mathf.Repeat(t, 0.8f) < Time.deltaTime) ExplosionFx.Spawn(basePos, 0.5f, Companion.Gold);

        if (player == null || Companion.Instance != null) return;
        if (Vector2.Distance(player.position, basePos) > PickupRadius) return;

        Sfx.Play(Sfx.Id.Meow, 0.8f);
        Sfx.Play(Sfx.Id.Ready, 0.6f);
        Companion.Join(basePos, walk, idle, player.gameObject);
        Destroy(gameObject);
    }
}
