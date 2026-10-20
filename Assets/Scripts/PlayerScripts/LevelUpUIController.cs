using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerStats))]
public class LevelUpUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private List<Button> upgradeButtons = new();
    [SerializeField] private TextMeshProUGUI titleText;
    private RectTransform levelTitle;

    [Header("Upgrade Options")]
    [SerializeField] private List<UpgradeDefinition> upgradePool = new();

    [Serializable]
    public class UpgradeDefinition
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public UpgradeType upgradeType;
        public float amount = 1f;

        public void Apply(PlayerStats stats)
        {
            if (stats == null)
            {
                return;
            }

            switch (upgradeType)
            {
                case UpgradeType.AttackDamagePercent:
                    stats.attackDamageMultiplier += amount;
                    break;
                case UpgradeType.MaxHealth:
                    stats.IncreaseMaxHealth(amount);
                    break;
                case UpgradeType.AttackSpeedPercent:
                    stats.attackSpeedMultiplier += amount;
                    break;
                case UpgradeType.MoveSpeedPercent:
                    stats.moveSpeedMultiplier += amount;
                    break;
                case UpgradeType.ProjectileCount:
                    stats.IncreaseProjectileCount(Mathf.RoundToInt(amount));
                    break;
                case UpgradeType.Heal:
                    stats.Heal(amount);
                    break;
                case UpgradeType.AreaPercent:
                    stats.areaMultiplier += amount;
                    break;
                case UpgradeType.CritChance:
                    stats.critChance += amount;
                    break;
            }
        }
    }

    public enum UpgradeType
    {
        AttackDamagePercent,
        MaxHealth,
        AttackSpeedPercent,
        MoveSpeedPercent,
        ProjectileCount,
        Heal,
        AreaPercent,
        CritChance
    }

    private PlayerStats stats;
    private int pendingLevelUps;
    private bool isPanelOpen;

    public int rerolls = 3;
    public int eliminates = 3;
    private bool eliminating;
    private readonly HashSet<string> banned = new HashSet<string>();
    private Button rerollButton, eliminateButton, skipButton, combosButton;
    public bool IsOpen => isPanelOpen;
    // True from the level-up flash until the last queued pick is made.
    public bool Busy => isPanelOpen || celebrating || pendingLevelUps > 0;
    private bool inputReady;
    private bool isGamePaused;
    private float timeScaleBeforePause = 1f;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    private void Start()
    {
        EnsureDefaultUpgradePool();
        rerolls += Achievements.Count("reroll");
        eliminates += Achievements.Count("eliminate");

        if (stats != null)
        {
            stats.OnLevelUp += HandleLevelUp;
        }

        BindUi();
        BuildActionButtons();
        if (levelUpPanel != null) RuntimeUI.Restyle(levelUpPanel.transform);
        if (titleText != null)
            levelTitle = RuntimeUI.Title(titleText.transform.parent, "LEVEL UP!", titleText.fontSize, titleText.rectTransform.anchoredPosition + new Vector2(0f, titleText.fontSize * 0.32f));
        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnLevelUp -= HandleLevelUp;
        }
    }

    private void HandleLevelUp(int level)
    {
        if (stats.currentHealth <= 0f || GameOverScreen.IsShowing) return;
        pendingLevelUps++;
        if (!isPanelOpen && !celebrating) StartCoroutine(Celebrate());
    }

    private bool celebrating;
    private IEnumerator Celebrate()
    {
        celebrating = true;
        Vector2 at = stats.transform.position;
        ExplosionFx.Ring(at, 3f, new Color(1f, 0.92f, 0.5f), 0.4f);
        ExplosionFx.Spawn(at, 1.2f, new Color(1f, 0.95f, 0.7f), order: 9);
        Juice.Sparks(at, new Color(1f, 0.85f, 0.3f), 18, 8f, 0.18f);
        DamagePopup.SpawnText(at + Vector2.up * 1f, "LEVEL UP!", new Color(1f, 0.88f, 0.35f), 7f);
        Juice.SlowMo(0.2f, 0.4f);
        yield return new WaitForSecondsRealtime(0.35f);
        while (RewardMenu.IsOpen) yield return null; // holo box / anvil owns the pause; open after it
        celebrating = false;
        if (stats.currentHealth > 0f && !GameOverScreen.IsShowing && !isPanelOpen) OpenPanel(stats.level);
    }

    private void BindUi()
    {
        if (levelUpPanel == null)
        {
            levelUpPanel = FindPanelByName();
        }

        if (upgradeButtons.Count == 0)
        {
            DiscoverButtons();
        }

        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }

        if (titleText == null && levelUpPanel != null)
        {
            titleText = levelUpPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        for (int i = 0; i < upgradeButtons.Count; i++)
        {
            Button button = upgradeButtons[i];
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);
        }
    }

    private void DiscoverButtons()
    {
        if (levelUpPanel != null)
        {
            Button[] panelButtons = levelUpPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < panelButtons.Length; i++)
            {
                Button button = panelButtons[i];
                if (button != null && !upgradeButtons.Contains(button))
                {
                    upgradeButtons.Add(button);
                }
            }
        }

        if (upgradeButtons.Count == 0)
        {
            Button[] allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);
            for (int i = 0; i < allButtons.Length; i++)
            {
                Button button = allButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (!upgradeButtons.Contains(button))
                {
                    upgradeButtons.Add(button);
                }
            }
        }

        if (levelUpPanel == null && upgradeButtons.Count > 0)
        {
            levelUpPanel = FindPanelForButton(upgradeButtons[0]);
        }
    }

    private void EnsureDefaultUpgradePool()
    {
        if (upgradePool != null && upgradePool.Count > 0)
        {
            return;
        }

        upgradePool = new List<UpgradeDefinition>
        {
            new UpgradeDefinition { id = "attack15", description = "Upgrade player's attack by 15%", upgradeType = UpgradeType.AttackDamagePercent, amount = 0.15f },
            new UpgradeDefinition { id = "health20", description = "Upgrade player's health by 20", upgradeType = UpgradeType.MaxHealth, amount = 20f },
            new UpgradeDefinition { id = "attackSpeed20", description = "Upgrade player's attack speed by 20%", upgradeType = UpgradeType.AttackSpeedPercent, amount = 0.20f },
            new UpgradeDefinition { id = "moveSpeed10", description = "Upgrade player's movement speed by 10%", upgradeType = UpgradeType.MoveSpeedPercent, amount = 0.10f },
            new UpgradeDefinition { id = "projectile1", description = "+1 bomb per throw", upgradeType = UpgradeType.ProjectileCount, amount = 1f },
            new UpgradeDefinition { id = "heal20", description = "Heal player by 20", upgradeType = UpgradeType.Heal, amount = 20f },
            new UpgradeDefinition { id = "area15", description = "Explosion radius +15%", upgradeType = UpgradeType.AreaPercent, amount = 0.15f },
            new UpgradeDefinition { id = "crit5", description = "Crit chance +5%", upgradeType = UpgradeType.CritChance, amount = 0.05f },
        };
    }

    private GameObject FindPanelByName()
    {
        string[] candidates = { "LevelUpPanel", "UpgradePanel", "LevelUpUI", "Panel" };

        foreach (string candidate in candidates)
        {
            GameObject found = GameObject.Find(candidate);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private GameObject FindPanelForButton(Button button)
    {
        if (button == null)
            return null;

        Transform current = button.transform;
        while (current != null)
        {
            if (current.name.ToLowerInvariant().Contains("panel") || current.name.ToLowerInvariant().Contains("level"))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private static T FindChild<T>(Transform root, string name) where T : Component
    {
        foreach (T c in root.GetComponentsInChildren<T>(true))
            if (c.name == name) return c;
        return null;
    }

    private void OpenPanel(int level)
    {
        if (levelUpPanel == null)
            return;

        eliminating = false;
        PopulateButtons();
        RefreshActions();

        if (titleText != null && stats != null)
        {
            titleText.text = $"\n<size=40%><color=#C9D2F0>Level {level}  -  choose an upgrade</color></size>";
        }

        TextMeshProUGUI statsText = FindChild<TextMeshProUGUI>(levelUpPanel.transform, "StatsText");
        if (statsText != null) statsText.text = PauseMenu.BuildStats(stats);

        SetPanelVisible(true);
        PauseGame();
        StopAllCoroutines();
        StartCoroutine(PopIn());
    }

    private IEnumerator PopIn()
    {
        const float duration = 0.15f, inputLock = 0.3f;
        inputReady = false;
        foreach (Button b in upgradeButtons) if (b != null) b.interactable = false;

        Transform panel = levelUpPanel.transform;
        for (float t = 0f; t < inputLock; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / duration);
            panel.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1f - (1f - k) * (1f - k));
            for (int i = 0; i < upgradeButtons.Count; i++)
                if (upgradeButtons[i] != null) upgradeButtons[i].transform.localScale = Vector3.one * BackOut(Mathf.Clamp01((t - i * 0.06f) / 0.18f));
            yield return null;
        }
        panel.localScale = Vector3.one;
        foreach (Button b in upgradeButtons) if (b != null) b.transform.localRotation = Quaternion.identity;
        foreach (Button b in upgradeButtons) if (b != null) b.transform.localScale = Vector3.one;

        foreach (Button b in upgradeButtons) if (b != null) b.interactable = true;
        inputReady = true;

        Button first = upgradeButtons.Find(b => b != null && b.gameObject.activeSelf);
        if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    private void Update()
    {
        if (!isPanelOpen || !inputReady) return;
        if ((Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.leftShoulder)) { ToggleCombos(); return; }
        if (ComboBook.IsOpen) { if (GamepadUI.Back) ComboBook.Close(); return; }
        AnimateCards();
        if (GamepadUI.Pressed(g => g.buttonWest)) { Reroll(); return; }
        if (GamepadUI.Pressed(g => g.buttonNorth)) { ToggleEliminate(); return; }
        if (GamepadUI.Pressed(g => g.rightShoulder)) { Skip(); return; }
        if (Keyboard.current == null) return;

        if (Keyboard.current.rKey.wasPressedThisFrame) { Reroll(); return; }
        if (Keyboard.current.eKey.wasPressedThisFrame) { ToggleEliminate(); return; }
        if (Keyboard.current.xKey.wasPressedThisFrame) { Skip(); return; }

        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        for (int i = 0; i < keys.Length && i < upgradeButtons.Count; i++)
        {
            Button b = upgradeButtons[i];
            if (Keyboard.current[keys[i]].wasPressedThisFrame && b != null && b.gameObject.activeSelf)
            {
                b.onClick.Invoke();
                return;
            }
        }
    }

    private enum OfferKind { Stat, Skill, Weapon, Companion, Passive }

    private const int EarlyLevels = 10;
    private bool EarlyGame => stats.level <= EarlyLevels;

    private class Offer
    {
        public string text;
        public Action apply;
        public float weight = 1f;
        public OfferKind kind;
        public string icon;
        public int comboScore;
    }

    private string ComboSuggestion(string pieceId, bool reachesMax, out int score)
    {
        score = 0;
        string unlock = reachesMax ? ComboHint(pieceId) : "";
        if (unlock != "") { score = 4; return unlock; }

        PlayerCollabs collabs = GetComponent<PlayerCollabs>();
        if (collabs == null) return "";
        var combos = collabs.CombosWithOwnedPartner(pieceId);
        if (combos.Count == 0) return "";
        combos.Sort((x, y) => y.score.CompareTo(x.score));
        var top = combos[0];
        score = top.score;
        int level = collabs.PieceLevel(top.partner, top.partnerIsSkill);
        string partner = $"{PlayerCollabs.PieceName(top.partner, top.partnerIsSkill)} {(level >= PlayerCollabs.PieceMax(top.partner, top.partnerIsSkill) ? "MAX" : "Lv " + level)}";
        string more = combos.Count > 1 ? $" +{combos.Count - 1} more" : "";
        return $"\n<size=80%><color=#FFD24A>* Builds toward {top.def.name} (with {partner}){more}</color></size>";
    }

    private float UpgradeWeight(UpgradeDefinition upgrade)
    {
        switch (upgrade.upgradeType)
        {
            case UpgradeType.Heal:
                float missing = 1f - stats.currentHealth / stats.maxHealth;
                return missing < 0.15f ? 0f : missing * 3f;
            case UpgradeType.ProjectileCount: return stats.projectileCount >= 6 ? 0f : 1f;
            case UpgradeType.CritChance: return stats.critChance >= 0.5f ? 0f : 1f;
            case UpgradeType.AreaPercent: return stats.areaMultiplier >= 2.5f ? 0f : 1f;
            case UpgradeType.AttackSpeedPercent: return stats.attackSpeedMultiplier >= 2f ? 0f : 1f;
            case UpgradeType.MoveSpeedPercent: return stats.moveSpeedMultiplier >= 1.6f ? 0f : 1f;
            default: return 1f;
        }
    }

    private string ComboHint(string pieceId)
    {
        PlayerCollabs collabs = GetComponent<PlayerCollabs>();
        if (collabs == null) return "";
        List<PlayerCollabs.Def> unlocked = collabs.UnlockedBy(pieceId);
        return unlocked.Count == 0 ? "" : $"\n<size=80%><color=#FFD24A>Unlocks combo: {string.Join(", ", unlocked.ConvertAll(d => d.name))}</color></size>";
    }

    private List<Offer> BuildOffers()
    {
        var offers = new List<Offer>();

        foreach (UpgradeDefinition upgrade in upgradePool)
        {
            float weight = UpgradeWeight(upgrade);
            if (weight <= 0f) continue;

            string title = string.IsNullOrEmpty(upgrade.title) ? upgrade.id : upgrade.title;
            UpgradeDefinition captured = upgrade;
            int score = 0;
            string hint = upgrade.upgradeType == UpgradeType.ProjectileCount
                ? ComboSuggestion("bombs", stats.projectileCount + 1 >= PlayerCollabs.MaxBombsPerThrow, out score) : "";
            offers.Add(new Offer
            {
                text = $"<size=115%><b><color=#FFE08A>{title}</color></b></size>\n<color=#C9D2F0>{upgrade.description}</color>" + hint,
                apply = () => captured.Apply(stats),
                icon = "stat:" + upgrade.upgradeType,
                weight = weight * (1f + 0.4f * score),
                comboScore = score,
            });
        }

        PlayerSkills skills = GetComponent<PlayerSkills>();
        if (skills != null)
        {
            foreach (PlayerSkills.Skill skill in skills.Offerable())
            {
                int next = skills.LevelOf(skill.id) + 1;
                string tag = next == 1 ? "<color=#7CFF9A>NEW SKILL!</color>" : $"<color=#9FD8FF>Lv {next}</color>";
                PlayerSkills.Skill captured = skill;
                string hint = ComboSuggestion(skill.id, next == skill.levels.Length, out int score);
                offers.Add(new Offer
                {
                    text = $"<size=115%><b><color=#D7A6FF>{skill.name}</color></b></size>  <size=80%>{tag}</size>\n<color=#C9D2F0>{skill.levels[next - 1]}</color>" + hint,
                    apply = () => skills.LevelUp(captured),
                    icon = "skill:" + skill.id,
                    weight = (next > 1 ? 1.5f : 0.8f) * (1f + 0.4f * score),
                    kind = OfferKind.Skill,
                    comboScore = score,
                });
            }
        }

        PlayerWeapons weapons = GetComponent<PlayerWeapons>();
        if (weapons != null)
        {
            foreach (PlayerWeapons.Def def in weapons.Offerable())
            {
                int next = weapons.LevelOf(def.id) + 1;
                string tag = next == 1 ? "<color=#7CFF9A>NEW WEAPON!</color>"
                           : next == Weapon.MaxLevel ? "<color=#FFD24A>Lv MAX</color>" : $"<color=#9FD8FF>Lv {next}</color>";
                PlayerWeapons.Def captured = def;
                string hint = ComboSuggestion(def.id, next == Weapon.MaxLevel, out int score);
                offers.Add(new Offer
                {
                    text = $"<size=115%><b><color=#FFB36B>{def.name}</color></b></size>  <size=80%>{tag}</size>\n<color=#C9D2F0>{def.levels[next - 1]}</color>" + hint,
                    apply = () => weapons.LevelUp(captured),
                    icon = def.id,
                    weight = (next > 1 ? 1.6f : 0.9f) * (1f + 0.4f * score),
                    kind = OfferKind.Weapon,
                    comboScore = score,
                });
            }
        }

        MiraPassives mira = MiraPassives.Instance;
        if (mira != null)
        {
            foreach (MiraPassives.Passive p in mira.Offerable())
            {
                int next = mira.LevelOf(p.id) + 1;
                MiraPassives.Passive captured = p;
                offers.Add(new Offer
                {
                    text = $"<size=115%><b><color=#7FE3FF>{p.name}</color></b></size>  <size=80%><color=#9FD8FF>Lv {next}</color></size>\n<color=#C9D2F0>{p.levels[next - 2]}</color>",
                    apply = () => mira.LevelUp(captured.id),
                    icon = "passive:" + p.id,
                    weight = 0.9f,
                    kind = OfferKind.Passive,
                });
            }
        }

        Companion buddy = Companion.Instance;
        if (buddy != null && buddy.Level < Companion.MaxLevel)
        {
            int next = buddy.Level + 1;
            offers.Add(new Offer
            {
                text = $"<size=115%><b><color=#FFD24A>Cat Buddy</color></b></size>  <size=80%><color=#9FD8FF>Lv {next}</color></size>\n<color=#C9D2F0>{Companion.LevelText[next - 1]}</color>",
                apply = () => buddy.LevelUp(),
                icon = "buddy",
                weight = 1.5f,
                kind = OfferKind.Companion,
            });
        }

        if (EarlyGame)
            foreach (Offer o in offers)
                if (o.kind == OfferKind.Stat || o.kind == OfferKind.Passive) o.weight *= 4f;

        offers.RemoveAll(o => banned.Contains(o.icon));
        return offers;
    }

    public bool GiveRandomStat(out string icon, out string title)
    {
        var pool = new List<UpgradeDefinition>();
        float sum = 0f;
        foreach (UpgradeDefinition u in upgradePool)
            if (u.upgradeType != UpgradeType.Heal && UpgradeWeight(u) > 0f) { pool.Add(u); sum += UpgradeWeight(u); }
        icon = title = null;
        if (pool.Count == 0) return false;

        float roll = UnityEngine.Random.value * sum;
        UpgradeDefinition pick = pool[pool.Count - 1];
        foreach (UpgradeDefinition u in pool) if ((roll -= UpgradeWeight(u)) <= 0f) { pick = u; break; }
        pick.Apply(stats);
        icon = "stat:" + pick.upgradeType;
        title = string.IsNullOrEmpty(pick.title) ? pick.id : pick.title;
        return true;
    }

    private static Offer TakeWeighted(List<Offer> pool)
    {
        float sum = 0f;
        foreach (Offer o in pool) sum += o.weight;
        float roll = UnityEngine.Random.value * sum;
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= pool[i].weight;
            if (roll <= 0f || i == pool.Count - 1)
            {
                Offer picked = pool[i];
                pool.RemoveAt(i);
                return picked;
            }
        }
        return null;
    }

    private List<Offer> ChooseOffers(int count)
    {
        List<Offer> pool = BuildOffers();
        var chosen = new List<Offer>();
        while (chosen.Count < count && pool.Count > 0) chosen.Add(TakeWeighted(pool));

        if (!EarlyGame)
        {
            Guarantee(OfferKind.Weapon, chosen, pool);
            Guarantee(OfferKind.Skill, chosen, pool);
        }

        Offer best = null;
        foreach (Offer o in chosen) if (o.comboScore > 0 && (best == null || o.comboScore > best.comboScore)) best = o;
        if (best != null) best.text += "\n<size=75%><b><color=#FFE08A>* RECOMMENDED</color></b></size>";

        for (int i = chosen.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (chosen[i], chosen[j]) = (chosen[j], chosen[i]);
        }
        return chosen;
    }

    private static void Guarantee(OfferKind kind, List<Offer> chosen, List<Offer> pool)
    {
        if (chosen.Count == 0 || chosen.Exists(o => o.kind == kind)) return;
        List<Offer> candidates = pool.FindAll(o => o.kind == kind);
        if (candidates.Count == 0) return;

        int replace = chosen.FindLastIndex(o => o.kind == OfferKind.Stat);
        if (replace < 0) replace = chosen.FindLastIndex(o => o.kind == OfferKind.Companion);
        if (replace < 0) replace = chosen.FindLastIndex(o => chosen.FindAll(x => x.kind == o.kind).Count > 1);
        if (replace < 0) return;

        Offer picked = TakeWeighted(candidates);
        pool.Remove(picked);
        pool.Add(chosen[replace]);
        chosen[replace] = picked;
    }

    private List<Offer> shown = new List<Offer>();

    private static Color KindColor(OfferKind kind) => kind switch
    {
        OfferKind.Weapon => new Color(1f, 0.62f, 0.3f),
        OfferKind.Skill => new Color(0.8f, 0.55f, 1f),
        OfferKind.Passive => new Color(0.4f, 0.9f, 1f),
        OfferKind.Companion => new Color(1f, 0.85f, 0.3f),
        _ => new Color(0.55f, 0.8f, 1f),
    };

    private void AnimateCards()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        float t = Time.unscaledTime, k = Time.unscaledDeltaTime * 14f;
        foreach (Button b in upgradeButtons)
        {
            if (b == null || !b.gameObject.activeSelf || !b.TryGetComponent(out Outline glow)) continue;
            bool on = b.gameObject == selected;
            Transform tr = b.transform;
            tr.localScale = Vector3.Lerp(tr.localScale, Vector3.one * (on ? 1.05f : 1f), k);
            tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.Euler(0f, 0f, on ? -1.5f + Mathf.Sin(t * 3f) * 1.5f : 0f), k);
            Color c = glow.effectColor;
            c.a = on ? 0.75f + 0.25f * Mathf.Sin(t * 6f) : 0.35f;
            glow.effectColor = c;
            glow.effectDistance = on ? new Vector2(5f, 5f) : new Vector2(2f, 2f);
        }
    }

    private void PopulateButtons()
    {
        shown = ChooseOffers(upgradeButtons.Count);
        ShowOffers();
    }

    private void ShowOffers()
    {
        List<Offer> offers = shown;

        for (int i = 0; i < upgradeButtons.Count; i++)
        {
            Button button = upgradeButtons[i];
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();

            bool hasOffer = i < offers.Count;
            button.gameObject.SetActive(hasOffer);
            if (!hasOffer)
            {
                continue;
            }

            Offer offer = offers[i];

            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (buttonText != null)
            {
                buttonText.text = offer.text;
            }

            Image icon = FindChild<Image>(button.transform, "Icon");
            TextMeshProUGUI badge = FindChild<TextMeshProUGUI>(button.transform, "Badge");
            if (icon != null && badge != null)
            {
                UiIcons.Apply(offer.icon, icon, badge);
            }

            button.onClick.AddListener(() => OnCard(offer));
            Color kind = KindColor(offer.kind);
            Color card = Color.Lerp(RuntimeUI.BackdropColor, kind, 0.1f);
            card.a = 1f;
            button.GetComponent<Image>().color = eliminating ? new Color(0.38f, 0.06f, 0.08f, 1f) : card;
            if (!button.TryGetComponent(out Outline glow))
            {
                glow = button.gameObject.AddComponent<Outline>();
                button.gameObject.AddComponent<CardHover>();
            }
            glow.effectColor = kind;
        }
    }

    private void ClosePanel()
    {
        pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);

        if (pendingLevelUps > 0 && stats != null)
        {
            OpenPanel(stats.level);
            return;
        }

        SetPanelVisible(false);
        ResumeGame();
    }

    private void PauseGame()
    {
        if (isGamePaused)
            return;

        Juice.CancelHitStop();
        Juice.Flash(new Color(1f, 1f, 0.85f, 0.5f), 0.3f);
        Sfx.Play(Sfx.Id.LevelUp, 0.7f);
        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        isGamePaused = true;
    }

    private void ResumeGame()
    {
        if (!isGamePaused)
            return;

        Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        isGamePaused = false;
        if (TryGetComponent(out PlayerAnimator anim)) anim.Glow(new Color(1f, 0.85f, 0.35f));
    }

    private void SetPanelVisible(bool visible)
    {
        isPanelOpen = visible;
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(visible);
        }
    }

    private static float BackOut(float x)
    {
        float p = x - 1f;
        return 1f + 2.70158f * p * p * p + 1.70158f * p * p;
    }

    private void OnCard(Offer offer)
    {
        if (!eliminating)
        {
            if (inputReady) StartCoroutine(ChooseCard(offer));
            return;
        }
        banned.Add(offer.icon);
        Sfx.Play(Sfx.Id.Shatter, 0.6f, 0.8f);
        eliminates--;
        eliminating = false;

        int slot = shown.IndexOf(offer);
        List<Offer> pool = BuildOffers();
        pool.RemoveAll(o => shown.Exists(s => s.icon == o.icon));
        if (pool.Count > 0) shown[slot] = TakeWeighted(pool);
        else shown.RemoveAt(slot);
        ShowOffers();
        RefreshActions();
    }

    private void Reroll()
    {
        if (rerolls <= 0) return;
        rerolls--;
        eliminating = false;
        Sfx.Play(Sfx.Id.Whoosh, 0.5f, 1.6f);
        PopulateButtons();
        RefreshActions();
    }

    private void ToggleCombos()
    {
        if (ComboBook.IsOpen) ComboBook.Close();
        else ComboBook.Open(combosButton.gameObject);
    }

    private void ToggleEliminate()
    {
        if (eliminates <= 0) return;
        eliminating = !eliminating;
        ShowOffers();
        RefreshActions();
    }

    private void Skip()
    {
        if (stats != null) stats.Heal(stats.maxHealth * 0.1f);
        ClosePanel();
    }

    private void BuildActionButtons()
    {
        if (levelUpPanel == null) return;
        bool wasActive = levelUpPanel.activeSelf;
        levelUpPanel.SetActive(true);
        Transform parent = levelUpPanel.transform;
        rerollButton = RuntimeUI.Button(parent, "Reroll", new Vector2(-115, -420), new Vector2(320, 64), Reroll);
        eliminateButton = RuntimeUI.Button(parent, "Eliminate", new Vector2(230, -420), new Vector2(320, 64), ToggleEliminate);
        skipButton = RuntimeUI.Button(parent, "Skip", new Vector2(575, -420), new Vector2(320, 64), Skip);
        combosButton = RuntimeUI.Button(parent, "Combos", new Vector2(-460, -420), new Vector2(320, 64), ToggleCombos);
        levelUpPanel.SetActive(wasActive);
    }

    private void RefreshActions()
    {
        if (rerollButton == null) return;
        SetAction(rerollButton, $"{(GamepadUI.Connected ? "[X]" : "[R]")} Reroll ({rerolls})", rerolls > 0);
        SetAction(eliminateButton, eliminating ? "Pick a card..." : $"{(GamepadUI.Connected ? "[Y]" : "[E]")} Eliminate ({eliminates})", eliminates > 0);
        SetAction(skipButton, GamepadUI.Connected ? "[RB] Skip (+10% HP)" : "[X] Skip (+10% HP)", true);
        SetAction(combosButton, GamepadUI.Connected ? "[LB] Combos" : "[Q] Combos", true);
    }

    private static void SetAction(Button b, string label, bool usable)
    {
        b.interactable = usable;
        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 24;
        t.color = usable ? Color.white : new Color(0.5f, 0.55f, 0.7f);
    }

    private IEnumerator ChooseCard(Offer offer)
    {
        inputReady = false;
        int i = shown.IndexOf(offer);
        Button card = i >= 0 && i < upgradeButtons.Count ? upgradeButtons[i] : null;
        Image image = card != null ? card.GetComponent<Image>() : null;
        Color before = image != null ? image.color : Color.white;
        Sfx.Play(Sfx.Id.Pickup, 0.6f, 1.2f);
        for (float t = 0f; t < 0.18f && card != null; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.18f;
            card.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Mathf.PI * k));
            if (image != null) image.color = Color.Lerp(new Color(1f, 0.85f, 0.35f), before, k);
            yield return null;
        }
        if (card != null) card.transform.localScale = Vector3.one;
        if (image != null) image.color = before;
        ApplyOffer(offer);
    }

    private void ApplyOffer(Offer offer)
    {
        offer.apply();
        ClosePanel();
    }
}

public class CardHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
