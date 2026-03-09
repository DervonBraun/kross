using System;
using UnityEngine;

namespace UI.Flow.Inventory
{
    /// <summary>
    /// Истинно если страница открывается "холодно" —
    /// прошло больше ThresholdSeconds с последнего закрытия.
    ///
    /// PageId берётся из текущего request'а автоматически.
    /// ThresholdSeconds настраивается в Inspector через [SerializeReference].
    /// </summary>
    [Serializable]
    public sealed class InventoryIdleCondition : IFlowCondition
    {
        [Tooltip("Секунд бездействия до считается 'холодным' открытием.")]
        public float ThresholdSeconds = 30f;

        public bool Evaluate(PageContext ctx)
        {
            if (ctx.Services.TryGet<IdleTimerService>(out var timer))
                return timer.IsIdle(ctx.Request.PageId, ThresholdSeconds);

            Debug.LogWarning("[InventoryIdleCondition] IdleTimerService not found — defaulting to idle=true.");
            return true;
        }
    }

    /// <summary>
    /// Инверсия: истинно при "тёплом" открытии.
    /// </summary>
    [Serializable]
    public sealed class InventoryRecentCondition : IFlowCondition
    {
        [Tooltip("Секунд бездействия до считается 'холодным' открытием.")]
        public float ThresholdSeconds = 30f;

        public bool Evaluate(PageContext ctx)
        {
            if (ctx.Services.TryGet<IdleTimerService>(out var timer))
                return !timer.IsIdle(ctx.Request.PageId, ThresholdSeconds);

            return false;
        }
    }
}