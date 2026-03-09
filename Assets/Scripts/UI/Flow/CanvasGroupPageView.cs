using System;
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;
using UI.Flow.Steps;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Универсальная реализация IPageView + IAnimatableView на базе CanvasGroup.
    /// Поддерживает именованные DOTween-анимации через словарь.
    ///
    /// ShowInstant / HideInstant — мгновенное переключение alpha + interactable.
    /// PlayAnimationAsync — запускает зарегистрированную анимацию и ждёт её.
    ///
    /// Использование:
    ///   1. Добавь компонент на root-объект окна.
    ///   2. Укажи PageId в Inspector.
    ///   3. Зарегистрируй анимации через RegisterAnimation() в Awake конкретного окна.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class CanvasGroupPageView : PageViewBase, IAnimatableView
    {
        [Header("CanvasGroup")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [SerializeField, Range(0.05f, 1f)] private float _showFadeDuration = 0.2f;
        [SerializeField, Range(0.05f, 1f)] private float _hideFadeDuration = 0.15f;

        private readonly Dictionary<string, Func<CancellationToken, Awaitable>> _animations = new();

        protected override void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            base.Awake(); // регистрация + HideInstant

            // Регистрируем дефолтные анимации
            RegisterAnimation("fade_in",  ct => FadeAsync(1f, _showFadeDuration, ct));
            RegisterAnimation("fade_out", ct => FadeAsync(0f, _hideFadeDuration, ct));
        }

        // ─── IPageView ────────────────────────────────────────────────────────

        public override void ShowInstant()
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha          = 1f;
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;
        }

        public override void HideInstant()
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        // ─── IAnimatableView ──────────────────────────────────────────────────

        public async Awaitable PlayAnimationAsync(string key, CancellationToken ct)
        {
            if (_animations.TryGetValue(key, out var factory))
            {
                await factory(ct);
            }
            else
            {
                Debug.LogWarning($"[CanvasGroupPageView] Animation '{key}' not registered on '{name}'.");
            }
        }

        // ─── Registration ─────────────────────────────────────────────────────

        /// <summary>
        /// Регистрирует именованную анимацию.
        /// factory получает CancellationToken и возвращает Awaitable до завершения анимации.
        /// </summary>
        public void RegisterAnimation(string key, Func<CancellationToken, Awaitable> factory)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _animations[key] = factory;
        }

        // ─── Built-in helpers ─────────────────────────────────────────────────

        private async Awaitable FadeAsync(float targetAlpha, float duration, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            gameObject.SetActive(true);

            await AwaitableUtils.AwaitTween(
                _canvasGroup.DOFade(targetAlpha, duration), ct, snapOnCancel: true);

            if (targetAlpha <= 0f)
            {
                _canvasGroup.interactable   = false;
                _canvasGroup.blocksRaycasts = false;
                gameObject.SetActive(false);
            }
            else
            {
                _canvasGroup.interactable   = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }
    }
}