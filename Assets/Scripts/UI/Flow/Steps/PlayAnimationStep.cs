using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/PlayAnimationStep")]
    public sealed class PlayAnimationStep : FlowStepAsset
    {
        public PageId ViewId;

        [Tooltip("Ключ анимации. Конкретная строка зависит от реализации IAnimatableView.")]
        public string AnimationKey;

        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            if (!ctx.Services.TryGet<WebUIManager>(out var mgr)
                || !mgr.Registry.TryGetView(ViewId, out var view))
            {
                Debug.LogWarning($"[PlayAnimationStep] View '{ViewId}' not found — skipping '{AnimationKey}'.");
                return;
            }

            if (view is IAnimatableView animatable)
                await animatable.PlayAnimationAsync(AnimationKey, ct);
            else
                Debug.LogWarning($"[PlayAnimationStep] View '{ViewId}' doesn't implement IAnimatableView.");
        }
    }
}
