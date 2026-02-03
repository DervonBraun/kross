using UnityEngine;
using DG.Tweening;

namespace Player
{
    public class CrosshairUIController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private InteractionController _interaction;
        [SerializeField] private CanvasGroup _group;

        [Header("Lines")]
        [SerializeField] private RectTransform _hLine;
        [SerializeField] private RectTransform _vLine;

        [Header("Angles")]
        [SerializeField] private float _idleH = 0f;
        [SerializeField] private float _idleV = 90f;
        [SerializeField] private float _activeA = 45f;
        [SerializeField] private float _activeB = -45f;

        [Header("Tween")]
        [SerializeField] private float _rotateDuration = 0.12f;
        [SerializeField] private Ease _rotateEase = Ease.OutCubic;
        [SerializeField] private float _fadeDuration = 0.10f;

        private bool _visible;

        private void Awake()
        {
            if (_interaction == null) return;

            _interaction.CameraStateChanged += OnCameraStateChanged;
            _interaction.DefaultInteractableChanged += OnDefaultInteractableChanged;

            // старт
            SetIdleImmediate();
            OnCameraStateChanged(_interaction.GetComponent<PlayerContext>().CameraState);
            OnDefaultInteractableChanged(_interaction.HasDefaultInteractable);
        }

        private void OnDestroy()
        {
            if (_interaction == null) return;

            _interaction.CameraStateChanged -= OnCameraStateChanged;
            _interaction.DefaultInteractableChanged -= OnDefaultInteractableChanged;
        }

        private void OnCameraStateChanged(CameraState state)
        {
            if (state == CameraState.Aim) Hide();
            else Show();
        }

        private void OnDefaultInteractableChanged(bool canInteract)
        {
            if (!_visible) return;

            if (canInteract) AnimateToActive();
            else AnimateToIdle();
        }

        private void AnimateToActive()
        {
            KillTweens();
            _hLine.DORotate(new Vector3(0, 0, _activeA), _rotateDuration).SetEase(_rotateEase).SetUpdate(true);
            _vLine.DORotate(new Vector3(0, 0, _activeB), _rotateDuration).SetEase(_rotateEase).SetUpdate(true);
        }

        private void AnimateToIdle()
        {
            KillTweens();
            _hLine.DORotate(new Vector3(0, 0, _idleH), _rotateDuration).SetEase(_rotateEase).SetUpdate(true);
            _vLine.DORotate(new Vector3(0, 0, _idleV), _rotateDuration).SetEase(_rotateEase).SetUpdate(true);
        }

        private void SetIdleImmediate()
        {
            if (_hLine) _hLine.localEulerAngles = new Vector3(0, 0, _idleH);
            if (_vLine) _vLine.localEulerAngles = new Vector3(0, 0, _idleV);
        }

        private void Show()
        {
            if (_visible) return;
            _visible = true;

            if (_group)
            {
                _group.DOKill();
                _group.gameObject.SetActive(true);
                _group.alpha = 0f;
                _group.DOFade(1f, _fadeDuration).SetUpdate(true);
            }
            else
            {
                gameObject.SetActive(true);
            }

            // привести к актуальному состоянию сразу
            OnDefaultInteractableChanged(_interaction.HasDefaultInteractable);
        }

        private void Hide()
        {
            if (!_visible) return;
            _visible = false;

            KillTweens();

            if (_group)
            {
                _group.DOKill();
                _group.DOFade(0f, _fadeDuration).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (_group) _group.gameObject.SetActive(false);
                    });
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void KillTweens()
        {
            if (_hLine) _hLine.DOKill();
            if (_vLine) _vLine.DOKill();
        }
    }
}
