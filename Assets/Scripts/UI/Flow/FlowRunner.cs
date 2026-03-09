using System;
using System.Threading;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Исполняет PageFlowDefinition шаг за шагом.
    /// Основан на Unity 6 native async/Awaitable — без UniTask.
    ///
    /// Жизненный цикл отмены:
    ///   - CancellationTokenSource принадлежит FlowRunner, создаётся на старте сессии.
    ///   - Cancel() прерывает текущий await-шаг по токену.
    ///   - finally-блок ВСЕГДА выполняется: снимает lock, очищает transient-state.
    ///   - Rollback шагов не предусмотрен в MVP — каждый шаг атомарен.
    /// </summary>
    public sealed class FlowRunner
    {
        private CancellationTokenSource _cts;

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        // Вызывается WebUIManager'ом после завершения (успех или отмена)
        public event Action<FlowRunner> Completed;

        /// <summary>
        /// Запускает flow асинхронно. Не блокирует вызывающий поток.
        /// WebUIManager должен await этого метода (или подписаться на Completed).
        /// </summary>
        public async Awaitable RunAsync(PageFlowDefinition flow, PageContext ctx)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (ctx  == null) throw new ArgumentNullException(nameof(ctx));

            _cts = new CancellationTokenSource();

            // Объединяем с внешним токеном из PageContext (если он уже отменён — сразу выйдем)
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ctx.Token);

            try
            {
                var steps = flow.StepsForPhase(ctx.Phase);
                if (steps != null)
                {
                    foreach (var step in steps)
                    {
                        if (step == null) continue;
                        linked.Token.ThrowIfCancellationRequested();
                        await step.ExecuteAsync(ctx, linked.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Нормальная отмена — не логируем как ошибку
                Debug.Log($"[FlowRunner] Flow cancelled for page '{ctx.Request.PageId}'");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FlowRunner] Unhandled exception in flow '{ctx.Request.PageId}': {ex}");
            }
            finally
            {
                // ВСЕГДА: снять input lock, очистить transient state
                _cts.Dispose();
                _cts = null;
                Completed?.Invoke(this);
            }
        }

        /// <summary>Прерывает текущий выполняющийся flow.</summary>
        public void Cancel() => _cts?.Cancel();
    }
}