using DG.Tweening;
using Player.EffectSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Player
{
    public sealed class EffectBarSegmentView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform _visual;
        [SerializeField] private Image _visualImage;
        [SerializeField] private CanvasGroup _cg;
        [SerializeField] private LayoutElement _layoutElement;

        public RectTransform Slot { get; private set; }
        public RectTransform Visual => _visual;

        private Tween _scaleTween;
        private Tween _fadeTween;

        private void Awake()
        {
            Slot = (RectTransform)transform;

            if (_layoutElement == null)
                _layoutElement = GetComponent<LayoutElement>(); // может быть null и это ок

            if (_cg == null)
            {
                _cg = GetComponent<CanvasGroup>();
                if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
            }
        }
        public void SetIgnoreLayout(bool ignore)
        {
            if (_layoutElement == null) _layoutElement = GetComponent<LayoutElement>();
            if (_layoutElement != null) _layoutElement.ignoreLayout = ignore;
        }

        public void Bind(EffectInstance inst)
        {
            if (_visualImage == null) return;
            _visualImage.color = inst?.Def != null ? inst.Def.Color : Color.clear;
        }

        public void KillTweens()
        {
            _scaleTween?.Kill();
            _fadeTween?.Kill();
            _scaleTween = null;
            _fadeTween = null;
        }

        /// <summary>
        /// FLIP: выставляет visual.scaleX так, чтобы визуально сохранить старую пиксельную ширину
        /// при мгновенном изменении ширины слота Layout'ом.
        /// </summary>
        public void SetFlipScale(float oldWidth, float newWidth)
        {
            if (_visual == null) return;

            float sx = (newWidth <= 0.0001f) ? 1f : Mathf.Clamp(oldWidth / newWidth, 0f, 10f);
            var s = _visual.localScale;
            s.x = sx;
            _visual.localScale = s;
        }

        public void SetVisualScaleX(float x)
        {
            if (_visual == null) return;
            var s = _visual.localScale;
            s.x = x;
            _visual.localScale = s;
        }

        public void AnimateVisualToNormal(float duration, Ease ease)
        {
            if (_visual == null) return;

            KillTweens();
            _scaleTween = _visual.DOScaleX(1f, duration).SetEase(ease).SetUpdate(true);
        }

        public void Fade(float to, float duration, Ease ease)
        {
            if (_cg == null) return;

            _fadeTween?.Kill();
            _fadeTween = _cg.DOFade(to, duration).SetEase(ease).SetUpdate(true);
        }
        public void ResetState()
        {
            KillTweens();

            // <-- ВАЖНО: если сегмент был leaving, он мог быть исключен из layout
            SetIgnoreLayout(false);

            if (_cg != null) _cg.alpha = 1f;

            Slot.localScale = Vector3.one;

            if (_visual != null)
                _visual.localScale = Vector3.one;
        }
        public Tween TweenVisualScaleX(float to, float duration, Ease ease)
        {
            if (_visual == null) return null;
            KillTweens();
            _scaleTween = _visual.DOScaleX(to, duration).SetEase(ease).SetUpdate(true);
            return _scaleTween;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            Slot = (RectTransform)transform;
            if (_visual == null && transform.childCount > 0)
                _visual = transform.GetChild(0) as RectTransform;

            if (_visual != null && _visualImage == null)
                _visualImage = _visual.GetComponent<Image>();

            _cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }
#endif
    }
}