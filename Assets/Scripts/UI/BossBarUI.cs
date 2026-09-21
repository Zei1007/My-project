using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieShooter
{
    /// <summary>
    /// Boss nameplate, health bar and phase pips, plus the entrance banner. Tracks the live boss
    /// through GameEvents, so it works for any BossDefinition without wiring.
    /// </summary>
    public class BossBarUI : MonoBehaviour
    {
        static readonly Color BarBack = new Color(0.06f, 0.05f, 0.08f, 0.85f);
        static readonly Color BarFill = new Color(0.82f, 0.18f, 0.32f);
        static readonly Color PipOn = new Color(1f, 0.85f, 0.35f);
        static readonly Color PipOff = new Color(0.3f, 0.3f, 0.36f);

        RectTransform _root;
        RectTransform _fill;
        Text _nameText;
        Text _phaseText;
        readonly List<Image> _pips = new List<Image>();

        RectTransform _banner;
        Text _bannerTitle;
        Text _bannerLine;
        float _bannerRemaining;

        ZombieController _boss;

        public void Build(Transform canvas)
        {
            // --- bar ---
            _root = UIFactory.Panel(canvas, "BossBar", new Color(0f, 0f, 0f, 0f));
            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -86f);
            _root.sizeDelta = new Vector2(900f, 96f);

            _nameText = UIFactory.Label(_root, "Name", "BOSS", 30, new Color(1f, 0.9f, 0.9f), TextAnchor.MiddleCenter);
            Place(_nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(880f, 36f));

            _fill = UIFactory.Bar(_root, "BossHealth", BarBack, BarFill,
                new Vector2(0f, -38f), new Vector2(860f, 26f), new Vector2(0.5f, 1f));

            _phaseText = UIFactory.Label(_root, "Phase", "", 20, new Color(0.9f, 0.75f, 0.55f), TextAnchor.MiddleCenter);
            Place(_phaseText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(880f, 26f));

            _root.gameObject.SetActive(false);

            // --- entrance banner ---
            _banner = UIFactory.Panel(canvas, "BossBanner", new Color(0.04f, 0.01f, 0.06f, 0.55f));
            _banner.anchorMin = new Vector2(0f, 0.5f);
            _banner.anchorMax = new Vector2(1f, 0.5f);
            _banner.pivot = new Vector2(0.5f, 0.5f);
            _banner.anchoredPosition = Vector2.zero;
            _banner.sizeDelta = new Vector2(0f, 220f);

            _bannerTitle = UIFactory.Label(_banner, "Title", "", 68, new Color(1f, 0.32f, 0.38f), TextAnchor.MiddleCenter);
            Place(_bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(1400f, 86f));

            _bannerLine = UIFactory.Label(_banner, "Line", "", 26, new Color(0.88f, 0.86f, 0.9f), TextAnchor.MiddleCenter);
            Place(_bannerLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(1400f, 40f));

            _banner.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameEvents.BossSpawned += OnBossSpawned;
            GameEvents.BossPhaseChanged += OnPhaseChanged;
            GameEvents.BossDefeated += OnBossDefeated;
        }

        void OnDisable()
        {
            GameEvents.BossSpawned -= OnBossSpawned;
            GameEvents.BossPhaseChanged -= OnPhaseChanged;
            GameEvents.BossDefeated -= OnBossDefeated;
        }

        void OnBossSpawned(ZombieController boss, BossDefinition definition)
        {
            _boss = boss;
            if (_root == null) return;

            // Most bosses name themselves once; only show both when they actually differ.
            string plate = string.Equals(definition.title, definition.displayName, System.StringComparison.OrdinalIgnoreCase)
                ? definition.title
                : definition.title + "  -  " + definition.displayName;
            _nameText.text = plate.ToUpperInvariant();
            _phaseText.text = "";
            UIFactory.SetFill(_fill, 1f);
            BuildPips(definition.phases.Count);
            _root.gameObject.SetActive(true);

            _bannerTitle.text = definition.title.ToUpperInvariant();
            _bannerLine.text = definition.introLine;
            _banner.gameObject.SetActive(true);
            _bannerRemaining = Mathf.Max(0.5f, definition.introDuration);
        }

        void BuildPips(int count)
        {
            for (int i = 0; i < _pips.Count; i++)
                if (_pips[i] != null) Destroy(_pips[i].gameObject);
            _pips.Clear();

            const float pipWidth = 46f;
            const float spacing = 8f;
            float total = count * pipWidth + (count - 1) * spacing;

            for (int i = 0; i < count; i++)
            {
                var pip = UIFactory.Panel(_root, "Pip" + i, PipOff);
                float x = -total * 0.5f + pipWidth * 0.5f + i * (pipWidth + spacing);
                Place(pip, new Vector2(0.5f, 1f), new Vector2(x, -68f), new Vector2(pipWidth, 6f));
                _pips.Add(pip.GetComponent<Image>());
            }
        }

        void OnPhaseChanged(int index, int total, string phaseName)
        {
            if (_phaseText != null) _phaseText.text = phaseName;

            // Pips light up as phases are cleared, so progress is visible at a glance.
            for (int i = 0; i < _pips.Count; i++)
                if (_pips[i] != null) _pips[i].color = i <= index ? PipOn : PipOff;
        }

        void OnBossDefeated(BossDefinition definition)
        {
            _boss = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_bannerRemaining > 0f)
            {
                _bannerRemaining -= Time.unscaledDeltaTime;
                if (_bannerRemaining <= 0f && _banner != null) _banner.gameObject.SetActive(false);
            }

            if (_boss == null || _root == null || !_root.gameObject.activeSelf) return;

            if (!_boss.IsAlive)
            {
                _root.gameObject.SetActive(false);
                _boss = null;
                return;
            }

            float max = _boss.Health.Max;
            UIFactory.SetFill(_fill, max > 0f ? _boss.Health.Current / max : 0f);
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
