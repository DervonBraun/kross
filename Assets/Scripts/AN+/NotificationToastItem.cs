using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AN_
{
    public sealed class NotificationToastItem : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform _rect;
        [SerializeField] private CanvasGroup _cg;
        [SerializeField] private Image _bg;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Image _timeBar; // Image Type = Filled, Horizontal

        [Header("Layout")]
        [SerializeField] private float _minHeight = 44f;
        [SerializeField] private Vector2 _padding = new(12f, 10f); // x = LR, y = TB
        [SerializeField] private float _titleMessageGap = 2f;

        [Header("Enter (X)")]
        [SerializeField] private float _enterDuration = 0.35f;
        [SerializeField] private Ease _enterEase = Ease.OutCubic;
        [SerializeField] private float _enterOffsetX = 420f;

        [Header("Exit (X)")]
        [SerializeField] private float _exitDuration = 0.28f;
        [SerializeField] private Ease _exitEase = Ease.InCubic;

        private const string TweenIdReflow = "toast_reflow_y";
        private const string TweenIdSlide  = "toast_slide_x";
        private const string TweenIdAlpha  = "toast_alpha";

        private float _lifetime;
        private float _t;
        private bool _closing;
        private bool _entered;
        private Tween _barTween;
        private Action<NotificationToastItem> _onRemove;

        public RectTransform Rect => _rect;
        public bool HasEntered => _entered;

        private void Reset()
        {
            _rect = (RectTransform)transform;
            _cg   = GetComponent<CanvasGroup>();
        }

        /// <param name="bgColor">Цвет фона тоста.</param>
        /// <param name="barColor">Цвет полоски таймера.</param>
        /// <param name="typeIcon">Иконка, соответствующая типу уведомления (может быть null).</param>
        public void SetupContent(
            in Notification n,
            float width,
            float lifetime,
            Color bgColor,
            Color barColor,
            Sprite typeIcon,
            Action<NotificationToastItem> requestRemove)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_cg   == null) _cg   = GetComponent<CanvasGroup>();

            _onRemove = requestRemove;
            _lifetime = Mathf.Max(0.1f, lifetime);
            _t        = 0f;
            _closing  = false;
            _entered  = false;

            // Фон
            if (_bg) _bg.color = bgColor;

            // Иконка: приоритет — typeIcon (из типа), фолбэк — n.icon (из Notification)
            if (_icon)
            {
                var sprite = typeIcon != null ? typeIcon : n.icon;
                _icon.gameObject.SetActive(sprite != null);
                _icon.sprite = sprite;
            }

            // Тексты
            if (_title)   _title.text   = n.title   ?? "";
            if (_message) _message.text = n.message ?? "";

            // ---- compute height for fixed width ----
            float innerW = Mathf.Max(10f, width - _padding.x * 2f);

            float titleH = 0f, msgH = 0f;
            if (_title)
            {
                _title.ForceMeshUpdate();
                titleH = _title.GetPreferredValues(_title.text, innerW, 10000f).y;
            }
            if (_message)
            {
                _message.ForceMeshUpdate();
                msgH = _message.GetPreferredValues(_message.text, innerW, 10000f).y;
            }

            float contentH = titleH + (titleH > 0f && msgH > 0f ? _titleMessageGap : 0f) + msgH;
            float finalH   = Mathf.Max(_minHeight, contentH + _padding.y * 2f);

            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   finalH);

            // Start state (offscreen X, hidden). Y will be set by UI Reflow.
            KillTweens();
            _cg.alpha = 0f;

            var p = _rect.anchoredPosition;
            _rect.anchoredPosition = new Vector2(_enterOffsetX, p.y);

            // time bar (runs in unscaled time)
            if (_timeBar)
            {
                _timeBar.color      = barColor;
                _timeBar.fillAmount = 1f;
                _barTween?.Kill();
                _barTween = DOTween
                    .To(() => _timeBar.fillAmount, v => _timeBar.fillAmount = v, 0f, _lifetime)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true);
            }
        }

        public void SetTargetY(float y, bool animate, float duration, Ease ease, float delay = 0f)
        {
            if (_rect == null) return;

            DOTween.Kill(_rect, TweenIdReflow);

            if (!animate)
            {
                var p = _rect.anchoredPosition;
                _rect.anchoredPosition = new Vector2(p.x, y);
                return;
            }

            _rect.DOAnchorPosY(y, duration)
                .SetEase(ease)
                .SetDelay(delay)
                .SetId(TweenIdReflow)
                .SetUpdate(true);
        }

        public void PlayEnter()
        {
            if (_entered || _closing) return;
            _entered = true;

            DOTween.Kill(_rect, TweenIdSlide);
            DOTween.Kill(_cg,   TweenIdAlpha);

            _rect.DOAnchorPosX(0f, _enterDuration)
                .SetEase(_enterEase)
                .SetId(TweenIdSlide)
                .SetUpdate(true);

            _cg.DOFade(1f, _enterDuration * 0.85f)
                .SetEase(Ease.OutQuad)
                .SetId(TweenIdAlpha)
                .SetUpdate(true);
        }

        public void ForceClose()
        {
            if (_closing) return;
            _closing = true;
            _barTween?.Kill();

            DOTween.Kill(_rect, TweenIdSlide);
            DOTween.Kill(_cg,   TweenIdAlpha);
            DOTween.Kill(_rect, TweenIdReflow);

            float y = _rect.anchoredPosition.y;

            Sequence exit = DOTween.Sequence().SetUpdate(true);
            exit.Join(_rect.DOAnchorPos(new Vector2(_enterOffsetX, y), _exitDuration).SetEase(_exitEase).SetId(TweenIdSlide));
            exit.Join(_cg.DOFade(0f, _exitDuration * 0.85f).SetEase(Ease.InQuad).SetId(TweenIdAlpha));
            exit.OnComplete(() => _onRemove?.Invoke(this));
        }

        private void Update()
        {
            if (_closing) return;

            _t += Time.unscaledDeltaTime;
            if (_t >= _lifetime)
                ForceClose();
        }

        private void KillTweens()
        {
            DOTween.Kill(_rect, TweenIdReflow);
            DOTween.Kill(_rect, TweenIdSlide);
            DOTween.Kill(_cg,   TweenIdAlpha);
            _barTween?.Kill();
        }

        private void OnDestroy() => KillTweens();
    }
}