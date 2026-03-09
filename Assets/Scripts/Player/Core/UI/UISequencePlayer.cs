using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace Player
{
    /// <summary>
    /// Исполнитель UI-последовательности. Вешается в сцену куда угодно.
    ///
    /// ─── Как настроить ───────────────────────────────────────────────────────
    ///   1. Создай UISequenceDef (Assets → Create → AN/UI/Sequence Def).
    ///      Заполни шаги: delay, duration, ease, animationType.
    ///
    ///   2. Назначь _elements в том же порядке что и шаги в SO:
    ///        _elements[0] → steps[0]
    ///        _elements[1] → steps[1]
    ///
    ///   3. Назначь _sequenceDef.
    ///
    ///   4. Вызывай Play() / Stop() / HideInstant().
    ///
    /// ─── Фиксы vs оригинала ──────────────────────────────────────────────────
    ///   • OnDisable больше не убивает корутину автоматически (это вызывало рейс
    ///     когда родитель деактивировался во время HideInstant → объекты не скрывались).
    ///     Теперь остановка только явная: Stop() / HideInstant().
    ///   • StopSequence разделён на два: StopCoroutine() и полный Reset().
    /// </summary>
    public sealed class UISequencePlayer : MonoBehaviour
    {
        [SerializeField] private UISequenceDef _sequenceDef;

        [Tooltip("Объекты сцены в порядке шагов SO. Каждый должен иметь UISequenceElement.")]
        [SerializeField] private UISequenceElement[] _elements;

        [Header("Auto-play")]
        [Tooltip("Запустить автоматически при OnEnable.")]
        [SerializeField] private bool _playOnEnable = false;

        // ── Events ────────────────────────────────────────────────────────
        public event System.Action OnSequenceComplete;
        public event System.Action OnSequenceStopped;

        // ── Internal ──────────────────────────────────────────────────────
        private Coroutine _coroutine;
        private bool      _playing;

        // ══════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ══════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (_playOnEnable) Play();
        }

        // ВАЖНО: OnDisable намеренно НЕ останавливает корутину.
        // HideInstant() вызывается явно извне до деактивации объекта.
        // Автоостановка в OnDisable создаёт рейс: Unity убивает корутины
        // до того как HideInstant успевает скрыть элементы.

        private void OnDestroy()
        {
            // При уничтожении — просто обрываем без нотификации
            AbortCoroutine();
        }

        // ══════════════════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════════════════

        public void Play(UISequenceDef overrideDef = null)
        {
            var def = overrideDef ?? _sequenceDef;

            if (def == null)
            {
                Debug.LogError($"[UISequencePlayer] '{name}' ❌ _sequenceDef не назначен!");
                return;
            }

            if (def.steps is not { Length: > 0 })
            {
                Debug.LogError($"[UISequencePlayer] '{name}' ❌ В def нет шагов!");
                return;
            }

            if (_elements is not { Length: > 0 })
            {
                Debug.LogError($"[UISequencePlayer] '{name}' ❌ _elements пустой!");
                return;
            }

            AbortCoroutine();
            _playing   = true;
            _coroutine = StartCoroutine(PlayRoutine(def));
        }

        public void Stop()
        {
            bool wasPlaying = _playing;
            AbortCoroutine();
            if (wasPlaying) OnSequenceStopped?.Invoke();
        }

        public void Hide(UISequenceDef overrideDef = null)
        {
            AbortCoroutine();

            var def = overrideDef ?? _sequenceDef;
            if (def == null || _elements == null) return;

            _coroutine = StartCoroutine(HideRoutine(def));
        }

        public void HideInstant(UISequenceDef overrideDef = null)
        {
            AbortCoroutine();

            if (_elements == null) return;
            foreach (var el in _elements)
                el?.HideInstant();
        }

        // ══════════════════════════════════════════════════════════════════
        // Coroutines
        // ══════════════════════════════════════════════════════════════════

        private IEnumerator PlayRoutine(UISequenceDef def)
        {
            int count = Mathf.Min(def.steps.Length, _elements.Length);

            for (int i = 0; i < count; i++)
            {
                var step    = def.steps[i];
                var element = _elements[i];

                if (step == null || element == null)
                {
                    Debug.LogWarning($"[UISequencePlayer] Step[{i}] пропущен (null)");
                    continue;
                }

                if (step.delay > 0f)
                    yield return new WaitForSeconds(step.delay);

                var seq = element.Show(step);

                if (step.waitForComplete)
                    yield return seq.WaitForCompletion();
            }

            _playing   = false;
            _coroutine = null;
            OnSequenceComplete?.Invoke();
        }

        private IEnumerator HideRoutine(UISequenceDef def)
        {
            // Скрываем в обратном порядке
            for (int i = _elements.Length - 1; i >= 0; i--)
            {
                var el = _elements[i];
                if (el == null || !el.gameObject.activeSelf) continue;

                el.Hide(def.hideDuration, def.hideEase);

                if (def.hideStagger > 0f)
                    yield return new WaitForSeconds(def.hideStagger);
            }

            yield return new WaitForSeconds(def.hideDuration);
            _coroutine = null;
        }

        // ══════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Останавливает корутину без нотификации и без HideInstant.</summary>
        private void AbortCoroutine()
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
                _coroutine = null;
            }
            _playing = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_sequenceDef == null || _elements == null) return;

            if (_sequenceDef.steps.Length != _elements.Length)
                Debug.LogWarning(
                    $"[UISequencePlayer] '{name}': steps ({_sequenceDef.steps.Length}) " +
                    $"≠ elements ({_elements.Length}). Используется минимум из двух.", this);
        }
#endif
    }
}