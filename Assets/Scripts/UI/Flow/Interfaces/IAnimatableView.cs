using System.Threading;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Опциональный интерфейс для IPageView с именованными анимациями.
    /// Реализуется в конкретном MonoBehaviour (DOTween и т.д.).
    /// Используется PlayAnimationStep.
    /// </summary>
    public interface IAnimatableView
    {
        Awaitable PlayAnimationAsync(string key, CancellationToken ct);
    }
}
