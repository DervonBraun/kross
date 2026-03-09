using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/WaitStep")]
    public sealed class WaitStep : FlowStepAsset
    {
        [Min(0f)] public float Seconds = 0.5f;

        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            await Awaitable.WaitForSecondsAsync(Seconds, ct);
        }
    }
}
