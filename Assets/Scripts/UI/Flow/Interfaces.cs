namespace UI.Flow
{
    // ─── Gameplay API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Единственный публичный API для gameplay-слоя.
    /// Gameplay-код вызывает только этот интерфейс — прямые вызовы WebUIManager запрещены.
    /// </summary>
    public interface IPageFlowBus
    {
        void OpenPage<TPayload>(PageId pageId, TPayload payload,
            PageOpenFlags flags    = PageOpenFlags.None,
            int           priority = 0);

        void OpenPage(PageId pageId,
            PageOpenFlags flags    = PageOpenFlags.None,
            int           priority = 0);

        void ClosePage(PageId pageId);

        bool IsPageOpen(PageId pageId);
    }

    // ─── Presentation layer ───────────────────────────────────────────────────

    /// <summary>
    /// Визуальное представление страницы. Presentation-слой не знает о порядке других окон.
    /// </summary>
    public interface IPageView
    {
        PageId ViewId { get; }

        /// <summary>Синхронно сделать объект видимым (SetActive / alpha = 1) без анимации.</summary>
        void ShowInstant();

        /// <summary>Синхронно скрыть без анимации.</summary>
        void HideInstant();
    }

    // ─── Services ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Минимальный сервис-локатор, передаваемый через PageContext.
    /// Позволяет шагам получать зависимости без прямых ссылок.
    /// </summary>
    public interface IServiceLocator
    {
        T Get<T>() where T : class;
        bool TryGet<T>(out T service) where T : class;
    }

    // ─── Page History ─────────────────────────────────────────────────────────

    /// <summary>
    /// Хранит историю открытий страниц. Используется условием FirstOpenCondition.
    /// </summary>
    public interface IPageHistory
    {
        bool HasEverOpened(PageId pageId);
        void MarkOpened(PageId pageId);
    }
}
