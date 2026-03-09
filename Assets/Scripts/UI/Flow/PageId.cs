using System;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Строго-типизированный идентификатор страницы.
    /// Сравнение по строке — не по ссылке.
    /// </summary>
    [Serializable]
    public struct PageId : IEquatable<PageId>
    {
        [SerializeField] private string _id;

        public bool IsEmpty => string.IsNullOrWhiteSpace(_id);

        public PageId(string id) => _id = id ?? string.Empty;

        public bool Equals(PageId other)            => string.Equals(_id, other._id, StringComparison.Ordinal);
        public override bool Equals(object obj)     => obj is PageId p && Equals(p);
        public override int  GetHashCode()          => _id?.GetHashCode() ?? 0;
        public override string ToString()           => _id ?? "<empty>";

        public static bool operator ==(PageId a, PageId b) => a.Equals(b);
        public static bool operator !=(PageId a, PageId b) => !a.Equals(b);

        public static readonly PageId None = new(string.Empty);
    }

    // ─── Политика навигации ───────────────────────────────────────────────────

    public enum NavigationPolicy
    {
        /// <summary>Входящий запрос отклоняется если страница активна.</summary>
        Reject,

        /// <summary>Запрос кладётся в pending-слот, выполняется после закрытия текущей.</summary>
        Queue,

        /// <summary>Активный flow немедленно отменяется, стартует новый.</summary>
        Interrupt,

        /// <summary>Активная страница корректно закрывается, затем открывается pending.</summary>
        ReplaceAfterClose,
    }

    // ─── Флаги открытия ───────────────────────────────────────────────────────

    [Flags]
    public enum PageOpenFlags
    {
        None         = 0,
        SkipAnimation = 1 << 0,
        ForceRefresh  = 1 << 1,
    }

    // ─── Фаза жизни flow ──────────────────────────────────────────────────────

    public enum FlowPhase
    {
        Opening,
        Open,
        Closing,
        Closed,
    }
}
