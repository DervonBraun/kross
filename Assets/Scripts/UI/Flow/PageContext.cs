using System;
using System.Threading;

namespace UI.Flow
{
    /// <summary>
    /// Неизменяемый входной дескриптор запроса. Создаётся на границе системы (WebUIManager).
    /// </summary>
    public sealed class PageRequest
    {
        public PageId         PageId      { get; }
        public object         Payload     { get; }
        public Type           PayloadType { get; }
        public NavigationPolicy Policy   { get; }
        public PageOpenFlags  Flags       { get; }
        public int            Priority    { get; }

        public PageRequest(
            PageId          pageId,
            object          payload     = null,
            Type            payloadType = null,
            NavigationPolicy policy     = NavigationPolicy.Reject,
            PageOpenFlags   flags       = PageOpenFlags.None,
            int             priority    = 0)
        {
            PageId      = pageId;
            Payload     = payload;
            PayloadType = payloadType ?? payload?.GetType();
            Policy      = policy;
            Flags       = flags;
            Priority    = priority;
        }

        /// <summary>Безопасно приводит Payload к <typeparamref name="T"/>. Null если тип не совпадает.</summary>
        public T GetPayload<T>() => Payload is T t ? t : default;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runtime-сессия выполняющегося flow.
    /// Создаётся FlowRunner'ом, живёт весь цикл open/close.
    /// </summary>
    public sealed class PageContext
    {
        public PageRequest      Request  { get; }
        public CancellationToken Token   { get; }
        public object           Model   { get; set; }
        public FlowPhase        Phase   { get; set; }
        public IServiceLocator  Services { get; }

        /// <summary>История открытий — даёт доступ к FirstOpenCondition.</summary>
        public IPageHistory History { get; }

        public PageContext(
            PageRequest     request,
            CancellationToken token,
            IServiceLocator services,
            IPageHistory    history)
        {
            Request  = request;
            Token    = token;
            Services = services;
            History  = history;
            Phase    = FlowPhase.Opening;
        }

        /// <summary>Shortcut: типизированный payload.</summary>
        public T GetPayload<T>() => Request.GetPayload<T>();
    }
}
