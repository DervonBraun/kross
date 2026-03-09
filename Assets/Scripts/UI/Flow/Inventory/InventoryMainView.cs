using System.Threading;
using DG.Tweening;
using UI.Flow.Steps;
using UnityEngine;

namespace UI.Flow.Inventory
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class InventoryMainView : MonoBehaviour, IPageView, IAnimatableView
    {
        [Header("Flow")]
        [SerializeField] private PageId _viewId = new("InventoryMain");

        [Header("Refs")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Animation")]
        [SerializeField, Min(0.01f)] private float _appearDur    = 0.25f;
        [SerializeField, Min(0.01f)] private float _disappearDur = 0.20f;
        [SerializeField]             private Ease  _appearEase    = Ease.OutQuad;
        [SerializeField]             private Ease  _disappearEase = Ease.InQuad;

        public PageId ViewId => _viewId;

        // ── IPageView ─────────────────────────────────────────────────────────

        public void ShowInstant()
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
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
                case "appear":
                    gameObject.SetActive(true);
                    await AwaitableUtils.AwaitTween(
                        _canvasGroup.DOFade(1f, _appearDur).SetEase(_appearEase), ct);
                    if (!ct.IsCancellationRequested)
                    {
                        _canvasGroup.interactable   = true;
                        _canvasGroup.blocksRaycasts = true;
                    }
                    break;

                case "disappear":
                    await AwaitableUtils.AwaitTween(
                        _canvasGroup.DOFade(0f, _disappearDur).SetEase(_disappearEase), ct);
                    _canvasGroup.interactable   = false;
                    _canvasGroup.blocksRaycasts = false;
                    gameObject.SetActive(false);
                    break;

                default:
                    Debug.LogWarning($"[InventoryMainView] Unknown animation key: '{key}'");
                    break;
            }
        }

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            HideInstant();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        }
#endif
    }
}