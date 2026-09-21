using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Sorts a static prop into the same depth band as the characters, so the player walks behind a
    /// tree that is further up the screen and in front of one that is lower. Sorting is evaluated
    /// once on enable - props do not move, and doing this per frame for a few hundred rocks is
    /// wasted work.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class YSortSprite : MonoBehaviour
    {
        [Tooltip("Sorting is measured from this point - usually the base of the prop, not its middle.")]
        [SerializeField] float pivotYOffset;
        [Tooltip("Re-sort every frame. Only needed if the prop moves.")]
        [SerializeField] bool dynamic;

        SpriteRenderer _renderer;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            Apply();
        }

        void LateUpdate()
        {
            if (dynamic) Apply();
        }

        void Apply()
        {
            if (_renderer == null) return;

            float y = transform.position.y + pivotYOffset;
            int order = SortingBands.CharacterBase - Mathf.RoundToInt(y * 100f);
            _renderer.sortingOrder = Mathf.Clamp(order, SortingBands.CharacterMin, SortingBands.CharacterMax);
        }
    }
}
