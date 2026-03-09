using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AN_
{
    public sealed class ANChatImageLightbox : MonoBehaviour
    {
        [SerializeField] private GameObject _root;     // весь оверлей
        [SerializeField] private CanvasGroup _cg;      // fade
        [SerializeField] private RectTransform _panel; // контейнер для scale
        [SerializeField] private Image _image;         // большая картинка
        [SerializeField] private Button _closeButton;  // обычно фон-кнопка на весь экран

        [Header("Anim")]
        [SerializeField, Min(0f)] private float _fadeTime = 0.12f;
        [SerializeField, Min(0f)] private float _scaleTime = 0.18f;
        [SerializeField] private Ease _ease = Ease.OutCubic;

        private Tween _tween;

        private void Awake()
        {
            if (_root != null) _root.SetActive(false);

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Hide);
                _closeButton.onClick.AddListener(Hide);
            }
        }

        public void Show(Sprite sprite)
        {
            Debug.Log("Show");
            if (sprite == null) return;

            if (_root != null) _root.SetActive(true);
            if (_image != null) _image.sprite = sprite;

            _tween?.Kill();

            if (_cg != null) _cg.alpha = 0f;
            if (_panel != null) _panel.localScale = Vector3.one * 0.92f;

            _tween = DOTween.Sequence()
                .Join(_cg != null ? _cg.DOFade(1f, _fadeTime).SetUpdate(true) : null)
                .Join(_panel != null ? _panel.DOScale(1f, _scaleTime).SetEase(_ease).SetUpdate(true) : null);
        }

        public void Hide()
        {
            _tween?.Kill();

            if (_root == null)
                return;

            if (_cg == null)
            {
                _root.SetActive(false);
                return;
            }

            _tween = DOTween.Sequence()
                .Join(_cg.DOFade(0f, _fadeTime).SetUpdate(true))
                .OnComplete(() => _root.SetActive(false));
        }
    }
}
