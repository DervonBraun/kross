using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/HideViewStep")]
    public sealed class HideViewStep : FlowStepAsset
    {
        public PageId ViewId;

        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            if (ctx.Services.TryGet<WebUIManager>(out var mgr)
                && mgr.Registry.TryGetView(ViewId, out var view))
            {
                view.HideInstant();
            }
            else
            {
                Debug.LogWarning($"[HideViewStep] View '{ViewId}' not found.");
            }
        }
    }
}
