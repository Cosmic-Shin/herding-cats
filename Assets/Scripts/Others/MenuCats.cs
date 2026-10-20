using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class MenuCats : MonoBehaviour
{
    public int count = 11;
    public Vector2 area = new Vector2(8f, 4.5f);
    public Rect hiddenBehindMenu = new Rect(-3f, -4f, 6f, 6f);

    private enum Mode { Sleep, Wander, Hop, Sit }

    private class Cat
    {
        public SpriteRenderer sr;
        public EnemyAnimator clips;
        public Mode mode;
        public Sprite[] frames;
        public float fps, frameTime, modeTimer, hopPhase, zTimer;
        public Vector2 pos, target;
    }

    private Cat[] cats;

    private void Start()
    {
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null || spawner.enemyWaves.Count == 0) return;
        var looks = new System.Collections.Generic.List<EnemyAnimator>();
        foreach (EnemySpawner.EnemyWave w in spawner.enemyWaves)
            if (w.enemyPrefab != null && w.enemyPrefab.TryGetComponent(out EnemyAnimator a) && a.walk != null && a.walk.Length > 0) looks.Add(a);
        if (looks.Count == 0) return;

        cats = new Cat[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("MenuCat");
            go.transform.SetParent(transform, false);
            var c = new Cat { sr = go.AddComponent<SpriteRenderer>(), clips = looks[i % looks.Count] };
            c.sr.sortingOrder = 5;
            SpriteFx.AddShadow(c.sr);
            c.pos = RandomSpot();
            Pick(c, i < count / 3 ? Mode.Sleep : (Mode)Random.Range(1, 4));
            cats[i] = c;
        }
    }

    private Vector2 RandomSpot()
    {
        for (int i = 0; i < 20; i++)
        {
            var p = new Vector2(Random.Range(-area.x, area.x), Random.Range(-area.y, area.y));
            if (Open(p)) return p;
        }
        return new Vector2(-6f, 2f);
    }

    private bool Open(Vector2 p) =>
        !hiddenBehindMenu.Contains(p) && Mathf.Abs(p.x) <= area.x && Mathf.Abs(p.y) <= area.y &&
        Physics2D.OverlapCircle((Vector2)transform.position + p, 0.5f, MapGenerator.ObstacleMask) == null;

    private Vector2 Near(Vector2 from)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 p = from + Random.insideUnitCircle * 2.5f;
            if (Open(p)) return p;
        }
        return from;
    }

    private void Pick(Cat c, Mode mode)
    {
        c.mode = mode;
        switch (mode)
        {
            case Mode.Sleep: Play(c, c.clips.sleep, 3f); c.modeTimer = Random.Range(6f, 14f); break;
            case Mode.Sit: Play(c, c.clips.idle, 6f); c.modeTimer = Random.Range(1.5f, 4f); break;
            case Mode.Wander: Play(c, c.clips.walk, 8f); c.target = RandomSpot(); c.modeTimer = 8f; break;
            case Mode.Hop: Play(c, c.clips.jump, 10f); c.target = Near(c.pos); c.modeTimer = Random.Range(2f, 4f); break;
        }
    }

    private static void Play(Cat c, Sprite[] frames, float fps)
    {
        c.frames = frames != null && frames.Length > 0 ? frames : c.clips.walk;
        c.fps = fps;
        c.frameTime = 0f;
    }

    private void Update()
    {
        if (cats == null) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        PetOnClick();
        foreach (Cat c in cats)
        {
            c.frameTime += dt;
            c.modeTimer -= dt;
            float lift = 0f;
            switch (c.mode)
            {
                case Mode.Wander:
                    c.pos = Vector2.MoveTowards(c.pos, c.target, 1.3f * dt);
                    if ((c.pos - c.target).sqrMagnitude < 0.01f || c.modeTimer <= 0f) Pick(c, Random.value < 0.6f ? Mode.Sit : Mode.Hop);
                    break;
                case Mode.Hop:
                    c.hopPhase += dt * 3.5f;
                    lift = Mathf.Abs(Mathf.Sin(c.hopPhase * Mathf.PI)) * 0.35f;
                    c.pos = Vector2.MoveTowards(c.pos, c.target, 1.8f * dt);
                    if ((c.pos - c.target).sqrMagnitude < 0.01f) c.target = Near(c.pos);
                    if (c.modeTimer <= 0f) Pick(c, Mode.Sit);
                    break;
                case Mode.Sleep:
                    if (c.modeTimer <= 0f) Pick(c, Mode.Wander);
                    break;
                case Mode.Sit:
                    if (c.modeTimer <= 0f) Pick(c, Random.value < 0.25f ? Mode.Sleep : Random.value < 0.6f ? Mode.Wander : Mode.Hop);
                    break;
            }
            c.pos = new Vector2(Mathf.Clamp(c.pos.x, -area.x, area.x), Mathf.Clamp(c.pos.y, -area.y, area.y));
            if (c.mode == Mode.Wander || c.mode == Mode.Hop)
            {
                float dx = c.target.x - c.pos.x;
                if (Mathf.Abs(dx) > 0.05f) c.sr.flipX = dx < 0f;
            }
            c.sr.sprite = SpriteFx.Outlined(SpriteFx.Skinned(c.frames[(int)(c.frameTime * c.fps) % c.frames.Length], c.clips.skin));
            if (c.mode == Mode.Sleep && (c.zTimer -= dt) <= 0f)
            {
                c.zTimer = Random.Range(1.6f, 2.6f);
                DamagePopup.SpawnText((Vector2)c.sr.transform.position + new Vector2(0.2f, 0.3f), "z", new Color(0.7f, 0.85f, 1f), 2.5f);
            }
            c.sr.transform.localPosition = new Vector3(c.pos.x, c.pos.y + lift, 0f);
        }
    }

    private static readonly string[] PetLines = { "mrrp!", "purr~", "nya!", "mew!" };

    // Click a menu cat: it hops and meows.
    private void PetOnClick()
    {
        if (cats == null || !MainMenu.IsOpen || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || Camera.main == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        Vector2 at = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        foreach (Cat c in cats)
        {
            if (((Vector2)c.sr.transform.position - at).sqrMagnitude > 0.5f) continue;
            Pick(c, Mode.Hop);
            DamagePopup.SpawnText((Vector2)c.sr.transform.position + Vector2.up * 0.5f, PetLines[Random.Range(0, PetLines.Length)], new Color(1f, 0.6f, 0.85f), 3f);
            Sfx.Play(Sfx.Id.Meow, 0.5f, Random.Range(0.95f, 1.2f));
            return;
        }
    }
}
