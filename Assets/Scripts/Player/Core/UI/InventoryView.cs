using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Player
{
    public sealed class InventoryView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerContext _ctx;

        [Header("UI Root (window)")]
        [SerializeField] private RectTransform _windowRoot; // панель окна инвентаря
        [SerializeField] private CanvasGroup _cg;           // CanvasGroup на окне
        [SerializeField] private GameObject _firstSelected;

        [Header("Anim")]
        [SerializeField, Min(0.01f)] private float _openDur = 0.22f;
        [SerializeField, Min(0.01f)] private float _closeDur = 0.18f;
        [SerializeField] private Ease _openEase = Ease.OutCubic;
        [SerializeField] private Ease _closeEase = Ease.InCubic;
        [SerializeField, Range(1f, 1.12f)] private float _overshoot = 1.04f;

        private Tween _t;
        private bool _open;

        private void Awake()
        {
            if (_ctx == null) _ctx = FindFirstObjectByType<PlayerContext>();
            if (_windowRoot == null) _windowRoot = transform as RectTransform;
            if (_cg == null) _cg = GetComponent<CanvasGroup>();

            if (_windowRoot != null)
                _windowRoot.pivot = new Vector2(0.5f, 0.5f);

            // стартовое состояние: закрыто
            ApplyInstant(false);
        }

        private void OnEnable()
        {
            if (_ctx == null) return;
            _ctx.ModeChanged += OnModeChanged;
            OnModeChanged(_ctx.Mode);
        }

        private void OnDisable()
        {
            if (_ctx != null) _ctx.ModeChanged -= OnModeChanged;
        }

        private void OnModeChanged(PlayerMode mode)
        {
            SetOpen(mode == PlayerMode.UiInventory);
        }

        public void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;

            _t?.Kill();

            if (open) PlayOpen();
            else PlayClose();
        }

        private void PlayOpen()
        {
            if (_windowRoot == null) return;

            if (_cg != null)
            {
                _cg.blocksRaycasts = true;
                _cg.interactable = true;
                _cg.alpha = 0f;
            }

            _windowRoot.localScale = Vector3.one * 0.001f;

            var seq = DOTween.Sequence();
            seq.Append(_windowRoot.DOScale(_overshoot, _openDur * 0.78f).SetEase(_openEase));
            seq.Append(_windowRoot.DOScale(1f, _openDur * 0.22f).SetEase(Ease.OutQuad));

            if (_cg != null)
                seq.Join(_cg.DOFade(1f, _openDur).SetEase(Ease.OutQuad));

            _t = seq;

            var es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(null);
                if (_firstSelected != null) es.SetSelectedGameObject(_firstSelected);
            }
        }

        private void PlayClose()
        {
            if (_windowRoot == null) return;

            if (_cg != null)
            {
                _cg.blocksRaycasts = false;
                _cg.interactable = false;
            }

            var seq = DOTween.Sequence();
            seq.Append(_windowRoot.DOScale(0.001f, _closeDur).SetEase(_closeEase));

            if (_cg != null)
                seq.Join(_cg.DOFade(0f, _closeDur * 0.9f).SetEase(Ease.InQuad));

            seq.OnComplete(() =>
            {
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null)
                    es.SetSelectedGameObject(null);
            });

            _t = seq;
        }

        private void ApplyInstant(bool open)
        {
            if (_windowRoot != null)
                _windowRoot.localScale = open ? Vector3.one : Vector3.one * 0.001f;

            if (_cg != null)
            {
                _cg.alpha = open ? 1f : 0f;
                _cg.blocksRaycasts = open;
                _cg.interactable = open;
            }
        }
    }
}
