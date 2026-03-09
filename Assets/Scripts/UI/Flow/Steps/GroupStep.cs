using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/Group")]
    public sealed class GroupStep : FlowStepAsset
    {
        [Tooltip("Шаги для группировки. Перетащи SO-ассеты.")]
        public FlowStepAsset[] Steps;

        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            if (Steps == null) return;

            foreach (var step in Steps)
            {
                if (step == null) continue;
                ct.ThrowIfCancellationRequested();
                await step.ExecuteAsync(ctx, ct);
            }
        }
    }
}