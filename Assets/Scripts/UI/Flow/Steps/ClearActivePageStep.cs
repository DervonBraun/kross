using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/ClearActivePage")]
    public sealed class ClearActivePageStep : FlowStepAsset
    {
        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            if (ctx.Services.TryGet<WebUIManager>(out var mgr))
            {
                mgr.ClearActivePage();
                ctx.Phase = FlowPhase.Closed;
            }
        }
    }
}
