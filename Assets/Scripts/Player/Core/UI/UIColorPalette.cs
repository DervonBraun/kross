using System;
using System.Collections.Generic;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// ScriptableObject — база данных именованных цветов UI-темы.
    ///
    /// Создаётся через: Assets → Create → AN/UI/Color Palette
    ///
    /// Используй UIColorPaletteRef как единственную точку доступа —
    /// это позволяет менять тему целиком, просто переключив один SO.
    /// </summary>
    [CreateAssetMenu(menuName = "AN/UI/Color Palette", fileName = "UIColorPalette")]
    public sealed class UIColorPalette : ScriptableObject
    {
        [Serializable]
        public struct ColorEntry
        {
            [Tooltip("Уникальный ключ. Используется в UIColorBinder.")]
            public string key;

            [Tooltip("Цвет.")]
            public Color color;

            [Tooltip("Описание (только для редактора).")]
            public string description;
        }

        [SerializeField] private ColorEntry[] _entries = Array.Empty<ColorEntry>();

        // Lazy-built lookup
        private Dictionary<string, Color> _lookup;

        public event Action PaletteChanged;

        /// <summary>
        /// Получить цвет по ключу. Возвращает Color.magenta если ключ не найден.
        /// </summary>
        public Color Get(string key)
        {
            BuildLookup();
            return _lookup.TryGetValue(key, out var c) ? c : Color.magenta;
        }

        /// <summary>
        /// Попытаться получить цвет по ключу.
        /// </summary>
        public bool TryGet(string key, out Color color)
        {
            BuildLookup();
            return _lookup.TryGetValue(key, out color);
        }

        /// <summary>
        /// Все ключи (для дропдауна в редакторе).
        /// </summary>
        public IReadOnlyList<ColorEntry> Entries => _entries;

        /// <summary>
        /// Обновить цвет в рантайме и уведомить всех байндеров.
        /// </summary>
        public void SetColor(string key, Color color)
        {
            BuildLookup();
            if (!_lookup.ContainsKey(key)) return;
            _lookup[key] = color;

            // Обновляем массив для сериализации
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].key == key)
                {
                    var e = _entries[i];
                    e.color = color;
                    _entries[i] = e;
                    break;
                }
            }

            PaletteChanged?.Invoke();
        }

        private void BuildLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, Color>(_entries.Length);
            foreach (var e in _entries)
                if (!string.IsNullOrWhiteSpace(e.key))
                    _lookup[e.key] = e.color;
        }

        // Перестраиваем lookup при изменении в редакторе
        private void OnValidate() => _lookup = null;
    }
}