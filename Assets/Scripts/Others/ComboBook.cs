using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ComboBook : MonoBehaviour
{
    public static bool IsOpen => instance != null && instance.panel != null;

    private const int Columns = 4;
    private static readonly Vector2 CardSize = new Vector2(296, 64);

    private static ComboBook instance;
    private Canvas canvas;
    private GameObject panel;
    private TextMeshProUGUI info;
    private GameObject returnFocus;

    public static void Open(GameObject focusAfterClose)
    {
        PlayerCollabs collabs = FindFirstObjectByType<PlayerCollabs>();
        if (collabs == null) return;
        if (instance == null) instance = new GameObject("ComboBook").AddComponent<ComboBook>();
        instance.returnFocus = focusAfterClose;
        instance.Build(collabs);
    }

    public static void Close()
    {
        if (!IsOpen) return;
        Destroy(instance.panel);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(instance.returnFocus);
    }

    private void Awake() => canvas = RuntimeUI.Canvas("ComboBookCanvas", 170);

    private bool supersTab;

    private void Build(PlayerCollabs collabs)
    {
        if (panel != null) Destroy(panel);

        var defs = new List<PlayerCollabs.Def>(supersTab ? PlayerCollabs.Supers : PlayerCollabs.All);
        PlayerCollabs.Status StatusOf(PlayerCollabs.Def d) => supersTab ? collabs.SuperStatus(d) : collabs.StatusOf(d);
        int Rank(PlayerCollabs.Def d) => StatusOf(d) switch
        {
            PlayerCollabs.Status.Ready => 0,
            PlayerCollabs.Status.Locked => 1,
            PlayerCollabs.Status.InProgress => 2,
            PlayerCollabs.Status.NotStarted => 3,
            PlayerCollabs.Status.Done => 4,
            _ => 5,
        };
        defs.Sort((x, y) => Rank(x) != Rank(y) ? Rank(x).CompareTo(Rank(y))
                          : supersTab ? 0 : collabs.Progress(y).CompareTo(collabs.Progress(x)));

        int rows = Mathf.CeilToInt(PlayerCollabs.All.Length / (float)Columns);
        Vector2 size = new Vector2(Columns * (CardSize.x + 8) + 40, rows * (CardSize.y + 6) + 130);

        Image dim = RuntimeUI.Box(canvas.transform, "ComboBook", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, 0.8f));
        panel = dim.gameObject;
        RectTransform box = RuntimeUI.Frame(dim.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).rectTransform;
        box.sizeDelta = size;
        float top = size.y / 2f;
        RuntimeUI.Title(box, supersTab ? $"<color=#{ColorUtility.ToHtmlStringRGB(SuperWeapon.SuperPink)}>SUPER COMBINES</color>" : "COMBINATIONS", 34f, new Vector2(0, top - 28), 600f);
        string unlock = collabs.SuperUnlocked ? "<color=#7CFF9A>unlocked</color>" : "<color=#FF7AD9>locked until the 15:00 boss is defeated</color>";
        RuntimeUI.Text(box, supersTab
                           ? $"Fuse two finished combos into one super. Only 1 per run, {unlock}."
                           : $"Max both pieces, then press {Keybinds.Label(Keybinds.Action.Combine)} to combine.  <color=#FFD24A>{collabs.Remaining}</color> of {PlayerCollabs.MaxCollabs} combos left this run.",
                       14, new Color(0.75f, 0.8f, 0.95f), new Vector2(0, top - 54), new Vector2(size.x - 40, 22));

        Button combosTab = RuntimeUI.Button(box, "Combos", new Vector2(-size.x / 2f + 100, top - 30), new Vector2(160, 40), () => SwitchTab(collabs, false));
        Button superTab = RuntimeUI.Button(box, "Super Combos", new Vector2(size.x / 2f - 110, top - 30), new Vector2(180, 40), () => SwitchTab(collabs, true));
        Button open = supersTab ? superTab : combosTab;
        ColorBlock lit = open.colors;
        lit.normalColor = RuntimeUI.Gold;
        open.colors = lit;

        Button first = null;
        for (int i = 0; i < defs.Count; i++)
        {
            int col = i % Columns, row = i / Columns;
            Vector2 pos = new Vector2((col - (Columns - 1) / 2f) * (CardSize.x + 8), top - 104 - row * (CardSize.y + 6));
            Button card = Card(box, defs[i], collabs, pos);
            if (first == null) first = card;
        }

        info = RuntimeUI.Text(box, "Click a combination to see what it does.", 15, new Color(0.85f, 0.88f, 1f),
                              new Vector2(-70, -top + 34), new Vector2(size.x - 220, 44), TextAlignmentOptions.MidlineLeft);
        RuntimeUI.Button(box, "Back", new Vector2(size.x / 2f - 80, -top + 34), new Vector2(120, 40), Close);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
    }

    private Button Card(RectTransform parent, PlayerCollabs.Def d, PlayerCollabs collabs, Vector2 pos)
    {
        bool super = d.kind == PlayerCollabs.Kind.Super;
        PlayerCollabs.Status status = super ? collabs.SuperStatus(d) : collabs.StatusOf(d);
        Image bg = RuntimeUI.Frame(parent, d.id, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), gold: status == PlayerCollabs.Status.Ready);
        bg.rectTransform.anchoredPosition = pos;
        bg.rectTransform.sizeDelta = CardSize;
        bool faded = status == PlayerCollabs.Status.Done || status == PlayerCollabs.Status.Blocked;
        bg.color = faded ? new Color(0.06f, 0.06f, 0.1f)
                 : super && status != PlayerCollabs.Status.Ready ? new Color(0.2f, 0.05f, 0.18f)
                 : RuntimeUI.Opaque(bg.color);

        var button = bg.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        bg.gameObject.AddComponent<SelectGlow>();
        bg.gameObject.AddComponent<CardHover>();
        string kind = super ? "Super combine" : d.kind == PlayerCollabs.Kind.Merge ? "Weapon merge" : d.kind == PlayerCollabs.Kind.Boost ? "Weapon boost" : "Bomb upgrade";
        button.onClick.AddListener(() => info.text = $"<color=#FFD24A>{d.name}</color>  <color=#9AA3C0>({kind})</color>\n{d.text}");

        Image slot = RuntimeUI.Box(bg.transform, "Slot", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Color.white);
        UiIcons.Frame(slot, UiIcons.Slot);
        slot.rectTransform.anchoredPosition = new Vector2(36, 0);
        slot.rectTransform.sizeDelta = new Vector2(52, 52);
        Image icon = RuntimeUI.Box(slot.transform, "Icon", Vector2.zero, Vector2.one, Color.white);
        icon.rectTransform.offsetMin = Vector2.one * 8;
        icon.rectTransform.offsetMax = -Vector2.one * 8;
        icon.preserveAspect = true;
        TextMeshProUGUI badge = RuntimeUI.Text(slot.transform, "", 18, Color.white, Vector2.zero, new Vector2(56, 56));
        UiIcons.Apply(d.kind == PlayerCollabs.Kind.Merge || super ? d.id : d.kind == PlayerCollabs.Kind.Boost ? d.a : "bomb", icon, badge);
        if (faded) icon.color = new Color(1f, 1f, 1f, 0.5f);

        string tag = status switch
        {
            PlayerCollabs.Status.Done => "<color=#7CFF9A>DONE</color>",
            PlayerCollabs.Status.Ready => $"<color=#FFD24A>READY! [{Keybinds.Label(Keybinds.Action.Combine)}]</color>",
            PlayerCollabs.Status.Locked => "<color=#FF7AD9>15:00 boss</color>",
            PlayerCollabs.Status.Blocked when super => "<color=#FF7A7A>" + (collabs.SuperTaken != null ? "super used" : "combo blocked") + "</color>",
            PlayerCollabs.Status.Blocked => "<color=#FF7A7A>" + (collabs.Remaining <= 0 ? "no slots" : "piece used") + "</color>",
            _ => "",
        };
        string first = PlayerCollabs.FirstPiece(d);
        string pieces = super ? ComboPiece(collabs, d.a) + " <color=#9AA3C0>+</color> " + ComboPiece(collabs, d.b)
                     : Piece(collabs, first, false) + " <color=#9AA3C0>+</color> " + Piece(collabs, d.b, PlayerCollabs.SecondIsSkill(d));
        TextMeshProUGUI text = RuntimeUI.Text(bg.transform, $"<b>{d.name}</b>  <size=80%>{tag}</size>\n<size=80%>{pieces}</size>", 16, Color.white,
                                              new Vector2(34, 0), new Vector2(CardSize.x - 84, CardSize.y - 8), TextAlignmentOptions.MidlineLeft);
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 16f;
        RuntimeUI.Outline(text, 0.15f);
        return button;
    }

    private void SwitchTab(PlayerCollabs collabs, bool supers)
    {
        supersTab = supers;
        Build(collabs);
    }

    private static string ComboPiece(PlayerCollabs collabs, string comboId)
    {
        PlayerCollabs.Def combo = PlayerCollabs.Find(comboId);
        PlayerCollabs.Status s = collabs.StatusOf(combo);
        string color = s == PlayerCollabs.Status.Done ? "#7CFF9A" : s == PlayerCollabs.Status.Blocked ? "#FF7A7A" : s == PlayerCollabs.Status.Ready ? "#FFE08A" : "#8890A8";
        return $"<color={color}>{combo.name}</color>";
    }

    private static string Piece(PlayerCollabs collabs, string id, bool isSkill)
    {
        int level = collabs.PieceLevel(id, isSkill), max = PlayerCollabs.PieceMax(id, isSkill);
        string color = collabs.PieceUsed(id) ? "#FF7A7A" : level >= max ? "#7CFF9A" : (id == "bombs" ? level > 1 : level > 0) ? "#FFE08A" : "#8890A8";
        return $"<color={color}>{PlayerCollabs.PieceName(id, isSkill)} {level}/{max}</color>";
    }
}
