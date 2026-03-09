using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace AN_
{
    public sealed class ANChatAttachmentView : MonoBehaviour
    {
        [SerializeField] private Image _image;              // thumbnail image
        [SerializeField] private GameObject _loadingVisual; // например серый фон/иконка "loading"
        [SerializeField] private CanvasGroup _cg;
        [SerializeField] private RectTransform _rect;
        [SerializeField] private Button _button;

        private Action _onClick;

        private void Awake()
        {
            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            if (_rect == null) _rect = transform as RectTransform;
            if (_button == null) _button = GetComponent<Button>();

            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
                _button.onClick.AddListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            _onClick?.Invoke();
        }

        public void SetOnClick(Action onClick)
        {
            _onClick = onClick;
        }

        public void SetInteractable(bool on)
        {
            if (_button != null) _button.interactable = on;
        }

        public void SetAsLoadingVisual(bool loading)
        {
            if (_loadingVisual != null) _loadingVisual.SetActive(loading);
            if (_image != null) _image.enabled = !loading; // пока грузится, картинку скрываем
        }

        public void SetSprite(Sprite sprite)
        {
            if (_image == null) return;
            _image.sprite = sprite;
        }

        public void ShowInstant()
        {
            if (_cg != null) _cg.alpha = 1f;
            if (_rect != null) _rect.localScale = Vector3.one;
        }

        public void PlayPop(float time, Ease ease)
        {
            if (_cg != null) _cg.alpha = 1f;
            if (_rect == null || time <= 0f) return;

            _rect.localScale = Vector3.one * 0.88f;
            _rect.DOScale(1f, time).SetEase(ease).SetUpdate(true);
        }
    }
}
