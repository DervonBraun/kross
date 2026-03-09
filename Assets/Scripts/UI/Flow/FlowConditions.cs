using System;
using UnityEngine;

namespace UI.Flow
{
    // ─── FirstOpenCondition ───────────────────────────────────────────────────

    /// <summary>
    /// Истинно, если страница открывается ВПЕРВЫЕ (history.HasEverOpened == false).
    ///
    /// Пример: показать GreetingWindow только при первом открытии инвентаря.
    /// </summary>
    [Serializable]
    public sealed class FirstOpenCondition : IFlowCondition
    {
        [Tooltip("Проверяется указанная страница. Если пусто — берётся pageId текущего request.")]
        public PageId TargetPageId;

        public bool Evaluate(PageContext ctx)
        {
            var id = TargetPageId.IsEmpty ? ctx.Request.PageId : TargetPageId;
            return !ctx.History.HasEverOpened(id);
        }
    }

    // ─── HasPayloadFlagCondition ──────────────────────────────────────────────

    /// <summary>
    /// Истинно, если payload содержит указанный флаг.
    /// Работает с любым payload, реализующим IFlagProvider,
    /// или с PageOpenFlags напрямую.
    /// </summary>
    [Serializable]
    public sealed class HasPayloadFlagCondition : IFlowCondition
    {
        public PageOpenFlags RequiredFlag;

        public bool Evaluate(PageContext ctx) =>
            (ctx.Request.Flags & RequiredFlag) != 0;
    }

    // ─── GameStateCondition ───────────────────────────────────────────────────

    /// <summary>
    /// Произвольное условие через GameState + SO-делегат.
    /// Наследуйте GameStateConditionBase для конкретных проверок.
    ///
    /// Пример: проверить OktsStage > 2, наличие эффекта, доступный уровень и т.д.
    /// </summary>
    [Serializable]
    public sealed class GameStateCondition : IFlowCondition
    {
        [Tooltip("SO-делегат с конкретной проверкой GameState.")]
        public GameStateConditionAsset Evaluator;

        public bool Evaluate(PageContext ctx)
        {
            if (Evaluator == null)
            {
                Debug.LogWarning("[GameStateCondition] Evaluator not set — returning true.");
                return true;
            }

            return Evaluator.Evaluate(ctx);
        }
    }

    /// <summary>
    /// Базовый SO-ассет для проверок GameState.
    /// Создавайте конкретные подклассы под нужные условия.
    /// </summary>
    public abstract class GameStateConditionAsset : ScriptableObject
    {
        public abstract bool Evaluate(PageContext ctx);
    }

    // ─── NotCondition ─────────────────────────────────────────────────────────

    /// <summary>
    /// Инвертирует любое другое условие.
    ///
    /// Пример: Not(FirstOpenCondition) = только при повторных открытиях.
    /// </summary>
    [Serializable]
    public sealed class NotCondition : IFlowCondition
    {
        [SerializeReference] public IFlowCondition Inner;

        public bool Evaluate(PageContext ctx)
        {
            if (Inner == null)
            {
                Debug.LogWarning("[NotCondition] Inner condition is null — returning false.");
                return false;
            }

            return !Inner.Evaluate(ctx);
        }
    }

    // ─── AndCondition ─────────────────────────────────────────────────────────

    /// <summary>
    /// Все вложенные условия должны быть истинны.
    /// </summary>
    [Serializable]
    public sealed class AndCondition : IFlowCondition
    {
        [SerializeReference] public IFlowCondition[] Conditions;

        public bool Evaluate(PageContext ctx)
        {
            if (Conditions == null || Conditions.Length == 0) return true;
            foreach (var c in Conditions)
            {
                if (c != null && !c.Evaluate(ctx)) return false;
            }
            return true;
        }
    }

    // ─── OrCondition ──────────────────────────────────────────────────────────

    /// <summary>
    /// Хотя бы одно вложенное условие должно быть истинным.
    /// </summary>
    [Serializable]
    public sealed class OrCondition : IFlowCondition
    {
        [SerializeReference] public IFlowCondition[] Conditions;

        public bool Evaluate(PageContext ctx)
        {
            if (Conditions == null || Conditions.Length == 0) return false;
            foreach (var c in Conditions)
            {
                if (c != null && c.Evaluate(ctx)) return true;
            }
            return false;
        }
    }
}
