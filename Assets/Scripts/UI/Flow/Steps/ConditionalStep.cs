using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "AN_/UI/Steps/Conditional")]
    public sealed class ConditionalStep : FlowStepAsset
    {
        [Tooltip("Условие. Если null — считается истиной.")]
        [SerializeReference] public IFlowCondition When;

        [Tooltip("Шаги если условие истинно. Перетащи SO-ассеты шагов.")]
        public FlowStepAsset[] Then;

        [Tooltip("Шаги если условие ложно. Перетащи SO-ассеты шагов.")]
        public FlowStepAsset[] Else;

        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            bool condition = When == null || When.Evaluate(ctx);
            var  steps     = condition ? Then : Else;

            if (steps == null) return;

            foreach (var step in steps)
            {
                if (step == null) continue;
                ct.ThrowIfCancellationRequested();
                await step.ExecuteAsync(ctx, ct);
            }
        }
    }
}