using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Flow
{
    // ─── PageHistory ──────────────────────────────────────────────────────────

    /// <summary>
    /// Простая runtime-реализация IPageHistory.
    /// Хранится в WebUIManager. Сбрасывается при перезагрузке сцены.
    /// Для персистентной истории — замените на реализацию с PlayerPrefs/SaveSystem.
    /// </summary>
    public sealed class PageHistory : IPageHistory
    {
        private readonly HashSet<PageId> _opened = new();

        public bool HasEverOpened(PageId pageId) => _opened.Contains(pageId);
        public void MarkOpened(PageId pageId)    => _opened.Add(pageId);
    }

    // ─── SimpleServiceLocator ─────────────────────────────────────────────────

    /// <summary>
    /// Минимальный сервис-локатор для передачи зависимостей в PageContext.
    /// Регистрация — через Register<T>.
    /// </summary>
    public sealed class SimpleServiceLocator : IServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj) && obj is T t) return t;
            Debug.LogWarning($"[ServiceLocator] Service '{typeof(T).Name}' not registered.");
            return null;
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj) && obj is T t)
            {
                service = t;
                return true;
            }
            service = default;
            return false;
        }
    }

    // ─── ReadOnly attribute (редакторный) ─────────────────────────────────────

    /// <summary>
    /// Помечает поле как ReadOnly в Inspector — только для отображения состояния.
    /// </summary>
    public sealed class ReadOnlyAttribute : PropertyAttribute { }
}
