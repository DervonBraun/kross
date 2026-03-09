using System.Threading;
using UnityEngine;

namespace UI.Flow.Steps
{
    [CreateAssetMenu(menuName = "UI/Steps/LockInputStep")]
    public sealed class LockInputStep : FlowStepAsset
    {
        [Tooltip("Режим, в который переключается PlayerContext при блокировке.")]
        public Player.PlayerMode LockMode = Player.PlayerMode.UiInventory;

        protected override async Awaitable ExecuteCoreAsync(PageContext ctx, CancellationToken ct)
        {
            if (ctx.Services.TryGet<Player.PlayerContext>(out var player))
                player.SetMode(LockMode);
            else
                Debug.LogWarning("[LockInputStep] PlayerContext not found in ServiceLocator.");
        }
    }
}
