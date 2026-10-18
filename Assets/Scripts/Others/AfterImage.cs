using System.Collections.Generic;
using UnityEngine;

public class AfterImage : MonoBehaviour
{
    private const float Duration = 0.25f;
    private static readonly Color DefaultTint = new Color(0.5f, 0.8f, 1f, 0.6f);
    private static readonly Stack<AfterImage> free = new Stack<AfterImage>();

    private SpriteRenderer sr;
    private Color tint;
    private float t;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => free.Clear();

    public static void Spawn(SpriteRenderer source, Color? tint = null)
    {
        AfterImage ghost = null;
        while (free.Count > 0 && ghost == null) ghost = free.Pop();
        if (ghost != null) ghost.gameObject.SetActive(true);
        else
        {
            var go = new GameObject("AfterImage");
            ghost = go.AddComponent<AfterImage>();
            ghost.sr = go.AddComponent<SpriteRenderer>();
            Lighting.Glow(ghost.sr);
        }

        ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        ghost.transform.localScale = source.transform.lossyScale;
        ghost.tint = tint ?? DefaultTint;
        ghost.t = 0f;
        ghost.sr.sprite = source.sprite;
        ghost.sr.flipX = source.flipX;
        ghost.sr.sortingLayerID = source.sortingLayerID;
        ghost.sr.sortingOrder = source.sortingOrder - 1;
        ghost.sr.color = ghost.tint;
    }

    private void Update()
    {
        t += Time.deltaTime / Duration;
        sr.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - t));
        if (t < 1f) return;
        gameObject.SetActive(false);
        free.Push(this);
    }
}
