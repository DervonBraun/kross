using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/SetActivePage")]
    public sealed class SetActivePageStep : FlowStepAsset
    {
        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            if (ctx.Services.TryGet<WebUIManager>(out var mgr))
            {
                mgr.SetActivePage(ctx.Request.PageId);
                ctx.Phase = FlowPhase.Open;
            }
            else
            {
                Debug.LogWarning("[SetActivePageStep] WebUIManager not found in ServiceLocator.");
            }
        }
    }
}