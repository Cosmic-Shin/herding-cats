using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public class KaiSword : MonoBehaviour
{
    public const string Source = "Greatsword";
    public static KaiSword Active { get; private set; }

    public float swingInterval = 1.8f;
    public float reach = 2.6f;
    public float arc = 200f;
    public float damageMultiplier = 0.85f;
    public float followUpDamage = 0.85f;
    public float push = 6f;

    private static readonly Color Edge = new Color(1f, 0.95f, 0.9f), Ember = new Color(1f, 0.35f, 0.2f);
    private static readonly List<Collider2D> hits = new List<Collider2D>(64), near = new List<Collider2D>(64);
    private static Sprite bladeSprite;
    private static float nextHitStop, nextExecutePopup;

    private PlayerStats stats;
    private PlayerController controller;
    private PlayerAnimator anim;
    private PlayerUltimate ult;
    private PlayerSkills skills;
    private SpriteRenderer blade;
    private bool sweeping;
    private float timer, restAngle = 225f, echo, spinLeft, spinAngle;
    private bool swinging;
    private static readonly Color EchoBlue = new Color(0.55f, 0.8f, 1f), CritGold = new Color(1f, 0.85f, 0.35f), Steel = new Color(0.85f, 0.9f, 1f);

    private float Fury => PlayerCollabs.Has("furyfuse") && stats.maxHealth > 0f ? 1f - Mathf.Clamp01(stats.currentHealth / stats.maxHealth) : 0f;
    public float Reach => reach * stats.areaMultiplier * (ult != null ? ult.RadiusMultiplier : 1f) * (1f + 0.5f * Fury);
    public float SwingDamage => stats.AttackDamage * damageMultiplier * (ult != null ? ult.DamageMultiplier : 1f);

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
        anim = GetComponent<PlayerAnimator>();
        ult = GetComponent<PlayerUltimate>();
        skills = GetComponent<PlayerSkills>();
        blade = new GameObject("Greatsword").AddComponent<SpriteRenderer>();
        blade.sprite = BladeSprite;
        BuildSmear();
    }

    private const int SmearSegments = 28;
    private static readonly float[] SmearRadius = { 0.28f, 0.72f, 0.93f, 1f, 1.06f };
    private static readonly float[] SmearAlpha = { 0f, 0.28f, 0.75f, 1f, 0f };
    private static readonly float[] SmearWhite = { 0f, 0.1f, 0.45f, 0.85f, 0.85f };
    private Mesh smear;
    private MeshRenderer smearRenderer;
    private Vector3[] smearVerts;
    private Color[] smearColors;
    private Color smearColor = Steel;
    private float smearTail, smearTip, smearFade;

    private void BuildSmear()
    {
        var go = new GameObject("SwingSmear");
        smear = new Mesh { name = "SwingSmear" };
        smear.MarkDynamic();
        int rings = SmearRadius.Length, n = (SmearSegments + 1) * rings;
        smearVerts = new Vector3[n];
        smearColors = new Color[n];
        var tris = new int[SmearSegments * (rings - 1) * 6];
        int t = 0;
        for (int s = 0; s < SmearSegments; s++)
            for (int j = 0; j < rings - 1; j++)
            {
                int a = s * rings + j, b = a + rings;
                tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
            }
        smear.vertices = smearVerts;
        smear.colors = smearColors;
        smear.triangles = tris;
        go.AddComponent<MeshFilter>().sharedMesh = smear;
        smearRenderer = go.AddComponent<MeshRenderer>();
        Lighting.Glow(smearRenderer);
        smearRenderer.sortingOrder = 71;
        smearRenderer.enabled = false;
    }

    private void DrawSmear(float tail, float tip, float alpha)
    {
        if (smear == null) return;
        smearRenderer.enabled = alpha > 0.01f;
        if (!smearRenderer.enabled) return;
        Vector2 pos = transform.position;
        float r = Reach, a = alpha * Settings.EffectAlpha;
        int rings = SmearRadius.Length;
        for (int s = 0; s <= SmearSegments; s++)
        {
            float k = s / (float)SmearSegments;
            float ang = Mathf.Lerp(tail, tip, k) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            float along = k * k * (3f - 2f * k);
            for (int j = 0; j < rings; j++)
            {
                int i = s * rings + j;
                smearVerts[i] = pos + d * r * SmearRadius[j];
                Color c = Color.Lerp(smearColor, Color.white, SmearWhite[j]);
                c.a = SmearAlpha[j] * along * a;
                smearColors[i] = c;
            }
        }
        smear.vertices = smearVerts;
        smear.colors = smearColors;
        smear.RecalculateBounds();
    }

    private void SmearSweep(float from, float to, float k, Color color)
    {
        smearColor = color;
        smearTail = Mathf.Lerp(from, to, Mathf.Max(0f, k - 0.55f));
        smearTip = Mathf.Lerp(from, to, k);
        smearFade = 1f;
        DrawSmear(smearTail, smearTip, 1f);
    }

    private void SmearFadeOut()
    {
        if (smearFade <= 0f) return;
        smearFade = Mathf.Max(0f, smearFade - Time.deltaTime / 0.12f);
        smearTail = Mathf.Lerp(smearTail, smearTip, Time.deltaTime * 18f);
        DrawSmear(smearTail, smearTip, smearFade * smearFade);
    }

    private void OnEnable()
    {
        Active = this;
        if (blade != null) blade.enabled = true;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        if (blade != null) blade.enabled = false;
        smearFade = 0f;
        sweeping = false;
        if (smearRenderer != null) smearRenderer.enabled = false;
        StopAllCoroutines();
        swinging = false;
    }

    private void OnDestroy()
    {
        if (blade != null) Destroy(blade.gameObject);
        if (smearRenderer != null) Destroy(smearRenderer.gameObject);
    }

    private void Update()
    {
        if (stats.currentHealth <= 0f) return;
        timer += Time.deltaTime;
        if (swinging || timer < swingInterval / Mathf.Max(0.1f, stats.AttackSpeed * (1f + Fury))) return;
        Transform target = Nearest(transform.position, Reach * 1.25f);
        if (target == null) return;
        timer = 0f;
        StartCoroutine(Attack(target.position));
    }

    private void LateUpdate()
    {
        if (!sweeping) SmearFadeOut();
        if (swinging) return;
        Vector2 pos = transform.position;
        if (spinLeft > 0f)
        {
            spinLeft -= Time.deltaTime;
            spinAngle -= 1400f * Time.deltaTime;
            SetBlade(pos, spinAngle, 72);
            if (spinLeft > 0f) { smearColor = Steel; smearTip = spinAngle; smearTail = spinAngle + 230f; smearFade = 1f; DrawSmear(smearTail, smearTip, 1f); }
            return;
        }
        Vector2 facing = controller != null ? controller.GetFacingDirection() : Vector2.down;
        float target = facing.x >= 0f ? 235f : 305f;
        restAngle = Mathf.LerpAngle(restAngle, target, Time.deltaTime * 12f);
        Vector2 shoulder = pos + new Vector2(facing.x >= 0f ? -0.1f : 0.1f, 0.35f);
        SetBlade(shoulder, restAngle, facing.y > 0.5f ? 72 : 68);
    }

    private void SetBlade(Vector2 at, float angle, int order)
    {
        blade.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, angle - 90f));
        blade.transform.localScale = Vector3.one * (Reach / reach);
        blade.sortingOrder = order;
    }

    private IEnumerator Attack(Vector2 target)
    {
        swinging = true;
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        int count = Mathf.Max(1, stats.ProjectileCount) + (ult != null ? ult.ExtraBombs : 0);
        if (PlayerCollabs.Has("carpet")) StartCoroutine(BladeRain(dir));
        if (SuperWeapon.Active != null) SuperWeapon.Active.OnThrow(target);

        bool clockwise = true;
        for (int i = 0; i < count; i++)
        {
            yield return Swing(dir, i == 0 ? 1f : followUpDamage, clockwise, i == 0);
            clockwise = !clockwise;
            if (echo > 0f)
            {
                float e = echo;
                echo = 0f;
                yield return Swing(dir, e, clockwise, true, true);
                clockwise = !clockwise;
            }
            Transform next = Nearest(transform.position, Reach * 1.25f);
            if (next != null) dir = ((Vector2)next.position - (Vector2)transform.position).normalized;
        }
        swinging = false;
    }

    private IEnumerator Swing(Vector2 dir, float damageMult, bool clockwise, bool first, bool isEcho = false)
    {
        float center = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, half = arc / 2f * (clockwise ? 1f : -1f);
        float from = center + half, to = center - half;
        float pace = Mathf.Clamp(Mathf.Sqrt(stats.AttackSpeed * (1f + Fury)), 1f, 1.8f);
        float windup = (first ? 0.1f : 0.04f) / pace, sweep = (first ? 0.2f : 0.15f) / pace;
        if (anim != null) anim.PlayThrow(dir);
        if (PlayerCollabs.Has("magnetbomb")) Undertow(dir);

        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            SetBlade(transform.position, from + half * 0.15f * (t / windup), 72);
            yield return null;
        }
        Sfx.Play(Sfx.Id.Whoosh, 0.55f, isEcho ? 1.1f : 0.75f);
        Color slash = isEcho ? EchoBlue : ult != null && ult.IsActive ? PlayerUltimate.RageRed : Steel;
        sweeping = true;
        struck.Clear();
        biteSum = Vector2.zero;
        float prevK = 0f;
        for (float t = 0f; t < sweep; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / sweep, 3f);
            SetBlade(transform.position, Mathf.Lerp(from, to, k), 72);
            SmearSweep(from, to, k, slash);
            Cut(center, from, to, prevK, k, damageMult, isEcho);
            prevK = k;
            yield return null;
        }
        Cut(center, from, to, prevK, 1f, damageMult, isEcho);
        SwingEnd(dir, damageMult, isEcho);
        SmearSweep(from, to, 1f, slash);
        sweeping = false;
        if (anim != null) anim.Squash(new Vector2(1.2f, 0.85f), 0.15f);
        for (float t = 0f; t < 0.06f / pace; t += Time.deltaTime) yield return null;
    }

    private readonly HashSet<EnemyHealth> struck = new HashSet<EnemyHealth>();
    private Vector2 biteSum;
    private const float CatRadius = 0.35f;

    private void Cut(float center, float from, float to, float prevK, float k, float damageMult, bool isEcho)
    {
        Vector2 pos = transform.position;
        float r = Reach, damage = SwingDamage * damageMult;
        bool concussion = PlayerCollabs.Has("concussion");
        int before = struck.Count;
        DamageStats.Source = isEcho ? "Echo Slash" : Source;
        foreach (Collider2D c in Bomb.OverlapEnemies(pos, r + CatRadius, hits))
        {
            if (!c.TryGetComponent(out EnemyHealth e) || e.IsDead || struck.Contains(e)) continue;
            Vector2 off = (Vector2)c.transform.position - pos;
            float dist = off.magnitude;
            float angle = center + Mathf.DeltaAngle(center, Mathf.Atan2(off.y, off.x) * Mathf.Rad2Deg);
            float p = (from - angle) / (from - to);
            float slack = Mathf.Rad2Deg * Mathf.Atan2(CatRadius, Mathf.Max(dist, 0.3f)) / Mathf.Abs(from - to);
            if (p < prevK - slack || p > k + slack) continue;
            struck.Add(e);
            biteSum += (Vector2)c.transform.position;
            float shove = push * (concussion ? 1.6f : 1f) * Mathf.Clamp01(1.1f - dist / r);
            Hit(e, pos, damage, dist > 0.01f ? off / dist : Vector2.up, shove, concussion ? 0.6f : 0f);
        }
        if (before == 0 && struck.Count > 0)
        {
            Sfx.Play(Sfx.Id.Clang, 0.35f, Random.Range(0.55f, 0.7f));
            GamepadUI.Rumble(0.15f, 0.35f, 0.08f);
            if (Time.unscaledTime >= nextHitStop) { nextHitStop = Time.unscaledTime + 0.25f; Juice.HitStop(0.04f); }
        }
    }

    private void SwingEnd(Vector2 dir, float damageMult, bool isEcho)
    {
        Vector2 pos = transform.position;
        float r = Reach;
        int count = struck.Count;
        Vector2 bite = count > 0 ? biteSum / count : pos + dir * r * 0.6f;
        CameraFollow.Shake(0.05f + 0.02f * Mathf.Min(count, 5), 0.1f);
        if (count >= 3)
        {
            Juice.Smoke(bite, 0.8f, 3);
            ExplosionFx.Ring(bite, r * 0.5f, new Color(1f, 0.95f, 0.85f, 0.6f), 0.2f);
        }
        Vector2 aim = (bite - pos).sqrMagnitude > 0.01f ? (bite - pos).normalized : dir;
        SwordSkills(pos, aim, r, SwingDamage * damageMult, isEcho);
    }

    public void Hit(EnemyHealth e, Vector2 from, float damage, Vector2 pushDir, float pushForce, float extraStun = 0f)
    {
        KaiPassives kai = KaiPassives.Active;
        bool crit = Random.value < stats.critChance;
        float bonus = kai != null && e.IsImpaired ? kai.ImpairedBonus : 1f;
        e.TakeDamage(damage * bonus * (crit ? stats.critDamage : 1f) * Random.Range(0.9f, 1.1f), from, crit);

        Vector2 at = e.transform.position;
        Vector2 away = pushDir.sqrMagnitude > 0.01f ? pushDir : (at - from).normalized;
        HeroFx.SlashMark(at, Vector2.Perpendicular(away) + away * 0.4f, crit ? CritGold : Color.white, crit ? 1.2f : 0.85f, crit ? 0.13f : 0.09f);
        Juice.SparksDir(at, away, 35f, crit ? CritGold : Steel, crit ? 5 : 3, 6f, 0.09f);
        if (ult != null && ult.IsActive) stats.Heal(0.4f);
        if (e.IsDead) return;

        if (kai != null && !e.IsBoss && e.CurrentHealth <= e.maxHealth * kai.ExecuteBelow) { Execute(e, from, kai); return; }
        float stun = Mathf.Max(kai != null ? kai.StunTime : 0f, extraStun);
        if (stun > 0f && e.TryGetComponent(out EnemyAI cat)) { cat.Stun(stun); DizzyStars(e.transform, stun); }
        if (pushForce > 0f && e.TryGetComponent(out IEnemyBrain brain)) brain.Knockback(pushDir * pushForce);
    }

    private static void DizzyStars(Transform cat, float seconds)
    {
        if (Settings.LowParticles) return;
        seconds = Mathf.Min(seconds, 1f);
        for (int i = 0; i < 3; i++)
        {
            float offset = i * Mathf.PI * 2f / 3f;
            Shot s = Shot.Spawn(Shapes.Star, CritGold, 0.22f, cat.position, 67);
            s.life = seconds;
            s.onUpdate = shot =>
            {
                if (cat == null || !cat.gameObject.activeInHierarchy) { shot.Release(); return; }
                float a = shot.age * 9f + offset;
                shot.transform.position = (Vector2)cat.position + new Vector2(0.28f * Mathf.Cos(a), 0.42f + 0.09f * Mathf.Sin(a));
                shot.Renderer.color = new Color(1f, 0.85f, 0.35f, Settings.EffectAlpha * Mathf.Clamp01((seconds - shot.age) * 4f));
            };
        }
    }

    private void Execute(EnemyHealth e, Vector2 from, KaiPassives kai)
    {
        string source = DamageStats.Source;
        DamageStats.Source = "Execute";
        Vector2 at = e.transform.position;
        e.TakeDamage(e.CurrentHealth * 10f + 100f, from, true);
        DamageStats.Source = source;
        Vector2 back = (at - from).sqrMagnitude > 0.01f ? (at - from).normalized : Vector2.right;
        HeroFx.SlashMark(at, Quaternion.Euler(0f, 0f, 45f) * back, Ember, 1.4f, 0.16f);
        HeroFx.SlashMark(at, Quaternion.Euler(0f, 0f, -45f) * back, Ember, 1.4f, 0.16f);
        HeroFx.Pop(at, Ember, 0.9f);
        Lighting.Flash(at, 2.2f, Ember, 0.2f);
        Juice.SparksDir(at, back, 50f, Ember, 10, 9f, 0.13f);
        if (Time.unscaledTime >= nextExecutePopup)
        {
            nextExecutePopup = Time.unscaledTime + 0.4f;
            DamagePopup.SpawnText(at + Vector2.up * 0.4f, "EXECUTE", Ember, 3.5f);
        }
        if (kai.ExecuteHeal > 0f) stats.Heal(kai.ExecuteHeal);
    }

    private void SwordSkills(Vector2 pos, Vector2 dir, float r, float damage, bool isEcho)
    {
        if (skills != null)
        {
            int chain = skills.LevelOf("chain");
            if (!isEcho && chain > 0 && Random.value < new[] { 0.15f, 0.25f, 0.35f }[chain - 1]) echo = new[] { 0.5f, 0.6f, 0.75f }[chain - 1];

            int cluster = skills.LevelOf("cluster");
            if (cluster > 0) Shrapnel(pos + dir * r * 0.6f, cluster + 1, damage * new[] { 0.25f, 0.3f, 0.35f }[cluster - 1]);

            int napalm = skills.LevelOf("napalm");
            if (napalm > 0)
            {
                bool hot = PlayerCollabs.Has("firebomb");
                DamageStats.Source = "Scorched Arc";
                foreach (float a in new[] { -50f, 0f, 50f })
                    BurningGround.Spawn(pos + (Vector2)(Quaternion.Euler(0f, 0f, a) * dir) * r * 0.65f, r * (hot ? 0.45f : 0.3f),
                                        hot ? 5f : new[] { 2f, 2.5f, 3f }[napalm - 1], damage * (hot ? 0.22f : new[] { 0.08f, 0.12f, 0.16f }[napalm - 1]));
            }
        }
        if (SuperWeapon.Active != null) SuperWeapon.Active.OnBombBlast(pos + dir * r * 0.6f, r * 0.5f, damage);
    }

    private void Shrapnel(Vector2 from, int count, float damage)
    {
        Bomb.OverlapEnemies(from, 6f, near);
        DamageStats.Source = "Shrapnel";
        for (int i = 0; i < count; i++)
        {
            Vector2 at = near.Count > 0 ? (Vector2)near[Random.Range(0, near.Count)].transform.position : from + Random.insideUnitCircle.normalized * 3f;
            Shot s = Shot.Spawn(HeroFx.Shard, Color.white, 1.6f, from, 60);
            s.life = 0.16f;
            s.velocity = (at - from) / s.life;
            s.spin = 1440f;
            s.ghost = new Color(0.8f, 0.85f, 1f, 0.45f);
            s.trail = Steel;
            s.trailSize = 0.06f;
            s.onExpire = shot =>
            {
                Vector2 p = shot.Position;
                foreach (Collider2D c in Bomb.OverlapEnemies(p, 0.7f * stats.areaMultiplier, hits))
                    if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead) Hit(e, p, damage, ((Vector2)c.transform.position - p).normalized, 3f);
                Juice.Sparks(p, Steel, 5, 5f, 0.08f);
                Sfx.Play(Sfx.Id.Clang, 0.12f, Random.Range(1.4f, 1.7f));
            };
        }
    }

    private void Undertow(Vector2 dir)
    {
        Vector2 sweetSpot = (Vector2)transform.position + dir * Reach * 0.6f;
        foreach (Collider2D c in Bomb.OverlapEnemies(transform.position, Reach * 1.8f, hits))
            if (c.TryGetComponent(out IEnemyBrain brain)) brain.Knockback((sweetSpot - (Vector2)c.transform.position) * 3f);
    }

    private IEnumerator BladeRain(Vector2 dir)
    {
        Vector2 origin = transform.position;
        float damage = stats.AttackDamage * 0.45f * (ult != null ? ult.DamageMultiplier : 1f);
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSeconds(0.07f);
            Vector2 at = origin + dir * (1.6f + i) * stats.areaMultiplier;
            DamageStats.Source = "Blade Rain";
            Shot s = Shot.Spawn(BladeSprite, new Color(0.75f, 0.85f, 1f, 0.85f), 0.6f, at + Vector2.up * 2f, 60);
            s.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            s.life = 0.12f;
            s.velocity = Vector2.down * 2f / s.life;
            s.ghost = new Color(0.6f, 0.75f, 1f, 0.5f);
            s.onExpire = _ =>
            {
                float r = 0.9f * stats.areaMultiplier;
                foreach (Collider2D c in Bomb.OverlapEnemies(at, r, hits))
                    if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead) Hit(e, at, damage, ((Vector2)c.transform.position - at).normalized, 4f);
                HeroFx.Pop(at, EchoBlue, 0.7f);
                HeroFx.SlashMark(at + Vector2.up * 0.3f, Vector2.down, EchoBlue, 0.9f, 0.1f);
                ExplosionFx.Ring(at, r, EchoBlue, 0.22f);
                Juice.SparksDir(at, Vector2.up, 60f, EchoBlue, 5, 5f, 0.08f);
                Juice.Smoke(at, 0.5f, 2);
                Lighting.Flash(at, 1.6f, EchoBlue, 0.15f);
                Sfx.Play(Sfx.Id.Clang, 0.2f, 1.2f);
            };
        }
    }

    public void Spin(Vector2 at, float damageMult)
    {
        float r = Reach * 1.1f;
        DamageStats.Source = Source;
        foreach (Collider2D c in Bomb.OverlapEnemies(at, r, hits))
            if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead) Hit(e, at, SwingDamage * damageMult, ((Vector2)c.transform.position - at).normalized, push * 1.3f);
        bool here = Vector2.Distance(at, transform.position) < 0.5f;
        if (here) { spinLeft = 0.3f; spinAngle = restAngle; }
        else for (int i = 0; i < 2; i++) SlashFx(at, Quaternion.Euler(0f, 0f, i * 180f) * Vector2.right, r, Edge);
        ExplosionFx.Ring(at, r, Edge, 0.3f);
        ExplosionFx.Ring(at, r * 0.6f, new Color(1f, 1f, 1f, 0.5f), 0.2f);
        HeroFx.Pop(at, Edge, 1.2f);
        Juice.Smoke(at, r * 0.5f, 4);
        CameraFollow.Shake(0.15f, 0.15f);
        Sfx.Play(Sfx.Id.Whoosh, 0.8f, 0.6f);
    }

    private static void SlashFx(Vector2 pos, Vector2 dir, float r, Color color)
    {
        const float life = 0.2f;
        Shot s = Shot.Spawn(Shapes.Crescent, color, r * 1.25f, pos + dir * r * 0.45f, 65);
        s.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 211f);
        s.life = life;
        s.onUpdate = shot => shot.Renderer.color = new Color(color.r, color.g, color.b, 0.85f * (1f - shot.age / life));
    }

    private static Transform Nearest(Vector2 from, float range)
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (Collider2D c in Bomb.OverlapEnemies(from, range, near))
        {
            float d = ((Vector2)c.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c.transform; }
        }
        return best;
    }

    public static Sprite BladeSprite
    {
        get
        {
            if (bladeSprite != null) return bladeSprite;
            const int w = 14, h = 60;
            var fill = new Color[w, h];
            Color iron = new Color(0.4f, 0.42f, 0.48f), dark = new Color(0.27f, 0.28f, 0.33f), shine = new Color(0.74f, 0.76f, 0.82f);
            Color wrap = new Color(0.35f, 0.22f, 0.14f), wrapDark = new Color(0.22f, 0.13f, 0.08f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;
                    if (y < 2) { if (x >= 5 && x <= 8) c = dark; }
                    else if (y < 10) { if (x >= 5 && x <= 8) c = (y / 2) % 2 == 0 ? wrap : wrapDark; }
                    else if (y < 13) { if (x >= 1 && x <= 12) c = y == 12 ? iron : dark; }
                    else if (y < 57) { if (x >= 2 && x <= 11) c = x == 11 ? shine : x == 2 ? dark : ((x * 7 + y * 13) % 23 == 0 ? dark : iron); }
                    else if (x >= 2 + (y - 56) * 2 && x <= 11) c = x == 11 ? shine : iron;
                    fill[x, y] = c;
                }
            var tex = new Texture2D(w + 2, h + 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color ink = new Color(0.08f, 0.08f, 0.1f);
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && fill[x, y].a > 0f;
            for (int y = 0; y < h + 2; y++)
                for (int x = 0; x < w + 2; x++)
                {
                    int fx = x - 1, fy = y - 1;
                    tex.SetPixel(x, y, Solid(fx, fy) ? fill[fx, fy]
                        : Solid(fx - 1, fy) || Solid(fx + 1, fy) || Solid(fx, fy - 1) || Solid(fx, fy + 1) ? ink : Color.clear);
                }
            tex.Apply();
            return bladeSprite = Sprite.Create(tex, new Rect(0, 0, w + 2, h + 2), new Vector2(0.5f, 7f / (h + 2)), 22f);
        }
    }
}
