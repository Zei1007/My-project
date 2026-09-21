using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Animates a torch: cycles the flame frames and breathes a glow sprite underneath.
    ///
    /// The glow is a sprite rather than a Light2D on purpose - the characters render through an
    /// unlit flash shader, so real 2D lights would not touch them, and a dozen lights is a cost a
    /// phone should not pay for decoration.
    /// </summary>
    public class TorchFlicker : MonoBehaviour
    {
        [SerializeField] SpriteRenderer flameRenderer;
        [SerializeField] SpriteRenderer glowRenderer;
        [SerializeField] Sprite[] frames;

        [Header("Flame")]
        [SerializeField] float framesPerSecond = 9f;

        [Header("Glow")]
        [SerializeField] Color glowColor = new Color(1f, 0.62f, 0.25f, 0.30f);
        [SerializeField] float glowScale = 3.2f;
        [SerializeField] float pulseAmount = 0.12f;
        [SerializeField] float pulseSpeed = 5.5f;

        float _timer;
        int _frame;
        float _phase;

        void Awake()
        {
            // Offset each torch so a row of them does not flicker in lockstep.
            _phase = Random.value * 10f;
            _frame = Random.Range(0, Mathf.Max(1, frames != null ? frames.Length : 1));

            if (glowRenderer != null)
            {
                glowRenderer.color = glowColor;
                glowRenderer.transform.localScale = Vector3.one * glowScale;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (frames != null && frames.Length > 0 && flameRenderer != null)
            {
                _timer += dt;
                float interval = 1f / Mathf.Max(0.01f, framesPerSecond);
                if (_timer >= interval)
                {
                    _timer -= interval;
                    _frame = (_frame + 1) % frames.Length;
                    flameRenderer.sprite = frames[_frame];
                }
            }

            if (glowRenderer != null)
            {
                float pulse = 1f + Mathf.Sin((Time.time + _phase) * pulseSpeed) * pulseAmount;
                glowRenderer.transform.localScale = Vector3.one * (glowScale * pulse);

                var c = glowColor;
                c.a = glowColor.a * pulse;
                glowRenderer.color = c;
            }
        }
    }
}
