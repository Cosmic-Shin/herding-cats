using UnityEngine;

public class Crate : MonoBehaviour
{
    public static int Count { get; private set; }
    private static Sprite sprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Count = 0;

    private void OnEnable() => Count++;
    private void OnDisable() => Count--;

    public static void Spawn(Vector2 pos, float health)
    {
        pos = MapGenerator.PlaceDrop(pos);
        var go = new GameObject("Crate");
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.8f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSprite();
        sr.sortingOrder = 4;
        go.AddComponent<CircleCollider2D>().isTrigger = true;
        go.AddComponent<Crate>();
        var h = go.AddComponent<EnemyHealth>();
        h.ScaleHealth(health / h.maxHealth);
        h.burgerDropChance = 0f;
        h.knockbackForce = 0f;
        h.hitFlashColor = new Color(1f, 0.85f, 0.6f);
    }

    public void Break()
    {
        Vector2 pos = transform.position;
        Sfx.Play(Sfx.Id.Crate, 1f);
        Sfx.Play(Sfx.Id.Pop, 0.5f, 0.7f);
        Juice.CatPuff(pos, new Color(0.7f, 0.45f, 0.25f));
        Juice.Sparks(pos, new Color(0.8f, 0.55f, 0.3f), 6, 4f, 0.15f);

        float r = Random.value;
        if (Random.value < (Pickup.AnvilChanceMultiplier - 1f) * 0.05f) Pickup.Spawn(Pickup.Kind.Anvil, pos);
        else if (r < 0.3f) Burger.Spawn(pos);
        else if (r < 0.52f) Pickup.Spawn(Pickup.Kind.Magnet, pos);
        else if (r < 0.74f) Pickup.Spawn(Pickup.Kind.Speed, pos);
        else if (r < 0.96f || Pickup.HoloBoxCount >= Pickup.MaxHoloBoxes) Pickup.Spawn(Pickup.Kind.Bomb, pos);
        else Pickup.Spawn(Pickup.Kind.HoloBox, pos);

        Destroy(gameObject);
    }

    private static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        const int n = 16;
        Color frame = new Color(0.36f, 0.2f, 0.1f), wood = new Color(0.72f, 0.47f, 0.24f), line = new Color(0.55f, 0.33f, 0.16f);
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool edge = x <= 1 || y <= 1 || x >= n - 2 || y >= n - 2;
                bool brace = Mathf.Abs(x - y) <= 1;
                bool plank = y == 6 || y == 10;
                tex.SetPixel(x, y, edge || brace ? frame : plank ? line : wood);
            }
        tex.Apply();
        sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return sprite;
    }
}
