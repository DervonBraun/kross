using DG.Tweening;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Базовый компонент элемента UI-последовательности.
    /// Вешается на любой GameObject с CanvasGroup.
    ///
    /// Поддерживает: Fade, Slide (4 направления), Scale, и их комбинации.
    ///
    /// Можно наследоваться и переопределить OnShow/OnHide для кастомного поведения
    /// (например typing effect, волна полосок и т.д.).
    ///
    /// UISequencePlayer вызывает Show/Hide автоматически.
    /// Можно также вызывать напрямую.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UISequenceElement : MonoBehaviour
    {
        [Header("UISequenceElement")]
        [Tooltip("Если true — объект деактивируется когда полностью скрыт.")]
        [SerializeField] protected bool _deactivateWhenHidden = true;

        // ── Cached ────────────────────────────────────────────────────────
        private CanvasGroup    _group;
        private RectTransform  _rect;
        private Vector2        _originalAnchoredPos;
        private Vector3        _originalScale;
        private Sequence       _tween;

        // ══════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ══════════════════════════════════════════════════════════════════
        
        protected virtual void Awake() => EnsureInit();
        private void EnsureInit()
        {
            if (_group != null) return; // уже инициализировано
    
            _group = GetComponent<CanvasGroup>();
            _rect  = GetComponent<RectTransform>();

            _originalAnchoredPos = _rect != null ? _rect.anchoredPosition : Vector2.zero;
            _originalScale       = transform.localScale;

            ApplyHiddenInstant();
        }

        protected virtual void OnDestroy()
        {
            _tween?.Kill();
        }

        // ══════════════════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Мгновенно скрыть без анимации.</summary>
        public void HideInstant()
        {
            _tween?.Kill();
            ApplyHiddenInstant();
        }

        /// <summary>Показать элемент с анимацией из шага последовательности.</summary>
        public Sequence Show(UISequenceStep step)
            => Show(step.animationType, step.duration, step.ease, step.slideOffset);

        /// <summary>Показать элемент с явными параметрами.</summary>
        public Sequence Show(
            UIAnimationType type,
            float           duration,
            Ease            ease,
            float           slideOffset = 60f)
        {
            EnsureInit(); // ← добавить первой строкой
            _tween?.Kill();

            gameObject.SetActive(true);
            _group.blocksRaycasts = false;
            _group.interactable   = false;

            // Сброс позиции/скейла к оригинальному перед анимацией
            ResetToOriginal();

            _tween = DOTween.Sequence();
            BuildShowTween(_tween, type, duration, ease, slideOffset);
            _tween.OnComplete(() =>
            {
                _group.blocksRaycasts = true;
                _group.interactable   = true;
                OnShowComplete();
            });
            _tween.Play();

            return _tween;
        }

        /// <summary>Скрыть элемент с анимацией.</summary>
        public Sequence Hide(float duration, Ease ease, float slideOffset = 0f,
                             UIAnimationType type = UIAnimationType.Fade)
        {
            _tween?.Kill();

            _group.blocksRaycasts = false;
            _group.interactable   = false;

            _tween = DOTween.Sequence();
            BuildHideTween(_tween, type, duration, ease, slideOffset);
            _tween.OnComplete(() =>
            {
                if (_deactivateWhenHidden) gameObject.SetActive(false);
                OnHideComplete();
            });
            _tween.Play();

            return _tween;
        }

        // ══════════════════════════════════════════════════════════════════
        // Overridable hooks
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Вызывается после завершения анимации появления.</summary>
        protected virtual void OnShowComplete() { }

        /// <summary>Вызывается после завершения анимации исчезновения.</summary>
        protected virtual void OnHideComplete() { }

        // ══════════════════════════════════════════════════════════════════
        // Tween builders
        // ══════════════════════════════════════════════════════════════════

        private void BuildShowTween(Sequence seq, UIAnimationType type,
                                    float duration, Ease ease, float offset)
        {
            switch (type)
            {
                case UIAnimationType.Fade:
                    _group.alpha = 0f;
                    seq.Append(_group.DOFade(1f, duration).SetEase(ease));
                    break;

                case UIAnimationType.Scale:
                    _group.alpha          = 0f;
                    transform.localScale  = Vector3.zero;
                    seq.Append(transform.DOScale(_originalScale, duration).SetEase(ease));
                    seq.Join(_group.DOFade(1f, duration * 0.6f).SetEase(ease));
                    break;

                case UIAnimationType.SlideFromLeft:
                case UIAnimationType.SlideFromRight:
                case UIAnimationType.SlideFromBottom:
                case UIAnimationType.SlideFromTop:
                    ApplySlideStart(type, offset);
                    seq.Append(SlideToOriginal(duration, ease));
                    break;

                case UIAnimationType.FadeAndSlideFromLeft:
                case UIAnimationType.FadeAndSlideFromRight:
                case UIAnimationType.FadeAndSlideFromBottom:
                case UIAnimationType.FadeAndSlideFromTop:
                    _group.alpha = 0f;
                    ApplySlideStart(SlideOnly(type), offset);
                    seq.Append(SlideToOriginal(duration, ease));
                    seq.Join(_group.DOFade(1f, duration * 0.8f).SetEase(ease));
                    break;
            }
        }

        private void BuildHideTween(Sequence seq, UIAnimationType type,
                                    float duration, Ease ease, float offset)
        {
            // Для скрытия достаточно Fade — можно расширить при необходимости
            seq.Append(_group.DOFade(0f, duration).SetEase(ease));
        }

        // ── Slide helpers ─────────────────────────────────────────────────

        private void ApplySlideStart(UIAnimationType slideDir, float offset)
        {
            if (_rect == null) return;
            var pos = _originalAnchoredPos;
            pos += SlideVector(slideDir, offset);
            _rect.anchoredPosition = pos;
        }

        private Tweener SlideToOriginal(float duration, Ease ease)
            => _rect != null
                ? _rect.DOAnchorPos(_originalAnchoredPos, duration).SetEase(ease)
                : DOTween.To(() => 0f, _ => { }, 1f, duration); // no-op если нет rect

        private static Vector2 SlideVector(UIAnimationType dir, float offset) => dir switch
        {
            UIAnimationType.SlideFromLeft   => new Vector2(-offset, 0f),
            UIAnimationType.SlideFromRight  => new Vector2( offset, 0f),
            UIAnimationType.SlideFromBottom => new Vector2(0f, -offset),
            UIAnimationType.SlideFromTop    => new Vector2(0f,  offset),
            _                               => Vector2.zero
        };

        /// <summary>Конвертирует FadeAndSlide* → чистый Slide* для вычисления вектора.</summary>
        private static UIAnimationType SlideOnly(UIAnimationType t) => t switch
        {
            UIAnimationType.FadeAndSlideFromLeft   => UIAnimationType.SlideFromLeft,
            UIAnimationType.FadeAndSlideFromRight  => UIAnimationType.SlideFromRight,
            UIAnimationType.FadeAndSlideFromBottom => UIAnimationType.SlideFromBottom,
            UIAnimationType.FadeAndSlideFromTop    => UIAnimationType.SlideFromTop,
            _                                      => t
        };

        // ── Reset helpers ─────────────────────────────────────────────────

        private void ResetToOriginal()
        {
            if (_rect != null) _rect.anchoredPosition = _originalAnchoredPos;
            transform.localScale = _originalScale;
        }

        private void ApplyHiddenInstant()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _group.alpha          = 0f;
            _group.blocksRaycasts = false;
            _group.interactable   = false;
            if (_deactivateWhenHidden) gameObject.SetActive(false);
            ResetToOriginal();
        }
    }
}