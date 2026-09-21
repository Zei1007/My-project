using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Procedural character animation. Everything is driven by sine curves against a walk phase
    /// rather than authored clips: it costs no animation assets, scales to every archetype from one
    /// script, and the timing follows actual move speed instead of fighting it.
    /// </summary>
    [RequireComponent(typeof(CharacterRig))]
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("Locomotion")]
        [Tooltip("Steps per second at full speed.")]
        [SerializeField] float stepRate = 2.8f;
        [SerializeField] float legSwing = 32f;
        [SerializeField] float armSwing = 22f;
        [SerializeField] float bodyBob = 0.05f;
        [SerializeField] float leanAngle = 5f;

        [Header("Pixel Mode")]
        [Tooltip("Animate by whole-pixel offsets instead of rotation. Rotating a 9px limb shimmers.")]
        [SerializeField] bool pixelMode = true;
        [Tooltip("Pixels per unit the art was authored at - offsets snap to this grid.")]
        [SerializeField] float pixelsPerUnit = 32f;
        [Tooltip("How far a leg lifts on each step, in pixels.")]
        [SerializeField] float stepLiftPixels = 2f;
        [Tooltip("How far the body bobs while running, in pixels.")]
        [SerializeField] float bobPixels = 1f;

        [Header("Idle")]
        [SerializeField] float idleBreathRate = 1.6f;
        [SerializeField] float idleBreathAmount = 0.025f;

        [Header("Reactions")]
        [SerializeField] float hitFlashDuration = 0.10f;
        [SerializeField] Color hitFlashColor = Color.white;
        [SerializeField] float attackRecoil = 34f;
        [SerializeField] float attackRecoverRate = 7f;

        [Header("Death")]
        [SerializeField] float deathDuration = 0.55f;
        [SerializeField] float deathSpin = 80f;

        CharacterRig _rig;

        Vector3 _torsoBase, _headBase, _rigBaseScale;
        Vector3 _legFrontBase, _legBackBase, _armFrontBase, _armBackBase;
        float _walkPhase;
        float _speed01;
        float _flashRemaining;
        float _attackKick;
        float _hitSquash;
        float _deathTimer = -1f;
        bool _dead;

        public bool IsPlayingDeath { get { return _dead; } }

        void Awake()
        {
            _rig = GetComponent<CharacterRig>();
            CacheBasePose();
        }

        void CacheBasePose()
        {
            if (_rig.torso != null) _torsoBase = _rig.torso.localPosition;
            if (_rig.head != null) _headBase = _rig.head.localPosition;
            if (_rig.legFront != null) _legFrontBase = _rig.legFront.localPosition;
            if (_rig.legBack != null) _legBackBase = _rig.legBack.localPosition;
            if (_rig.armFront != null) _armFrontBase = _rig.armFront.localPosition;
            if (_rig.armBack != null) _armBackBase = _rig.armBack.localPosition;
            _rigBaseScale = transform.localScale;
        }

        /// <summary>Snaps an offset to the art's pixel grid so nothing lands on a half pixel.</summary>
        float SnapPixels(float pixels)
        {
            return Mathf.Round(pixels) / pixelsPerUnit;
        }

        /// <summary>Called by the pool when an instance is reused, so no state leaks between lives.</summary>
        public void ResetAnimation()
        {
            _walkPhase = 0f;
            _speed01 = 0f;
            _flashRemaining = 0f;
            _attackKick = 0f;
            _hitSquash = 0f;
            _deathTimer = -1f;
            _dead = false;

            transform.localScale = new Vector3(Mathf.Abs(_rigBaseScale.x) * _rig.Facing, _rigBaseScale.y, _rigBaseScale.z);
            transform.localRotation = Quaternion.identity;

            if (_rig.torso != null) { _rig.torso.localPosition = _torsoBase; _rig.torso.localRotation = Quaternion.identity; }
            if (_rig.head != null) { _rig.head.localPosition = _headBase; _rig.head.localRotation = Quaternion.identity; }
            if (_rig.legFront != null) { _rig.legFront.localPosition = _legFrontBase; _rig.legFront.localRotation = Quaternion.identity; }
            if (_rig.legBack != null) { _rig.legBack.localPosition = _legBackBase; _rig.legBack.localRotation = Quaternion.identity; }
            if (_rig.armFront != null) { _rig.armFront.localPosition = _armFrontBase; _rig.armFront.localRotation = Quaternion.identity; }
            if (_rig.armBack != null) { _rig.armBack.localPosition = _armBackBase; _rig.armBack.localRotation = Quaternion.identity; }

            _rig.SetAlpha(1f);
            _rig.SetFlash(0f, hitFlashColor);
        }

        /// <summary>0 = standing still, 1 = running flat out.</summary>
        public void SetLocomotion(float normalizedSpeed)
        {
            _speed01 = Mathf.Clamp01(normalizedSpeed);
        }

        public void SetFacingFromVelocity(float xVelocity)
        {
            if (Mathf.Abs(xVelocity) < 0.05f) return;
            _rig.SetFacing(xVelocity < 0f ? -1 : 1);
        }

        public void PlayAttack()
        {
            _attackKick = 1f;
        }

        public void PlayHit()
        {
            _flashRemaining = hitFlashDuration;
            _hitSquash = 1f;
        }

        public void PlayDeath()
        {
            if (_dead) return;
            _dead = true;
            _deathTimer = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_dead) { TickDeath(dt); return; }

            TickFlash(dt);
            TickLocomotion(dt);
            TickReactions(dt);
        }

        void TickFlash(float dt)
        {
            if (_flashRemaining <= 0f) return;

            _flashRemaining -= dt;
            float t = Mathf.Clamp01(_flashRemaining / hitFlashDuration);
            _rig.SetFlash(t, hitFlashColor);
        }

        void TickLocomotion(float dt)
        {
            _walkPhase += _speed01 * stepRate * Mathf.PI * 2f * dt;

            float swing = Mathf.Sin(_walkPhase);
            float moving = _speed01;

            if (pixelMode)
            {
                TickPixelLocomotion(swing, moving);
                return;
            }

            // Legs and arms counter-swing.
            if (_rig.legFront != null) _rig.legFront.localRotation = Quaternion.Euler(0f, 0f, swing * legSwing * moving);
            if (_rig.legBack != null) _rig.legBack.localRotation = Quaternion.Euler(0f, 0f, -swing * legSwing * moving);
            if (_rig.armFront != null) _rig.armFront.localRotation = Quaternion.Euler(0f, 0f, -swing * armSwing * moving - _attackKick * attackRecoil);
            if (_rig.armBack != null) _rig.armBack.localRotation = Quaternion.Euler(0f, 0f, swing * armSwing * moving);

            // Body rises on each step, plus a slow idle breath when standing.
            float bob = Mathf.Abs(Mathf.Sin(_walkPhase)) * bodyBob * moving;
            float breath = Mathf.Sin(Time.time * idleBreathRate) * idleBreathAmount * (1f - moving);

            if (_rig.torso != null) _rig.torso.localPosition = _torsoBase + new Vector3(0f, bob + breath, 0f);
            if (_rig.head != null)
            {
                _rig.head.localPosition = _headBase + new Vector3(0f, bob * 1.15f + breath, 0f);
                _rig.head.localRotation = Quaternion.Euler(0f, 0f, -swing * 3f * moving);
            }

            // Lean into the run.
            transform.localRotation = Quaternion.Euler(0f, 0f, -leanAngle * moving);
        }

        /// <summary>
        /// Pixel-art walk: limbs step in whole-pixel vertical offsets rather than rotating, and the
        /// body bobs on the same grid. Rotating a 9-pixel-wide limb crawls with resampling
        /// artefacts, which is the thing that makes pixel art look wrong in a 3D engine.
        /// </summary>
        void TickPixelLocomotion(float swing, float moving)
        {
            float lift = stepLiftPixels * moving;
            float frontLift = SnapPixels(Mathf.Max(0f, swing) * lift);
            float backLift = SnapPixels(Mathf.Max(0f, -swing) * lift);

            if (_rig.legFront != null) _rig.legFront.localPosition = _legFrontBase + new Vector3(0f, frontLift, 0f);
            if (_rig.legBack != null) _rig.legBack.localPosition = _legBackBase + new Vector3(0f, backLift, 0f);

            // Arms counter the legs; the front arm kicks back when a shot goes out.
            float armPixels = stepLiftPixels * 0.5f * moving;
            float recoil = _attackKick * 2f;
            if (_rig.armFront != null)
                _rig.armFront.localPosition = _armFrontBase + new Vector3(SnapPixels(-recoil), SnapPixels(-swing * armPixels), 0f);
            if (_rig.armBack != null)
                _rig.armBack.localPosition = _armBackBase + new Vector3(0f, SnapPixels(swing * armPixels), 0f);

            // Body bob on the step, plus a one-pixel idle breath when standing still.
            float bob = SnapPixels(Mathf.Abs(swing) * bobPixels * moving);
            float breath = moving < 0.05f && Mathf.Sin(Time.time * idleBreathRate) > 0f ? SnapPixels(1f) : 0f;

            if (_rig.torso != null) _rig.torso.localPosition = _torsoBase + new Vector3(0f, bob + breath, 0f);
            if (_rig.head != null)
            {
                _rig.head.localPosition = _headBase + new Vector3(0f, bob + breath, 0f);
                _rig.head.localRotation = Quaternion.identity;
            }

            // No run lean: rotating the whole rig would resample every part.
            transform.localRotation = Quaternion.identity;
        }

        void TickReactions(float dt)
        {
            if (_attackKick > 0f) _attackKick = Mathf.MoveTowards(_attackKick, 0f, attackRecoverRate * dt);
            if (_hitSquash > 0f) _hitSquash = Mathf.MoveTowards(_hitSquash, 0f, 6f * dt);

            // Squash on hit, stretch on the attack kick - the classic readability trick. Kept
            // coarse in pixel mode: fine scaling resamples the sprites every frame.
            float squashAmount = pixelMode ? 0.08f : 0.16f;
            float squash = 1f - _hitSquash * squashAmount + _attackKick * 0.04f;
            float stretch = 1f + _hitSquash * squashAmount - _attackKick * 0.03f;

            var scale = _rigBaseScale;
            transform.localScale = new Vector3(Mathf.Abs(scale.x) * stretch * _rig.Facing, scale.y * squash, scale.z);
        }

        void TickDeath(float dt)
        {
            _deathTimer += dt;
            float t = Mathf.Clamp01(_deathTimer / deathDuration);

            _rig.SetAlpha(1f - t);
            transform.localRotation = Quaternion.Euler(0f, 0f, -deathSpin * t * _rig.Facing);

            float shrink = Mathf.Lerp(1f, 0.55f, t);
            transform.localScale = new Vector3(Mathf.Abs(_rigBaseScale.x) * shrink * _rig.Facing,
                                               _rigBaseScale.y * shrink, _rigBaseScale.z);
        }
    }
}
