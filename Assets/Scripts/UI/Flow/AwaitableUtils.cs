using System;
using System.Collections;
using System.Threading;
using DG.Tweening;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Утилиты для работы с Awaitable в Unity 6.
    ///
    /// Решает две задачи:
    ///   1. Coroutine → Awaitable bridge (RunCoroutineAsync)
    ///   2. DOTween Tween → Awaitable (AwaitTween)
    ///
    /// Использование:
    ///   await AwaitableUtils.RunCoroutineAsync(this, MyRoutine(), ct, onCancel: SnapToEnd);
    ///   await AwaitableUtils.AwaitTween(transform.DOMove(...), ct, snapOnCancel: true);
    /// </summary>
    public static class AwaitableUtils
    {
        // ── Coroutine → Awaitable ─────────────────────────────────────────────

        /// <summary>
        /// Запускает корутину на <paramref name="owner"/> и возвращает Awaitable,
        /// который завершается вместе с ней.
        ///
        /// При отмене токена:
        ///   - корутина останавливается
        ///   - вызывается <paramref name="onCancel"/> (если задан)
        ///   - метод возвращается немедленно
        /// </summary>
        public static async Awaitable RunCoroutineAsync(
            MonoBehaviour  owner,
            IEnumerator    routine,
            CancellationToken ct,
            Action         onCancel = null)
        {
            bool done = false;

            IEnumerator Wrapper()
            {
                yield return routine;
                done = true;
            }

            var coroutine = owner.StartCoroutine(Wrapper());

            while (!done)
            {
                if (ct.IsCancellationRequested)
                {
                    owner.StopCoroutine(coroutine);
                    onCancel?.Invoke();
                    return;
                }
                await Awaitable.NextFrameAsync();
            }
        }

        // ── DOTween → Awaitable ───────────────────────────────────────────────

        /// <summary>
        /// Ждёт завершения DOTween-твина.
        /// При отмене токена убивает твин.
        /// Если <paramref name="snapOnCancel"/> == true — прыгает к конечному значению (Complete).
        /// </summary>
        public static async Awaitable AwaitTween(
            Tween             tween,
            CancellationToken ct,
            bool              snapOnCancel = true)
        {
            if (tween == null) return;

            while (tween.IsActive() && !tween.IsComplete())
            {
                if (ct.IsCancellationRequested)
                {
                    if (snapOnCancel)
                        tween.Complete();
                    else
                        tween.Kill();
                    return;
                }
                await Awaitable.NextFrameAsync();
            }
        }

        /// <summary>
        /// Ждёт завершения DOTween Sequence.
        /// </summary>
        public static async Awaitable AwaitSequence(
            Sequence          seq,
            CancellationToken ct,
            bool              snapOnCancel = true)
        {
            if (seq == null) return;

            while (seq.IsActive() && !seq.IsComplete())
            {
                if (ct.IsCancellationRequested)
                {
                    if (snapOnCancel)
                        seq.Complete();
                    else
                        seq.Kill();
                    return;
                }
                await Awaitable.NextFrameAsync();
            }
        }
    }
}