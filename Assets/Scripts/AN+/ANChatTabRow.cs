using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AN_
{
    public sealed class ANChatTabRow : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;

        [Header("Anim Parts")]
        [SerializeField] private RectTransform _animRoot; // child
        [SerializeField] private CanvasGroup _cg;         // на animRoot
        [SerializeField] private LayoutElement _layout;   // на root

        [Header("Sizes")]
        [SerializeField, Min(0f)] private float _targetHeight = 34f;

        private string _id;
        private Tween _appearTween;

        public string Id => _id;

        public void Bind(string id, string title, Action onClick)
        {
            _id = id;
            SetLabel(title);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        public void SetLabel(string title)
        {
            if (_label != null)
                _label.SetText(title ?? "");
        }

        public void PlayAppear(RectTransform contentToRebuild, float time, Ease ease)
        {
            if (_animRoot == null) _animRoot = (RectTransform)transform;
            if (_cg == null) _cg = _animRoot.GetComponent<CanvasGroup>();
            if (_layout == null) _layout = GetComponent<LayoutElement>();

            _appearTween?.Kill();

            // Фиксируем высоту СРАЗУ, чтобы layout не прыгал вообще
            if (_layout != null)
            {
                _layout.minHeight = _targetHeight;
                _layout.preferredHeight = _targetHeight;
                _layout.flexibleHeight = 0f;
            }

            // Ставим старт визуала
            if (_cg != null) _cg.alpha = 0f;

            _animRoot.localScale = new Vector3(1f, 0.92f, 1f);
            var startPos = _animRoot.anchoredPosition;
            _animRoot.anchoredPosition = startPos + new Vector2(0f, -6f); // лёгкий "въезд"

            // Один rebuild в начале, чтобы layout закрепился (опционально)
            if (contentToRebuild != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentToRebuild);

            // Анимация без изменения layout
            _appearTween = DOTween.Sequence()
                .Join(_animRoot.DOAnchorPos(startPos, time).SetEase(ease))
                .Join(_animRoot.DOScaleY(1f, time).SetEase(ease))
                .Join((_cg != null ? _cg.DOFade(1f, time) : null))
                .SetUpdate(true);
        }

    }
}
