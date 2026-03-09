using UnityEngine;

namespace Player
{
    /// <summary>
    /// ScriptableObject — указатель на активную UIColorPalette.
    ///
    /// Создаётся через: Assets → Create → AN/UI/Color Palette Ref
    ///
    /// ВСЕ UIColorBinder'ы ссылаются на этот объект, а не напрямую на палитру.
    /// Чтобы сменить тему — просто смени _palette здесь.
    /// Чтобы изменить один цвет — вызови ActivePalette.SetColor(...).
    /// </summary>
    [CreateAssetMenu(menuName = "AN/UI/Color Palette Ref", fileName = "UIColorPaletteRef")]
    public sealed class UIColorPaletteRef : ScriptableObject
    {
        [SerializeField] private UIColorPalette _palette;

        public UIColorPalette ActivePalette => _palette;

        /// <summary>
        /// Сменить тему целиком. Все UIColorBinder'ы обновятся автоматически.
        /// </summary>
        public void SetPalette(UIColorPalette newPalette)
        {
            if (_palette == newPalette) return;

            if (_palette != null)
                _palette.PaletteChanged -= OnPaletteChanged;

            _palette = newPalette;

            if (_palette != null)
                _palette.PaletteChanged += OnPaletteChanged;

            PaletteRefChanged?.Invoke();
        }

        public event System.Action PaletteRefChanged;

        private void OnEnable()
        {
            if (_palette != null)
                _palette.PaletteChanged += OnPaletteChanged;
        }

        private void OnDisable()
        {
            if (_palette != null)
                _palette.PaletteChanged -= OnPaletteChanged;
        }

        private void OnPaletteChanged() => PaletteRefChanged?.Invoke();

        public Color Get(string key) => _palette != null ? _palette.Get(key) : Color.magenta;
        public bool TryGet(string key, out Color color)
        {
            if (_palette != null) return _palette.TryGet(key, out color);
            color = Color.magenta;
            return false;
        }
    }
}