using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Makes a dropped soul gem feel alive: it bobs, its halo breathes, and it flares as it is
    /// pulled in. Driven from one sine so every orb on screen costs almost nothing, and offset per
    /// orb so a field of them does not pulse in unison.
    /// </summary>
    public class OrbVisual : MonoBehaviour
    {
        [SerializeField] Transform gem;
        [SerializeField] SpriteRenderer gemRenderer;
        [SerializeField] SpriteRenderer glowRenderer;

        [Header("Idle")]
        [Tooltip("Bob height in pixels, snapped to the art grid.")]
        [SerializeField] float bobPixels = 2f;
        [SerializeField] float bobSpeed = 2.6f;
        [SerializeField] float pixelsPerUnit = 32f;

        [Header("Halo")]
        [SerializeField] Color glowColor = new Color(0.35f, 0.85f, 1f, 0.45f);
        [SerializeField] float glowScale = 1.5f;
        [SerializeField] float glowPulse = 0.18f;

        [Header("Attract")]
        [Tooltip("Extra halo and lift once the orb starts homing toward the player.")]
        [SerializeField] float attractGlowBoost = 1.9f;

        float _phase;
        Vector3 _gemBase;
        bool _attracting;

        void Awake()
        {
            _phase = Random.value * 10f;
            if (gem != null) _gemBase = gem.localPosition;
            if (glowRenderer != null) glowRenderer.color = glowColor;
        }

        void OnEnable()
        {
            _attracting = false;
            _phase = Random.value * 10f;
        }

        /// <summary>Loot uses one prefab for every reward type, so its halo colour is set per drop.</summary>
        public void SetGlowColor(Color color)
        {
            glowColor = color;
            if (glowRenderer != null) glowRenderer.color = color;
        }

        /// <summary>Called by XPOrb when the pickup radius grabs it.</summary>
        public void SetAttracting(bool attracting)
        {
            _attracting = attracting;
        }

        void Update()
        {
            float t = Time.time + _phase;

            if (gem != null)
            {
                // Snap the bob to whole pixels - a smooth sub-pixel bob shimmers on pixel art.
                float bob = Mathf.Round(Mathf.Sin(t * bobSpeed) * bobPixels) / pixelsPerUnit;
                gem.localPosition = _gemBase + new Vector3(0f, bob, 0f);
            }

            if (glowRenderer == null) return;

            float pulse = 1f + Mathf.Sin(t * bobSpeed * 1.7f) * glowPulse;
            float boost = _attracting ? attractGlowBoost : 1f;
            glowRenderer.transform.localScale = Vector3.one * (glowScale * pulse * boost);

            var c = glowColor;
            c.a = glowColor.a * pulse * (_attracting ? 1.5f : 1f);
            glowRenderer.color = c;
        }
    }
}
