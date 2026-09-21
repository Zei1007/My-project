using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombieShooter
{
    /// <summary>
    /// Floating on-screen thumbstick. The stick appears wherever the thumb lands inside its zone
    /// rather than sitting in a fixed spot - fixed sticks force players to look down at the screen,
    /// and on a phone that is the difference between playable and not.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform baseRing;
        [SerializeField] RectTransform knob;
        [SerializeField] float radius = 110f;
        [SerializeField] float deadzone = 0.12f;
        [Tooltip("Hide the stick until the player touches the zone.")]
        [SerializeField] bool floating = true;

        Canvas _canvas;
        RectTransform _zone;
        Vector2 _origin;
        int _pointerId = -1;

        public Vector2 Value { get; private set; }
        public bool IsHeld { get { return _pointerId != -1; } }

        void Awake()
        {
            _zone = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            SetVisible(!floating);
        }

        void OnDisable()
        {
            Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId != -1) return;   // already tracking a thumb

            _pointerId = eventData.pointerId;
            _origin = ScreenToLocal(eventData);

            if (floating && baseRing != null) baseRing.anchoredPosition = _origin;
            SetVisible(true);
            UpdateValue(_origin);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            UpdateValue(ScreenToLocal(eventData));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            Release();
        }

        void Release()
        {
            _pointerId = -1;
            Value = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            SetVisible(!floating);
        }

        void UpdateValue(Vector2 localPoint)
        {
            Vector2 delta = localPoint - _origin;
            float magnitude = delta.magnitude;

            // Past the edge the knob pins to the rim but the direction keeps tracking the thumb.
            Vector2 clamped = magnitude > radius ? delta.normalized * radius : delta;
            if (knob != null) knob.anchoredPosition = clamped;

            Vector2 raw = clamped / radius;
            Value = raw.magnitude < deadzone ? Vector2.zero : raw;
        }

        Vector2 ScreenToLocal(PointerEventData eventData)
        {
            var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, eventData.position, camera, out local);
            return local;
        }

        void SetVisible(bool visible)
        {
            if (baseRing != null) baseRing.gameObject.SetActive(visible);
            if (knob != null) knob.gameObject.SetActive(visible);
        }

        /// <summary>Builds a joystick zone in code. Returns the component, already registered.</summary>
        public static VirtualJoystick Create(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                             Color ringColor, Color knobColor, Sprite ringSprite, Sprite knobSprite)
        {
            var zoneGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var zone = zoneGo.GetComponent<RectTransform>();
            zone.SetParent(parent, false);
            zone.anchorMin = anchorMin;
            zone.anchorMax = anchorMax;
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;

            // Invisible but raycastable - this is the region the thumb may land in.
            var zoneImage = zoneGo.GetComponent<Image>();
            zoneImage.color = new Color(0f, 0f, 0f, 0f);
            zoneImage.raycastTarget = true;

            var ring = MakeCircle(zone, "Base", 220f, ringColor, ringSprite);
            var knob = MakeCircle(ring, "Knob", 96f, knobColor, knobSprite);

            var joystick = zoneGo.AddComponent<VirtualJoystick>();
            joystick.baseRing = ring;
            joystick.knob = knob;
            return joystick;
        }

        static RectTransform MakeCircle(Transform parent, string name, float size, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
    }
}
