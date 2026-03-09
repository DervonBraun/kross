using System.Collections.Generic;
using UnityEngine;

namespace UI.Flow.Inventory
{
    /// <summary>
    /// Универсальный сервис idle-таймеров. Отслеживает время закрытия
    /// для любого количества страниц через словарь PageId → lastCloseTime.
    ///
    /// Регистрируется один раз в WebUIManagerBootstrap.
    /// Подписывается на PageOpened/PageClosed у WebUIManager.
    ///
    /// Использование в условии:
    ///   service.IsIdle(pageId, thresholdSeconds)
    /// </summary>
    public sealed class IdleTimerService
    {
        private readonly Dictionary<PageId, float> _lastCloseTimes = new();

        // ── Query ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Прошло ли больше <paramref name="thresholdSeconds"/> с последнего закрытия страницы.
        /// Если страница ни разу не закрывалась — считается idle (первое открытие).
        /// </summary>
        public bool IsIdle(PageId pageId, float thresholdSeconds)
        {
            if (!_lastCloseTimes.TryGetValue(pageId, out float lastClose))
                return true; // никогда не закрывалась → всегда холодно

            return (Time.unscaledTime - lastClose) >= thresholdSeconds;
        }

        /// <summary>Секунд прошло с последнего закрытия. float.MaxValue если никогда не закрывалась.</summary>
        public float SecondsSinceClose(PageId pageId)
        {
            if (!_lastCloseTimes.TryGetValue(pageId, out float lastClose))
                return float.MaxValue;

            return Time.unscaledTime - lastClose;
        }

        // ── WebUIManager event handlers ───────────────────────────────────────

        public void OnPageOpened(PageId pageId) { /* при необходимости */ }

        public void OnPageClosed(PageId pageId)
        {
            _lastCloseTimes[pageId] = Time.unscaledTime;
        }
    }
}