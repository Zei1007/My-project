using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ZombieShooter
{
    /// <summary>
    /// Builds and drives the whole runtime UI: HUD bars, the level-up choice panel and the game
    /// over screen. Everything it shows arrives through GameEvents, so no gameplay system holds a
    /// reference to the UI.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Palette")]
        [SerializeField] Color healthColor = new Color(0.86f, 0.24f, 0.24f);
        [SerializeField] Color armorColor = new Color(0.45f, 0.72f, 0.95f);
        [SerializeField] Color xpColor = new Color(0.98f, 0.79f, 0.24f);
        [SerializeField] Color panelColor = new Color(0.07f, 0.08f, 0.10f, 0.82f);
        [SerializeField] Color cardColor = new Color(0.14f, 0.16f, 0.20f, 0.98f);

        [Header("Sprites")]
        [Tooltip("Ring sprite for the on-screen thumbstick.")]
        [SerializeField] Sprite joystickRing;
        [Tooltip("Filled circle for the thumbstick knob.")]
        [SerializeField] Sprite joystickKnob;
        [Tooltip("Show the on-screen thumbstick on desktop too, to check the mobile layout.")]
        [SerializeField] bool forceTouchControls;
        [Tooltip("9-sliced ornate frame used for the upgrade cards.")]
        [SerializeField] Sprite cardFrameSprite;
        [Tooltip("9-sliced panel used for HUD backings.")]
        [SerializeField] Sprite panelSprite;
        [Tooltip("Soft radial glow, used for card rarity auras and the bloom.")]
        [SerializeField] Sprite glowSprite;
        [Tooltip("Diamond gem icon shown on each upgrade card.")]
        [SerializeField] Sprite gemSprite;

        [Header("Arcane Palette")]
        [SerializeField] Color arcaneGold = new Color(0.90f, 0.75f, 0.38f);

        Canvas _canvas;
        RectTransform _healthFill;
        RectTransform _armorFill;
        RectTransform _xpFill;
        RectTransform _armorTrack;
        Text _healthText;
        Text _levelText;
        Text _waveText;
        Text _killText;
        Text _buffText;
        Text _toastText;

        RectTransform _levelUpPanel;
        RectTransform _gameOverPanel;
        Text _gameOverText;

        readonly List<UpgradeOption> _currentOptions = new List<UpgradeOption>();
        readonly List<Button> _cardButtons = new List<Button>();

        UpgradeService _upgrades;
        float _toastRemaining;
        float _buffRefreshTimer;
        bool _hasTimedBuffs;

        // Choices owed to the player, in order - one XP pickup can clear several levels, and a
        // blessing can land mid-queue. True = a blessing (buffs only), false = a level-up.
        readonly System.Collections.Generic.Queue<bool> _pendingChoices = new System.Collections.Generic.Queue<bool>();
        Text _levelUpTitle;
        Text _levelUpHint;

        void Awake()
        {
            _upgrades = FindAnyObjectByType<UpgradeService>();
            EnsureEventSystem();
            Build();
        }

        void OnEnable()
        {
            GameEvents.PlayerHealthChanged += OnHealth;
            GameEvents.PlayerArmorChanged += OnArmor;
            GameEvents.XPChanged += OnXP;
            GameEvents.LevelUp += OnLevelUp;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.KillCountChanged += OnKills;
            GameEvents.BuffApplied += OnBuffChanged;
            GameEvents.BuffExpired += OnBuffChanged;
            GameEvents.RunEnded += OnRunEnded;
            GameEvents.BonusChoice += OnBonusChoice;
            GameEvents.LootCollected += OnLootCollected;
        }

        void OnDisable()
        {
            GameEvents.PlayerHealthChanged -= OnHealth;
            GameEvents.PlayerArmorChanged -= OnArmor;
            GameEvents.XPChanged -= OnXP;
            GameEvents.LevelUp -= OnLevelUp;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.KillCountChanged -= OnKills;
            GameEvents.BuffApplied -= OnBuffChanged;
            GameEvents.BuffExpired -= OnBuffChanged;
            GameEvents.RunEnded -= OnRunEnded;
            GameEvents.BonusChoice -= OnBonusChoice;
            GameEvents.LootCollected -= OnLootCollected;
        }

        void Update()
        {
            // Timed buffs count down every frame, so the list is redrawn while any is running -
            // redrawing only on apply/expire froze the countdown at its first value.
            _buffRefreshTimer -= Time.unscaledDeltaTime;
            if (_buffRefreshTimer <= 0f)
            {
                _buffRefreshTimer = 0.2f;
                if (_hasTimedBuffs) OnBuffChanged(null);
            }

            if (_toastRemaining > 0f)
            {
                _toastRemaining -= Time.unscaledDeltaTime;
                if (_toastRemaining <= 0f && _toastText != null) _toastText.text = "";
            }

            // Number keys pick a card - faster than the mouse, and it keeps working if the
            // EventSystem is unhappy for any reason.
            if (_levelUpPanel != null && _levelUpPanel.gameObject.activeSelf)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) Choose(2);
                }
            }
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
        }

        void Build()
        {
            var canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildHud(canvasGo.transform);
            BuildLevelUpPanel(canvasGo.transform);
            BuildGameOverPanel(canvasGo.transform);

            // Boss furniture and touch controls live on the same canvas but own their own logic.
            var bossBar = gameObject.AddComponent<BossBarUI>();
            bossBar.Build(canvasGo.transform);

            var mobile = gameObject.AddComponent<MobileControlsUI>();
            mobile.Build(canvasGo.transform, joystickRing, joystickKnob, forceTouchControls);
        }

        void BuildHud(Transform root)
        {
            var topLeft = new Vector2(0f, 1f);

            // Ornate backing plate behind the stat bars, so the HUD belongs to the same world as
            // the dungeon rather than floating over it.
            if (panelSprite != null)
            {
                var plate = UIFactory.Panel(root, "HudPlate", Color.white);
                var plateImage = plate.GetComponent<Image>();
                plateImage.sprite = panelSprite;
                plateImage.type = Image.Type.Sliced;
                plateImage.pixelsPerUnitMultiplier = 0.34f;
                Place(plate, topLeft, new Vector2(16f, -14f), new Vector2(474f, 104f));
            }

            _healthFill = UIFactory.Bar(root, "HealthBar", panelColor, healthColor,
                new Vector2(30f, -30f), new Vector2(420f, 34f), topLeft);
            _armorFill = UIFactory.Bar(root, "ArmorBar", panelColor, armorColor,
                new Vector2(30f, -70f), new Vector2(420f, 16f), topLeft);
            _armorTrack = _armorFill.parent as RectTransform;
            _xpFill = UIFactory.Bar(root, "XPBar", panelColor, xpColor,
                new Vector2(30f, -94f), new Vector2(420f, 16f), topLeft);

            _healthText = UIFactory.Label(root, "HealthText", "100 / 100", 20, Color.white);
            Place(_healthText.rectTransform, topLeft, new Vector2(42f, -34f), new Vector2(300f, 26f));

            _levelText = UIFactory.Label(root, "LevelText", "LV 1", 22, xpColor);
            Place(_levelText.rectTransform, topLeft, new Vector2(466f, -92f), new Vector2(200f, 26f));

            _waveText = UIFactory.Label(root, "WaveText", "WAVE 1", 34, Color.white, TextAnchor.UpperCenter);
            Place(_waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(400f, 44f));

            _killText = UIFactory.Label(root, "KillText", "KILLS 0", 22, new Color(0.8f, 0.8f, 0.85f), TextAnchor.UpperRight);
            Place(_killText.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(240f, 28f));

            _buffText = UIFactory.Label(root, "BuffText", "", 18, new Color(0.7f, 0.85f, 1f), TextAnchor.LowerLeft);
            Place(_buffText.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(700f, 160f));

            // Sits below the boss nameplate's band so the two never overlap on a boss wave.
            _toastText = UIFactory.Label(root, "Toast", "", 30, xpColor, TextAnchor.MiddleCenter);
            Place(_toastText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(900f, 40f));
        }

        void BuildLevelUpPanel(Transform root)
        {
            // Deep arcane wash rather than flat black - the choice screen should feel like a ritual,
            // not a pause menu.
            _levelUpPanel = UIFactory.Panel(root, "LevelUpPanel", new Color(0.05f, 0.03f, 0.10f, 0.88f));
            UIFactory.Stretch(_levelUpPanel);
            _levelUpPanel.GetComponent<Image>().raycastTarget = true;

            if (glowSprite != null)
            {
                // A soft bloom behind the cards, so the panel has a centre of light.
                var bloom = UIFactory.Panel(_levelUpPanel, "Bloom", new Color(0.45f, 0.30f, 0.85f, 0.22f));
                bloom.GetComponent<Image>().sprite = glowSprite;
                Place(bloom, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 900f));
            }

            var title = UIFactory.Label(_levelUpPanel, "Title", "LEVEL UP", 58, arcaneGold, TextAnchor.MiddleCenter);
            _levelUpTitle = title;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(900f, 72f));

            var rule = UIFactory.Panel(_levelUpPanel, "Rule", new Color(arcaneGold.r, arcaneGold.g, arcaneGold.b, 0.55f));
            Place(rule, new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(420f, 2f));

            var hint = UIFactory.Label(_levelUpPanel, "Hint", "CHOOSE YOUR POWER", 22,
                new Color(0.72f, 0.66f, 0.88f), TextAnchor.MiddleCenter);
            _levelUpHint = hint;
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(900f, 34f));

            const int cardCount = 3;
            const float cardWidth = 360f;
            const float cardHeight = 430f;
            const float spacing = 34f;
            float totalWidth = cardCount * cardWidth + (cardCount - 1) * spacing;

            for (int i = 0; i < cardCount; i++)
            {
                var button = UIFactory.CardButton(_levelUpPanel, Color.white);
                var rect = button.GetComponent<RectTransform>();
                float x = -totalWidth * 0.5f + cardWidth * 0.5f + i * (cardWidth + spacing);
                Place(rect, new Vector2(0.5f, 0.5f), new Vector2(x, -40f), new Vector2(cardWidth, cardHeight));

                // Ornate 9-sliced frame as the card itself.
                var image = button.GetComponent<Image>();
                if (cardFrameSprite != null)
                {
                    image.sprite = cardFrameSprite;
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = 0.25f;   // frame is 32px authored; scale it up
                }
                image.color = Color.white;

                // Rarity glow sits behind the frame and is tinted per option.
                var glow = UIFactory.Panel(rect, "Glow", new Color(1f, 1f, 1f, 0f));
                if (glowSprite != null) glow.GetComponent<Image>().sprite = glowSprite;
                Place(glow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cardWidth + 120f, cardHeight + 120f));
                glow.SetAsFirstSibling();

                // Gem icon, tinted by what the card offers.
                var gem = UIFactory.Panel(rect, "Gem", Color.white);
                if (gemSprite != null) gem.GetComponent<Image>().sprite = gemSprite;
                Place(gem, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(64f, 64f));

                var tag = UIFactory.Label(rect, "Tag", "", 20, arcaneGold, TextAnchor.UpperCenter);
                Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(cardWidth - 50f, 28f));

                var name = UIFactory.Label(rect, "Name", "", 31, Color.white, TextAnchor.UpperCenter);
                Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(cardWidth - 50f, 84f));

                var divider = UIFactory.Panel(rect, "Divider", new Color(arcaneGold.r, arcaneGold.g, arcaneGold.b, 0.35f));
                Place(divider, new Vector2(0.5f, 1f), new Vector2(0f, -238f), new Vector2(cardWidth - 110f, 2f));

                var body = UIFactory.Label(rect, "Body", "", 20, new Color(0.80f, 0.78f, 0.90f), TextAnchor.UpperCenter);
                Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -256f), new Vector2(cardWidth - 64f, 140f));

                var key = UIFactory.Label(rect, "Key", (i + 1).ToString(), 22,
                    new Color(arcaneGold.r, arcaneGold.g, arcaneGold.b, 0.85f), TextAnchor.LowerCenter);
                Place(key.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(60f, 30f));

                int index = i;
                button.onClick.AddListener(delegate { Choose(index); });
                _cardButtons.Add(button);
            }

            _levelUpPanel.gameObject.SetActive(false);
        }

        /// <summary>Colour language for the cards: gold is a new weapon, blue a blessing, red a curse.</summary>
        Color RarityColor(UpgradeOption option)
        {
            switch (option.Kind)
            {
                case UpgradeKind.NewWeapon: return arcaneGold;
                case UpgradeKind.WeaponLevel: return new Color(1f, 0.58f, 0.25f);
                default:
                    return option.Buff != null && option.Buff.isDebuff
                        ? new Color(0.95f, 0.30f, 0.35f)
                        : new Color(0.45f, 0.72f, 1f);
            }
        }

        void BuildGameOverPanel(Transform root)
        {
            _gameOverPanel = UIFactory.Panel(root, "GameOverPanel", new Color(0.05f, 0.01f, 0.01f, 0.88f));
            UIFactory.Stretch(_gameOverPanel);
            _gameOverPanel.GetComponent<Image>().raycastTarget = true;

            var title = UIFactory.Label(_gameOverPanel, "Title", "YOU DIED", 72, healthColor, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 90f));

            _gameOverText = UIFactory.Label(_gameOverPanel, "Summary", "", 28, Color.white, TextAnchor.MiddleCenter);
            Place(_gameOverText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(900f, 120f));

            var restart = UIFactory.CardButton(_gameOverPanel, cardColor);
            Place(restart.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(320f, 70f));
            var restartLabel = UIFactory.Label(restart.transform, "Label", "RESTART  (R)", 26, Color.white, TextAnchor.MiddleCenter);
            UIFactory.Stretch(restartLabel.rectTransform);
            restart.onClick.AddListener(delegate
            {
                if (GameManager.Instance != null) GameManager.Instance.Restart();
            });

            _gameOverPanel.gameObject.SetActive(false);
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // --- event handlers --------------------------------------------------

        void OnHealth(float current, float max)
        {
            UIFactory.SetFill(_healthFill, max <= 0f ? 0f : current / max);
            if (_healthText != null) _healthText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);
        }

        void OnArmor(float current, float max)
        {
            UIFactory.SetFill(_armorFill, max <= 0f ? 0f : current / max);

            // No armor at all: hide the track entirely rather than showing an empty slot.
            if (_armorTrack != null) _armorTrack.gameObject.SetActive(max > 0f);
        }

        void OnXP(int level, float current, float required)
        {
            UIFactory.SetFill(_xpFill, required <= 0f ? 0f : current / required);
            if (_levelText != null) _levelText.text = "LV " + level;
        }

        void OnWaveStarted(int wave)
        {
            if (_waveText != null) _waveText.text = "WAVE " + wave;
            if (_toastText != null) _toastText.color = xpColor;
            Toast("WAVE " + wave, 2f);
        }

        void OnKills(int kills)
        {
            if (_killText != null) _killText.text = "KILLS " + kills;
        }

        void OnBuffChanged(BuffDefinition definition)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || _buffText == null) return;

            var sb = new System.Text.StringBuilder();
            var active = player.Buffs.Active;
            _hasTimedBuffs = false;
            for (int i = 0; i < active.Count; i++)
            {
                var buff = active[i];
                if (buff.Expires) _hasTimedBuffs = true;
                sb.Append(buff.Definition.displayName);
                if (buff.Stacks > 1) sb.Append(" x").Append(buff.Stacks);
                if (buff.Expires) sb.Append(" (").Append(Mathf.CeilToInt(buff.Remaining)).Append("s)");
                sb.Append('\n');
            }
            _buffText.text = sb.ToString();
        }

        void Toast(string message, float seconds)
        {
            if (_toastText == null) return;
            _toastText.text = message;
            _toastRemaining = seconds;
        }

        void OnLevelUp(int level)
        {
            QueueChoice(false);
        }

        /// <summary>Elite/boss loot: a free pick from buffs only.</summary>
        void OnBonusChoice()
        {
            QueueChoice(true);
        }

        void QueueChoice(bool blessing)
        {
            _pendingChoices.Enqueue(blessing);
            if (!_levelUpPanel.gameObject.activeSelf) ShowChoices();
        }

        void OnLootCollected(string label, Color color)
        {
            if (_toastText == null) return;
            _toastText.color = new Color(color.r, color.g, color.b, 1f);
            Toast("+ " + label, 1.6f);
        }

        void ShowChoices()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null || _upgrades == null || _pendingChoices.Count == 0)
            {
                // Nothing to offer - do not strand the player on a paused screen.
                _pendingChoices.Clear();
                _levelUpPanel.gameObject.SetActive(false);
                if (GameManager.Instance != null) GameManager.Instance.Resume();
                return;
            }

            bool blessing = _pendingChoices.Peek();
            if (_levelUpTitle != null) _levelUpTitle.text = blessing ? "BLESSING" : "LEVEL UP";
            if (_levelUpHint != null) _levelUpHint.text = blessing ? "A GIFT FROM THE FALLEN" : "CHOOSE YOUR POWER";

            _currentOptions.Clear();
            _currentOptions.AddRange(_upgrades.Roll(player, blessing));

            if (_currentOptions.Count == 0)
            {
                // Everything is maxed out - skip this choice and move to the next, if any.
                _pendingChoices.Dequeue();
                if (_pendingChoices.Count > 0) { ShowChoices(); return; }
                _levelUpPanel.gameObject.SetActive(false);
                GameManager.Instance.Resume();
                return;
            }

            for (int i = 0; i < _cardButtons.Count; i++)
            {
                bool used = i < _currentOptions.Count;
                _cardButtons[i].gameObject.SetActive(used);
                if (!used) continue;

                var option = _currentOptions[i];
                var root = _cardButtons[i].transform;

                SetChildText(root, "Tag", option.Tag);
                SetChildText(root, "Name", option.Title);
                SetChildText(root, "Body", option.Body);

                // The frame keeps its own artwork; rarity is carried by the aura and the gem.
                Color rarity = RarityColor(option);
                _cardButtons[i].GetComponent<Image>().color = Color.white;

                var glow = root.Find("Glow");
                if (glow != null)
                {
                    var glowImage = glow.GetComponent<Image>();
                    glowImage.color = new Color(rarity.r, rarity.g, rarity.b, 0.34f);
                }

                var gem = root.Find("Gem");
                if (gem != null) gem.GetComponent<Image>().color = rarity;

                var tag = root.Find("Tag");
                if (tag != null) tag.GetComponent<Text>().color = rarity;
            }

            _levelUpPanel.gameObject.SetActive(true);

            var eventSystem = EventSystem.current;
            if (eventSystem != null && _cardButtons.Count > 0)
                eventSystem.SetSelectedGameObject(_cardButtons[0].gameObject);
        }

        static void SetChildText(Transform root, string childName, string value)
        {
            var child = root.Find(childName);
            if (child == null) return;
            var label = child.GetComponent<Text>();
            if (label != null) label.text = value;
        }

        void Choose(int index)
        {
            if (index < 0 || index >= _currentOptions.Count) return;

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            _currentOptions[index].Apply(player);
            _currentOptions.Clear();

            if (_pendingChoices.Count > 0) _pendingChoices.Dequeue();

            // Another choice is still owed: re-roll and stay paused.
            if (_pendingChoices.Count > 0)
            {
                ShowChoices();
                return;
            }

            _levelUpPanel.gameObject.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.Resume();
        }

        void OnRunEnded(int wave)
        {
            if (_levelUpPanel != null) _levelUpPanel.gameObject.SetActive(false);
            if (_gameOverPanel == null) return;

            int kills = GameManager.Instance != null ? GameManager.Instance.KillCount : 0;
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            int level = player != null && player.Experience != null ? player.Experience.Level : 1;

            _gameOverText.text = "Reached wave " + wave + "   -   " + kills + " kills   -   level " + level;
            _gameOverPanel.gameObject.SetActive(true);
        }
    }
}
