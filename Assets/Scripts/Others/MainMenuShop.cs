using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Main menu shop: spend fish coins on permanent upgrades (left) and buddy looks (right).
public partial class MainMenu
{
    private GameObject shopPage;
    private Button shopBack, refundButton;
    private TextMeshProUGUI shopCoins, shopHint, refundLabel;
    private Button[] upgradeButtons, lookButtons;
    private TextMeshProUGUI[] upgradeText, upgradeCost, lookText, lookPrice;
    private Image[] upgradeCoin, lookFrames, lookCoin, lookImages;
    private Button[] lookTabButtons;
    private Shop.Look[] lookShown = new Shop.Look[0];
    private int lookTab;
    private static readonly string[] LookTabs = { "Colors", "Breeds", "Hats", "Weapons" };
    private const int WeaponTab = 3;
    private const int LookSlots = 8;
    private bool refundArmed;

    private const string FishColor = "#7FE3FF", Short = "#FF8A7A";

    // Content stays inside the panel's inner border (panel is 1180 wide, border eats ~30 px a side).
    private const float LeftX = -265f, RowW = 540f, LookX = 140f, LookStep = 118f, LookSize = 104f;

    private void BuildShop(Transform parent)
    {
        shopPage = Page(parent, "Shop");
        RectTransform panel = Container(shopPage.transform, new Vector2(1200, 680));
        BuildTitle(panel, "SHOP", 60f, 292f);
        CatTheme.Panel(panel, "Panel", CatTheme.Style.Panel, Vector2.zero, new Vector2(1180, 550));
        CatTheme.Img(panel, "Coin", FishCoin, new Vector2(436, 240), new Vector2(28, 28));
        shopCoins = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 24, Color.white, new Vector2(500, 240), new Vector2(100, 32), TextAlignmentOptions.MidlineLeft));

        // Upgrades: one clickable row each on the left.
        int n = Shop.Upgrades.Length;
        upgradeButtons = new Button[n];
        upgradeText = new TextMeshProUGUI[n];
        upgradeCost = new TextMeshProUGUI[n];
        upgradeCoin = new Image[n];
        RuntimeUI.Outline(RuntimeUI.Text(panel, "UPGRADES", 18, CatTheme.Faint, new Vector2(LeftX, 240), new Vector2(RowW, 24), TextAlignmentOptions.MidlineLeft));
        const float top = 196f, rowH = 54f;
        for (int i = 0; i < n; i++)
        {
            Shop.Upgrade u = Shop.Upgrades[i];
            float y = top - i * rowH;
            Image row = RuntimeUI.Box(panel, "Row", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
            row.rectTransform.anchoredPosition = new Vector2(LeftX, y);
            row.rectTransform.sizeDelta = new Vector2(RowW, rowH - 4f);
            Button b = row.gameObject.AddComponent<Button>();
            b.targetGraphic = row;
            ColorBlock c = b.colors;
            c.normalColor = new Color(1f, 1f, 1f, i % 2 == 0 ? 0.035f : 0f);
            c.highlightedColor = c.selectedColor = new Color(1f, 0.85f, 0.4f, 0.22f);
            c.pressedColor = new Color(1f, 0.85f, 0.4f, 0.45f);
            b.colors = c;
            Shop.Upgrade pick = u;
            b.onClick.AddListener(() => BuyUpgrade(pick));
            upgradeButtons[i] = b;

            CatTheme.Img(row.transform, "Icon", UiIcons.Icon(u.icon), new Vector2(-RowW / 2f + 28f, 0), new Vector2(38, 38)).preserveAspect = true;
            upgradeText[i] = RuntimeUI.Outline(RuntimeUI.Text(row.transform, "", 17, Color.white, new Vector2(-56, 0), new Vector2(330, rowH - 6f), TextAlignmentOptions.MidlineLeft));
            upgradeText[i].enableAutoSizing = true;
            upgradeText[i].fontSizeMin = 11;
            upgradeText[i].fontSizeMax = 17;
            upgradeText[i].raycastTarget = false;
            upgradeCoin[i] = CatTheme.Img(row.transform, "Coin", FishCoin, new Vector2(RowW / 2f - 118f, 0), new Vector2(26, 26));
            upgradeCost[i] = RuntimeUI.Outline(RuntimeUI.Text(row.transform, "", 22, Color.white, new Vector2(RowW / 2f - 55f, 0), new Vector2(92, 36), TextAlignmentOptions.MidlineLeft));
            upgradeCost[i].raycastTarget = false;
        }

        // Buddy looks: three tabs (colors, breeds, hats) over one 2 x 4 grid on the right.
        lookTabButtons = new Button[LookTabs.Length];
        for (int t = 0; t < LookTabs.Length; t++)
        {
            int tab = t;
            lookTabButtons[t] = TextButton(panel, LookTabs[t], 240f, () => SetLookTab(tab), 100f + t * 84f, 17f);
        }
        lookButtons = new Button[LookSlots];
        lookText = new TextMeshProUGUI[LookSlots];
        lookPrice = new TextMeshProUGUI[LookSlots];
        lookFrames = new Image[LookSlots];
        lookCoin = new Image[LookSlots];
        lookImages = new Image[LookSlots];
        for (int i = 0; i < LookSlots; i++)
        {
            var pos = new Vector2(LookX + (i % 4) * LookStep, 150 - (i / 4) * 196);
            Image frame = CatTheme.Panel(panel, "Look", CatTheme.Style.Slot, pos, new Vector2(LookSize, LookSize));
            frame.raycastTarget = true;
            lookImages[i] = CatTheme.Img(frame.transform, "Buddy", null, Vector2.zero, new Vector2(88, 88));
            lookImages[i].preserveAspect = true;
            Button b = frame.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            ColorBlock c = b.colors;
            c.highlightedColor = c.selectedColor = new Color(1f, 0.85f, 0.4f);
            b.colors = c;
            int slot = i;
            b.onClick.AddListener(() =>
            {
                if (lookTab == WeaponTab) { if (slot < Shop.Weapons.Length) PickWeapon(Shop.Weapons[slot]); }
                else if (slot < lookShown.Length) PickLook(lookShown[slot]);
            });
            lookButtons[i] = b;
            lookFrames[i] = frame;
            lookText[i] = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 16, Color.white, pos + new Vector2(0, -66), new Vector2(LookStep - 4f, 22)));
            lookText[i].textWrappingMode = TextWrappingModes.NoWrap; // long names shrink instead of pushing the price down
            lookText[i].enableAutoSizing = true;
            lookText[i].fontSizeMin = 10;
            lookText[i].fontSizeMax = 16;
            lookCoin[i] = CatTheme.Img(panel, "Coin", FishCoin, pos + new Vector2(-26, -88), new Vector2(20, 20));
            lookPrice[i] = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 16, Color.white, pos + new Vector2(0, -88), new Vector2(LookStep - 4f, 22)));
        }

        shopHint = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 16, CatTheme.Faint, new Vector2(0, -250), new Vector2(1100, 24)));
        refundButton = TextButton(panel, "", -305, Refund, -330, 22);
        Width(refundButton, 330f);
        CatTheme.Button(refundButton, 8f);
        refundLabel = refundButton.GetComponent<TextMeshProUGUI>();
        shopBack = TextButton(panel, "Back", -305, ShowMain, 0, 28);
        Width(shopBack, 170f);
        CatTheme.Button(shopBack, 10f);
        // Your buddy in its current coat + hat, walking in place on the panel's bottom edge.
        shopBuddy = CatTheme.Img(panel, "BuddyPreview", null, new Vector2(450, -292), new Vector2(110, 110));
        shopBuddy.preserveAspect = true;
        shopBuddy.raycastTarget = false;
        shopPage.SetActive(false);
    }

    private Image shopBuddy;

    // Called every frame from Animate while the shop is open.
    private void AnimateShopBuddy()
    {
        Sprite[] walk = BuddySprites.Get(1, "Walk", Shop.EquippedCoat, Shop.EquippedHat);
        shopBuddy.enabled = walk != null && walk.Length > 0;
        if (!shopBuddy.enabled) return;
        shopBuddy.sprite = walk[(int)(Time.unscaledTime * 8f) % walk.Length];
    }

    private void ShowShop()
    {
        mainPage.SetActive(false);
        shopPage.SetActive(true);
        refundArmed = false;
        RefreshShop("Fish coins come from every run: time survived, cats, bosses and endless minutes.");
        Select(upgradeButtons[0].gameObject);
    }

    private void BuyUpgrade(Shop.Upgrade u)
    {
        refundArmed = false;
        if (Shop.Maxed(u)) { RefreshShop($"{u.name} is maxed."); return; }
        int cost = Shop.NextCost(u);
        if (!Shop.Buy(u)) { Sfx.Play(Sfx.Id.Clang, 0.4f, 0.8f); RefreshShop($"Need {cost - Shop.Coins:N0} more fish coins for {u.name}."); return; }
        Sfx.Play(Sfx.Id.Pickup, 0.7f, 1.2f);
        RefreshShop($"Bought {u.name}: {u.Describe(Shop.Tier(u))}.");
    }

    private void SetLookTab(int tab)
    {
        lookTab = tab;
        Sfx.Play(Sfx.Id.Click, 0.5f);
        RefreshShop(tab == WeaponTab ? "Bought weapons join the level-up pool for every hero." : tab == 2 ? "Hats go on top of any coat." : tab == 1 ? "Every breed knows a small trick that helps every run while it's equipped." : "Pick a fur color for your buddy.");
    }

    private void PickLook(Shop.Look l)
    {
        refundArmed = false;
        if (Shop.Owns(l))
        {
            Shop.Equip(l);
            Sfx.Play(Sfx.Id.Click, 0.6f);
            RefreshShop(l.IsHat ? (l.id == "none" ? "Hat off." : $"Your buddy is wearing the {l.name}.") : $"Your buddy is now {l.name}." + Trick(l));
            return;
        }
        if (!Shop.BuyLook(l)) { Sfx.Play(Sfx.Id.Clang, 0.4f, 0.8f); RefreshShop($"Need {l.cost - Shop.Coins:N0} more fish coins for {l.name}." + Trick(l)); return; }
        Sfx.Play(Sfx.Id.Fanfare, 0.6f);
        RefreshShop($"Bought and equipped {l.name}!" + Trick(l));
    }

    private static string Trick(Shop.Look l) => l.trick != null ? $"  <color=#FFD24A>Trick: {l.trick}</color>" : "";

    private void PickWeapon(Shop.ShopWeapon w)
    {
        refundArmed = false;
        string name = PlayerWeapons.Find(w.id).name;
        if (Shop.OwnsWeapon(w)) { RefreshShop($"{name} is unlocked: it shows up in level-ups."); return; }
        if (!Shop.BuyWeapon(w)) { Sfx.Play(Sfx.Id.Clang, 0.4f, 0.8f); RefreshShop($"Need {w.cost - Shop.Coins:N0} more fish coins for {name}."); return; }
        Sfx.Play(Sfx.Id.Fanfare, 0.6f);
        RefreshShop($"Unlocked {name}! It can now show up when you level up.");
    }

    // Two presses: the first asks, the second refunds every upgrade (looks and weapons are kept).
    private void Refund()
    {
        if (Shop.Spent == 0) { RefreshShop("Nothing to refund yet."); return; }
        if (!refundArmed) { refundArmed = true; RefreshShop($"Press again to refund all upgrades for {Shop.Spent:N0} fish coins. Looks and weapons are kept."); return; }
        refundArmed = false;
        int back = Shop.Spent;
        Shop.Refund();
        Sfx.Play(Sfx.Id.Xp, 0.7f);
        RefreshShop($"Refunded {back:N0} fish coins.");
    }

    private void RefreshShop(string hint)
    {
        int coins = Shop.Coins;
        shopCoins.text = $"<color={FishColor}>{coins:N0}</color>";
        for (int i = 0; i < Shop.Upgrades.Length; i++)
        {
            Shop.Upgrade u = Shop.Upgrades[i];
            int tier = Shop.Tier(u);
            string pips = new string('#', tier) + new string('-', u.max - tier);
            string now = tier > 0 ? u.Describe(tier) : "<color=#9AA3C0>not bought</color>";
            upgradeText[i].text = $"<color=#FFF0AA>{u.name}</color>  <color=#FFD24A>{pips}</color>\n<size=80%>{now}</size>";
            bool maxed = Shop.Maxed(u);
            int cost = Shop.NextCost(u);
            upgradeCoin[i].enabled = !maxed;
            upgradeCost[i].text = maxed ? "<color=#7CFF9A>MAX</color>" : $"<color={(coins >= cost ? FishColor : Short)}>{cost:N0}</color>";
        }
        for (int t = 0; t < lookTabButtons.Length; t++)
            ((TextMeshProUGUI)lookTabButtons[t].targetGraphic).text = t == lookTab ? $"<color=#FFD24A>[{LookTabs[t]}]</color>" : LookTabs[t];
        bool weapons = lookTab == WeaponTab;
        lookShown = weapons ? new Shop.Look[0] : Shop.OfKind((Shop.LookKind)lookTab);
        for (int i = 0; i < LookSlots; i++)
        {
            bool used = i < (weapons ? Shop.Weapons.Length : lookShown.Length);
            if (weapons)
            {
                ShowSlot(i, used);
                if (used) FillWeaponSlot(i, Shop.Weapons[i], coins);
                continue;
            }
            lookFrames[i].gameObject.SetActive(used);
            lookText[i].gameObject.SetActive(used);
            lookPrice[i].gameObject.SetActive(used);
            lookCoin[i].gameObject.SetActive(used);
            if (!used) continue;
            Shop.Look l = lookShown[i];
            bool own = Shop.Owns(l), on = Shop.IsEquipped(l);
            Sprite[] idle = BuddySprites.Get(1, "Idle", l);
            lookImages[i].sprite = idle != null && idle.Length > 0 ? idle[0] : null;
            lookImages[i].enabled = lookImages[i].sprite != null;
            lookText[i].text = l.name;
            lookFrames[i].sprite = CatTheme.Get(on ? CatTheme.Style.SlotGold : CatTheme.Style.Slot);
            lookCoin[i].enabled = !own;
            lookPrice[i].alignment = own ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            lookPrice[i].rectTransform.anchoredPosition = lookText[i].rectTransform.anchoredPosition + new Vector2(own ? 0f : 42f, -22f);
            lookPrice[i].text = on ? "<color=#7CFF9A>equipped</color>" : own ? "<color=#9AA3C0>owned</color>"
                              : $"<color={(coins >= l.cost ? FishColor : Short)}>{l.cost:N0}</color>";
        }
        int spent = Shop.Spent;
        refundLabel.text = refundArmed ? $"<color={Short}>Confirm refund</color>" : spent > 0 ? $"Refund ({spent:N0})" : "Refund";
        shopHint.text = hint;
    }

    private void ShowSlot(int i, bool on)
    {
        lookFrames[i].gameObject.SetActive(on);
        lookText[i].gameObject.SetActive(on);
        lookPrice[i].gameObject.SetActive(on);
        lookCoin[i].gameObject.SetActive(on);
    }

    private void FillWeaponSlot(int i, Shop.ShopWeapon w, int coins)
    {
        bool own = Shop.OwnsWeapon(w);
        lookImages[i].sprite = UiIcons.Icon(w.id);
        lookImages[i].enabled = lookImages[i].sprite != null;
        lookText[i].text = PlayerWeapons.Find(w.id).name;
        lookFrames[i].sprite = CatTheme.Get(own ? CatTheme.Style.SlotGold : CatTheme.Style.Slot);
        lookCoin[i].enabled = !own;
        lookPrice[i].alignment = own ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
        lookPrice[i].rectTransform.anchoredPosition = lookText[i].rectTransform.anchoredPosition + new Vector2(own ? 0f : 42f, -22f);
        lookPrice[i].text = own ? "<color=#7CFF9A>unlocked</color>" : $"<color={(coins >= w.cost ? FishColor : Short)}>{w.cost:N0}</color>";
    }

    // ---------- Fish coin symbol: a gold coin with a little blue fish, drawn in code ----------

    private static Sprite fishCoin;
    public static Sprite FishCoin => fishCoin != null ? fishCoin : fishCoin = PixelSprite(new[]
    {
        "....oooooo....",
        "..oohhggggoo..",
        ".ohhggggggggo.",
        ".ohggggggggso.",
        "ohgggggggggsso",
        "ohgfggfffggsso",
        "ohgffffffefsso",
        "ohgffwwwwwfsso",
        "ohgfggfffggsso",
        "ohgggggggggsso",
        ".ohggggggggso.",
        ".osggggggggso.",
        "..oossssssoo..",
        "....oooooo....",
    });

    private static Sprite PixelSprite(string[] rows)
    {
        Color Pal(char ch) => ch switch
        {
            'o' => new Color32(90, 58, 16, 255),
            'h' => new Color32(255, 240, 170, 255),
            'g' => new Color32(255, 210, 74, 255),
            's' => new Color32(200, 150, 30, 255),
            'f' => new Color32(58, 127, 208, 255),
            'w' => new Color32(127, 227, 255, 255),
            'e' => new Color32(20, 20, 40, 255),
            _ => Color.clear,
        };
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - y, Pal(rows[y][x]));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
    }
}
