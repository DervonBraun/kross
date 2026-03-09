using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/UnlockInputStep")]
    public sealed class UnlockInputStep : FlowStepAsset
    {
        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            if (ctx.Services.TryGet<Player.PlayerContext>(out var player))
                player.SetMode(Player.PlayerMode.Gameplay);
            else
                Debug.LogWarning("[UnlockInputStep] PlayerContext not found in ServiceLocator.");
        }
    }
}
