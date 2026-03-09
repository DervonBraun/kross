using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/ShowViewStep")]
    public sealed class ShowViewStep : FlowStepAsset
    {
        [Tooltip("ViewId страницы из PageRegistry.")]
        public PageId ViewId;

        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            if (ctx.Services.TryGet<WebUIManager>(out var mgr)
                && mgr.Registry.TryGetView(ViewId, out var view))
            {
                view.ShowInstant();
            }
            else
            {
                Debug.LogWarning($"[ShowViewStep] View '{ViewId}' not found.");
            }
        }
    }
}
