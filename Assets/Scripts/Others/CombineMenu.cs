using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CombineMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private PlayerStats stats;
    private PlayerCollabs collabs;
    private LevelUpUIController levelUp;
    private PauseMenu pause;

    private Canvas canvas;
    private GameObject banner;
    private TextMeshProUGUI bannerText;
    private GameObject panel;
    private float timeScaleBefore = 1f;
    private float nextCheck;
    private bool anyReady;

    private void Awake() => IsOpen = false;

    private void Start()
    {
        stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            collabs = stats.GetComponent<PlayerCollabs>();
            levelUp = stats.GetComponent<LevelUpUIController>();
        }
        pause = FindFirstObjectByType<PauseMenu>();

        canvas = RuntimeUI.Canvas("CombineCanvas", 150);
        Button b = RuntimeUI.Button(canvas.transform, "Combine", new Vector2(0, 0), new Vector2(300, 42), Open);
        RectTransform rt = b.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -118);
        bannerText = b.GetComponentInChildren<TextMeshProUGUI>();
        banner = b.gameObject;
        banner.SetActive(false);
    }

    private void Update()
    {
        if (collabs == null) return;

        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 0.25f;
            anyReady = stats.currentHealth > 0f && collabs.Offerable().Count > 0;
        }

        banner.SetActive(anyReady && !IsOpen);
        if (banner.activeSelf)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            bannerText.text = $"<b><color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(new Color(1f, 0.75f, 0.2f), Color.white, pulse))}>COMBINE READY!</color></b>  <size=75%><color=#C9D2F0>{(GamepadUI.Connected ? "[Select]" : "[C]")}</color></size>";
        }

        bool toggle = (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                      || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
        if (IsOpen && GamepadUI.Back) toggle = true;
        if (!toggle) return;
        if (IsOpen) Close();
        else Open();
    }

    private bool CanOpen() => !MainMenu.IsOpen &&
        anyReady && stats.currentHealth > 0f && (levelUp == null || !levelUp.IsOpen) && (pause == null || !pause.IsPaused) && !RewardMenu.IsOpen && !GameOverScreen.IsShowing;

    private void Open()
    {
        if (IsOpen || !CanOpen()) return;
        List<PlayerCollabs.Def> ready = collabs.Offerable();
        if (ready.Count == 0) return;

        IsOpen = true;
        Juice.CancelHitStop();
        timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Sfx.Play(Sfx.Id.Ready, 0.6f);
        Time.timeScale = 0f;
        Build(ready);
    }

    private void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (panel != null) Destroy(panel);
        Time.timeScale = timeScaleBefore;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Combine(PlayerCollabs.Def def)
    {
        Close();
        Sfx.Play(Sfx.Id.Boom, 0.6f, 1.3f);
        collabs.Take(def);
        nextCheck = 0f;
    }

    private void Build(List<PlayerCollabs.Def> ready)
    {
        ready.Sort((x, y) => (y.kind == PlayerCollabs.Kind.Super).CompareTo(x.kind == PlayerCollabs.Kind.Super));
        const int MaxRows = 5;
        int rows = Mathf.Min(ready.Count, MaxRows);
        float rowHeight = 92f;
        float height = 170f + rows * rowHeight + 70f;

        Image dim = RuntimeUI.Box(canvas.transform, "CombineMenu", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, 0.75f));
        panel = dim.gameObject;
        RectTransform box = RuntimeUI.Frame(dim.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).rectTransform;
        box.sizeDelta = new Vector2(780, height);

        float top = height / 2f;
        RuntimeUI.Title(box, "COMBINE", 48f, new Vector2(0, top - 42), 700f);
        RuntimeUI.Text(box, $"Combinations left this run: <color=#FFD24A>{collabs.Remaining}</color> / {PlayerCollabs.MaxCollabs}\n" +
                            "<size=85%><color=#FF8A8A>The pieces you combine are used up and can't be picked up again.</color></size>",
                       20, new Color(0.8f, 0.84f, 0.95f), new Vector2(0, top - 102), new Vector2(720, 60));

        Button first = null;
        for (int i = 0; i < rows; i++)
        {
            PlayerCollabs.Def def = ready[i];
            float y = top - 170f - i * rowHeight - rowHeight / 2f + 10f;

            RectTransform row = RuntimeUI.Box(box, "Row", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 1f, 1f, 0.06f)).rectTransform;
            row.anchoredPosition = new Vector2(0, y);
            row.sizeDelta = new Vector2(740, rowHeight - 10f);

            bool super = def.kind == PlayerCollabs.Kind.Super;
            if (super) row.GetComponent<Image>().color = new Color(1f, 0.45f, 0.9f, 0.14f);
            string title = super ? $"<color=#FF7AD9>SUPER: {def.name}</color>" : $"<color=#FFD24A>{def.name}</color>";
            TextMeshProUGUI rowText = RuntimeUI.Text(row, $"<b>{title}</b>  <size=80%><color=#FFB36B>{PlayerCollabs.Recipe(def)}</color></size>\n<size=85%><color=#C9D2F0>{def.text}</color></size>",
                           21, Color.white, new Vector2(-85, 0), new Vector2(540, rowHeight - 14f), TextAlignmentOptions.MidlineLeft);
            rowText.enableAutoSizing = true;
            rowText.fontSizeMin = 12;
            rowText.fontSizeMax = 21;
            Button combine = RuntimeUI.Button(row, "Combine", new Vector2(285, 0), new Vector2(150, 50), () => Combine(def));
            if (first == null) first = combine;
        }
        if (ready.Count > MaxRows)
            RuntimeUI.Text(box, $"<size=80%>+{ready.Count - MaxRows} more after you combine</size>", 18, new Color(0.6f, 0.65f, 0.8f),
                           new Vector2(0, -height / 2f + 82f), new Vector2(600, 24));

        RuntimeUI.Button(box, "Not yet", new Vector2(0, -height / 2f + 42f), new Vector2(200, 48), Close);

        if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }
}
