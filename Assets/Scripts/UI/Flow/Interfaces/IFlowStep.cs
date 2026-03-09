using System.Threading;
using UnityEngine;

namespace UI.Flow
{
    // ─── Шаг сценария ─────────────────────────────────────────────────────────

    /// <summary>
    /// Атомарная единица выполнения flow. Шаги иммутабельны — никаких runtime-полей.
    /// </summary>
    public interface IFlowStep
    {
        Awaitable ExecuteAsync(PageContext ctx, CancellationToken ct);
    }

    // ─── Базовый класс-ассет ──────────────────────────────────────────────────

    /// <summary>
    /// Базовый ScriptableObject для всех шагов. Содержит опциональный предикат.
    /// Конкретные шаги наследуют этот класс и реализуют ExecuteCoreAsync.
    ///
    /// ПРАВИЛО: только конфиг-поля (Inspector). Никаких runtime-полей.
    /// </summary>
    public abstract class FlowStepAsset : ScriptableObject, IFlowStep
    {
        [Tooltip("Шаг выполняется только если Condition истинен. Null = всегда выполнять.")]
        [SerializeReference] public IFlowCondition Condition;

        public async Awaitable ExecuteAsync(PageContext ctx, CancellationToken ct)
        {
            if (Condition != null && !Condition.Evaluate(ctx))
                return;

            await ExecuteCoreAsync(ctx, ct);
        }

        protected abstract Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct);
    }

    // ─── Условие ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Предикат, используемый FlowStepAsset и ConditionalStep.
    /// Реализации должны быть [Serializable] для SerializeReference.
    /// </summary>
    public interface IFlowCondition
    {
        bool Evaluate(PageContext ctx);
    }
}
