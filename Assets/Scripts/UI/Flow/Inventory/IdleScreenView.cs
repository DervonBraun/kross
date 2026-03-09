using System.Collections;
using System.Threading;
using DG.Tweening;
using TMPro;
using UnityEngine;
using AN_.UI.Flow;
using UI.Flow;
using UI.Flow.Steps;

namespace AN_.UI.Flow.Inventory
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class IdleScreenView : MonoBehaviour, IPageView, IAnimatableView
    {
        [Header("Flow")]
        [SerializeField] private PageId _viewId = new("InventoryIdle");

        [Header("Refs")]
        [SerializeField] private CanvasGroup   _canvasGroup;
        [SerializeField] private TMP_Text      _greetingLabel;
        [SerializeField] private RectTransform _avatarContainer;

        [Header("Greeting")]
        [SerializeField] private string _greetingText = "Добро пожаловать";

        [Header("Timings")]
        [SerializeField, Min(0f)] private float _greetingFadeInDur = 0.4f;
        [SerializeField, Min(0f)] private float _avatarSlideDur    = 0.5f;
        [SerializeField, Min(0f)] private float _avatarSlideOffset = 80f;
        [SerializeField, Min(0f)] private float _holdDur           = 1.2f;
        [SerializeField, Min(0f)] private float _fadeOutDur        = 0.3f;

        [Header("Easing")]
        [SerializeField] private Ease _slideEase   = Ease.OutCubic;
        [SerializeField] private Ease _fadeInEase  = Ease.OutQuad;
        [SerializeField] private Ease _fadeOutEase = Ease.InQuad;

        public PageId ViewId => _viewId;

        // ── IPageView ─────────────────────────────────────────────────────────

        public void ShowInstant()
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;

            if (_greetingLabel != null)
            {
                _greetingLabel.text  = _greetingText;
                _greetingLabel.alpha = 0f;
            }

            if (_avatarContainer != null)
            {
                var pos = _avatarContainer.anchoredPosition;
                pos.y = -_avatarSlideOffset;
                _avatarContainer.anchoredPosition = pos;

                var g = _avatarContainer.GetComponent<CanvasGroup>();
                if (g != null) g.alpha = 0f;
            }
        }

        public void HideInstant()
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        // ── IAnimatableView ───────────────────────────────────────────────────

        public async Awaitable PlayAnimationAsync(string key, CancellationToken ct)
        {
            switch (key)
            {
                case "idle_in":
                    await AwaitableUtils.RunCoroutineAsync(this, IdleInRoutine(ct), ct,
                        onCancel: SnapToVisible);
                    break;
                case "idle_out":
                    await AwaitableUtils.RunCoroutineAsync(this, IdleOutRoutine(ct), ct,
                        onCancel: () => _canvasGroup.alpha = 0f);
                    break;
                default:
                    Debug.LogWarning($"[IdleScreenView] Unknown animation key: '{key}'");
                    break;
            }
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator IdleInRoutine(CancellationToken ct)
        {
            _canvasGroup.alpha = 1f;

            // Надпись fade-in — обычный yield return, мы внутри IEnumerator
            if (_greetingLabel != null)
            {
                var tween = _greetingLabel.DOFade(1f, _greetingFadeInDur).SetEase(_fadeInEase);
                yield return tween.WaitForCompletion();
            }

            if (ct.IsCancellationRequested) yield break;

            // Аватарка выезжает из-за надписи
            if (_avatarContainer != null)
            {
                var avatarCg = _avatarContainer.GetComponent<CanvasGroup>();
                var seq = DOTween.Sequence();
                seq.Append(_avatarContainer.DOAnchorPosY(0f, _avatarSlideDur).SetEase(_slideEase));
                if (avatarCg != null)
                    seq.Join(avatarCg.DOFade(1f, _avatarSlideDur * 0.6f).SetEase(_fadeInEase));

                yield return seq.WaitForCompletion();
            }

            if (!ct.IsCancellationRequested)
                yield return new WaitForSeconds(_holdDur);
        }

        private IEnumerator IdleOutRoutine(CancellationToken ct)
        {
            yield return _canvasGroup
                .DOFade(0f, _fadeOutDur)
                .SetEase(_fadeOutEase)
                .WaitForCompletion();

            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void SnapToVisible()
        {
            DOTween.Kill(transform, complete: true);
            _canvasGroup.alpha          = 1f;
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;
        }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            HideInstant(); // WebUIManager.Start зарегистрирует нас через ScanAndRegisterViews
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }
#endif
    }
}