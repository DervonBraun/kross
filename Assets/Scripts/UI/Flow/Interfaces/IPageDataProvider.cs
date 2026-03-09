using System.Threading;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Готовит данные для страницы ДО старта flow.
    /// Регистрируется в PageRegistry рядом с PageFlowDefinition.
    ///
    /// ctx.Model = snapshot данных — заполняется провайдером, используется шагами для биндинга.
    /// Реактивные обновления модели — вне MVP-скоупа.
    /// </summary>
    public interface IPageDataProvider
    {
        Awaitable<object> ProvideAsync(PageRequest request, CancellationToken ct);
    }

    // ─── Типизированный вариант ───────────────────────────────────────────────

    /// <summary>
    /// Удобный базовый класс для провайдеров конкретных страниц.
    /// Наследники реализуют только ProvideTypedAsync.
    /// </summary>
    public abstract class PageDataProvider<TModel> : IPageDataProvider
    {
        public abstract Awaitable<TModel> ProvideTypedAsync(PageRequest req, CancellationToken ct);

        async Awaitable<object> IPageDataProvider.ProvideAsync(PageRequest req, CancellationToken ct)
            => await ProvideTypedAsync(req, ct);
    }
}
