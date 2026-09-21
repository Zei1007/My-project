using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombieShooter
{
    /// <summary>
    /// The sprites that dress one character. Authored on a ZombieDefinition (or the player prefab)
    /// so a new archetype is a data change, and so real artwork drops straight into these slots.
    /// </summary>
    [Serializable]
    public class CharacterPartSet
    {
        public Sprite head;
        public Sprite torso;
        public Sprite arm;
        public Sprite leg;

        public bool IsValid { get { return head != null && torso != null; } }
    }

    /// <summary>
    /// Holds the part transforms and renderers of an assembled character and owns everything about
    /// how it looks: which sprites are on it, which way it faces, and the hit flash.
    /// CharacterAnimator moves the same transforms; this class does not animate.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        [Header("Joints")]
        public Transform root;
        public Transform torso;
        public Transform head;
        public Transform armBack;
        public Transform armFront;
        public Transform legBack;
        public Transform legFront;
        public Transform weaponPivot;
        public Transform shadow;

        [Header("Renderers")]
        public SpriteRenderer headRenderer;
        public SpriteRenderer torsoRenderer;
        public SpriteRenderer armBackRenderer;
        public SpriteRenderer armFrontRenderer;
        public SpriteRenderer legBackRenderer;
        public SpriteRenderer legFrontRenderer;
        public SpriteRenderer weaponRenderer;
        public SpriteRenderer shadowRenderer;

        [Header("Depth")]
        [SerializeField] SortingGroup sortingGroup;
        [Tooltip("Sprites are sorted by world Y so characters overlap correctly.")]
        [SerializeField] bool sortByDepth = true;

        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        SpriteRenderer[] _bodyRenderers;
        MaterialPropertyBlock _block;
        float _flashAmount;
        int _facing = 1;

        public int Facing { get { return _facing; } }
        public SpriteRenderer[] BodyRenderers { get { return _bodyRenderers; } }

        void Awake()
        {
            Cache();
        }

        void Cache()
        {
            if (_bodyRenderers != null) return;

            _bodyRenderers = new[]
            {
                legBackRenderer, legFrontRenderer, armBackRenderer,
                torsoRenderer, headRenderer, armFrontRenderer,
            };
            _block = new MaterialPropertyBlock();
            if (sortingGroup == null) sortingGroup = GetComponent<SortingGroup>();
        }

        void LateUpdate()
        {
            if (!sortByDepth || sortingGroup == null) return;

            // Lower on screen draws in front. Biased into a high positive band: a plain -y*100
            // goes negative above y=0 and sinks the character behind the floor.
            int order = SortingBands.CharacterBase - Mathf.RoundToInt(transform.position.y * 100f);
            sortingGroup.sortingOrder = Mathf.Clamp(order, SortingBands.CharacterMin, SortingBands.CharacterMax);
        }

        public void ApplyParts(CharacterPartSet parts)
        {
            if (parts == null) return;
            Cache();

            if (headRenderer != null && parts.head != null) headRenderer.sprite = parts.head;
            if (torsoRenderer != null && parts.torso != null) torsoRenderer.sprite = parts.torso;
            if (parts.arm != null)
            {
                if (armBackRenderer != null) armBackRenderer.sprite = parts.arm;
                if (armFrontRenderer != null) armFrontRenderer.sprite = parts.arm;
            }
            if (parts.leg != null)
            {
                if (legBackRenderer != null) legBackRenderer.sprite = parts.leg;
                if (legFrontRenderer != null) legFrontRenderer.sprite = parts.leg;
            }
        }

        /// <summary>Back limbs are darkened so the silhouette still reads when they overlap.</summary>
        public void ApplyDepthShading(float backDarkness = 0.78f)
        {
            Cache();
            var shade = new Color(backDarkness, backDarkness, backDarkness, 1f);
            if (armBackRenderer != null) armBackRenderer.color = shade;
            if (legBackRenderer != null) legBackRenderer.color = shade;
        }

        public void SetWeaponSprite(Sprite sprite)
        {
            if (weaponRenderer == null) return;
            weaponRenderer.sprite = sprite;
            weaponRenderer.enabled = sprite != null;
        }

        /// <summary>Mirrors the whole rig. Uses scale rather than flipX so child offsets mirror too.</summary>
        public void SetFacing(int facing)
        {
            if (facing == 0 || facing == _facing) return;
            _facing = facing < 0 ? -1 : 1;

            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * _facing;
            transform.localScale = scale;
        }

        public void SetFlash(float amount, Color color)
        {
            Cache();
            _flashAmount = Mathf.Clamp01(amount);

            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                var r = _bodyRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_block);
                _block.SetFloat(FlashAmountId, _flashAmount);
                _block.SetColor(FlashColorId, color);
                r.SetPropertyBlock(_block);
            }
        }

        public void SetAlpha(float alpha)
        {
            Cache();
            for (int i = 0; i < _bodyRenderers.Length; i++)
            {
                var r = _bodyRenderers[i];
                if (r == null) continue;
                var c = r.color;
                c.a = alpha;
                r.color = c;
            }
            if (weaponRenderer != null)
            {
                var c = weaponRenderer.color;
                c.a = alpha;
                weaponRenderer.color = c;
            }
            if (shadowRenderer != null)
            {
                var c = shadowRenderer.color;
                c.a = 0.30f * alpha;
                shadowRenderer.color = c;
            }
        }
    }
}
