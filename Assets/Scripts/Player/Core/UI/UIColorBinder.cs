using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Player
{
    /// <summary>
    /// Привязывает цвет Image или TMP_Text к именованному ключу в UIColorPaletteRef.
    ///
    /// ─── Использование ──────────────────────────────────────────────────────
    ///   1. Повесь на GameObject рядом с Image/TMP_Text.
    ///   2. Укажи UIColorPaletteRef.
    ///   3. Введи ключ цвета (например "primary", "accent", "background").
    ///   4. При смене палитры или значений — цвет обновится автоматически.
    ///
    /// Работает в Edit Mode ([ExecuteAlways]).
    /// </summary>
    [ExecuteAlways]
    public sealed class UIColorBinder : MonoBehaviour
    {
        [SerializeField] private UIColorPaletteRef _paletteRef;
        [SerializeField] private string            _colorKey;

        [Header("Target (авто-поиск на этом же объекте)")]
        [SerializeField] private Image    _image;
        [SerializeField] private TMP_Text _text;

        [Header("Blend")]
        [Tooltip("Итоговый цвет = palette color * tint.")]
        [SerializeField] private Color _tint = Color.white;

        [Tooltip("Применить только alpha из палитры, не трогая RGB текущего цвета.")]
        [SerializeField] private bool _alphaOnly;

        private void OnEnable()
        {
            Subscribe();
            Apply();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            if (_image  == null) TryGetComponent(out _image);
            if (_text   == null) TryGetComponent(out _text);
            Apply();
        }

        private void Subscribe()
        {
            if (_paletteRef == null) return;
            _paletteRef.PaletteRefChanged += Apply;
        }

        private void Unsubscribe()
        {
            if (_paletteRef == null) return;
            _paletteRef.PaletteRefChanged -= Apply;
        }

        private void Apply()
        {
            if (_paletteRef == null || string.IsNullOrWhiteSpace(_colorKey)) return;
            if (!_paletteRef.TryGet(_colorKey, out var paletteColor)) return;

            Color final;
            if (_alphaOnly)
            {
                // Берём текущий цвет, только заменяем alpha
                final = _image != null ? _image.color : (_text != null ? _text.color : Color.white);
                final.a = paletteColor.a * _tint.a;
            }
            else
            {
                final = paletteColor * _tint;
            }

            if (_image != null) _image.color = final;
            if (_text  != null) _text.color  = final;
        }

        /// <summary>
        /// Сменить ключ в рантайме и немедленно применить цвет.
        /// </summary>
        public void SetKey(string key)
        {
            _colorKey = key;
            Apply();
        }

        /// <summary>
        /// Применить цвет вручную (если нужен ручной trigger).
        /// </summary>
        public void Refresh() => Apply();
    }
}