using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Player
{
    public sealed class StripColorWave : MonoBehaviour
    {
        [Header("Palette (optional)")]
        [SerializeField] private UIColorPaletteRef _paletteRef;
        [Tooltip("Ключи цветов из палитры. Используются если _paletteRef назначен. Нужно минимум 2 ключа.")]
        [SerializeField] private string[] _colorKeys = { "wave0", "wave1", "wave2" };

        [Header("Colors")]
        [Tooltip("Рабочий список цветов. Если палитра назначена — будет автоматически переписан из палитры.")]
        [SerializeField] private List<Color> _colors = new() { Color.white, Color.gray };

        [Header("Strips")]
        [SerializeField] private Image[] _stripBackgrounds;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float _minDuration = 0.8f;
        [SerializeField, Min(0.05f)] private float _maxDuration = 2.0f;
        [SerializeField] private Ease _ease = Ease.InOutSine;

        private int[] _colorIndices;
        private Tweener[] _tweens;

        // Снимок цветов на момент запуска твина — не зависит от последующих Clear()
        private Color[] _colorsSnapshot;

        private void OnEnable()
        {
            RebuildColorsFromPalette();
            if (_colors == null || _colors.Count < 2) return;
            if (_stripBackgrounds == null || _stripBackgrounds.Length == 0) return;

            if (_paletteRef != null)
                _paletteRef.PaletteRefChanged += OnPaletteChanged;

            int count = _stripBackgrounds.Length;
            _colorIndices = new int[count];
            _tweens        = new Tweener[count];

            TakeColorsSnapshot();

            for (int i = 0; i < count; i++)
            {
                var img = _stripBackgrounds[i];
                if (img == null) continue;

                _colorIndices[i] = Random.Range(0, _colorsSnapshot.Length);
                img.color = _colorsSnapshot[_colorIndices[i]];

                // Захватываем локальную копию индекса — иначе все лямбды захватят i == count
                int idx = i;
                StartStripTransition(idx);
            }
        }

        private void OnDisable()
        {
            if (_paletteRef != null)
                _paletteRef.PaletteRefChanged -= OnPaletteChanged;

            if (_tweens == null) return;
            foreach (var t in _tweens) t?.Kill();
        }

        private void StartStripTransition(int i)
        {
            var img = _stripBackgrounds[i];
            if (img == null) return;

            if (_colorsSnapshot == null || _colorsSnapshot.Length < 2) return;

            _colorIndices[i] = Mathf.Clamp(_colorIndices[i], 0, _colorsSnapshot.Length - 1);

            int nextIndex = (_colorIndices[i] + 1) % _colorsSnapshot.Length;
            Color target   = _colorsSnapshot[nextIndex];

            float min = Mathf.Max(0.05f, Mathf.Min(_minDuration, _maxDuration));
            float max = Mathf.Max(0.05f, Mathf.Max(_minDuration, _maxDuration));
            float dur = Random.Range(min, max);

            _tweens[i]?.Kill();
            _tweens[i] = img.DOColor(target, dur)
                .SetEase(_ease)
                .OnComplete(() =>
                {
                    _colorIndices[i] = nextIndex;
                    StartStripTransition(i);
                });
        }

        private void OnPaletteChanged()
        {
            RebuildColorsFromPalette();
            if (_colors == null || _colors.Count < 2) return;
            if (_tweens == null) return;

            TakeColorsSnapshot();

            for (int i = 0; i < _tweens.Length; i++)
            {
                var t = _tweens[i];
                if (t == null || !t.active) continue;

                _colorIndices[i] = Mathf.Clamp(_colorIndices[i], 0, _colorsSnapshot.Length - 1);
                int nextIndex   = (_colorIndices[i] + 1) % _colorsSnapshot.Length;
                Color newTarget = _colorsSnapshot[nextIndex];

                // false = не снэпать старт на текущее значение (без рывка)
                t.ChangeEndValue(newTarget, false);
            }
        }

        /// <summary>
        /// Перестраивает _colors из палитры (если назначена).
        /// Не вызывается внутри горячего пути твина — только при старте и смене палитры.
        /// </summary>
        private void RebuildColorsFromPalette()
        {
            if (_paletteRef != null && _colorKeys != null && _colorKeys.Length >= 2)
            {
                if (_colors == null) _colors = new List<Color>(_colorKeys.Length);
                _colors.Clear();
                foreach (var key in _colorKeys)
                    _colors.Add(_paletteRef.Get(key));
            }

            if (_colors == null) _colors = new List<Color>(2);

            if (_colors.Count == 0)
            {
                _colors.Add(Color.white);
                _colors.Add(Color.gray);
            }
            else if (_colors.Count == 1)
            {
                _colors.Add(Color.gray);
            }
        }

        /// <summary>
        /// Снимок _colors в массив — твины читают из него, не из List.
        /// Это изолирует горячий путь от любых Clear()/rebuild на _colors.
        /// </summary>
        private void TakeColorsSnapshot()
        {
            _colorsSnapshot = _colors.ToArray();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_minDuration > _maxDuration) _maxDuration = _minDuration;
            if (_stripBackgrounds == null || _stripBackgrounds.Length == 0)
                AutoPopulate();
        }

        [ContextMenu("Auto-Populate Strip Backgrounds")]
        private void AutoPopulate()
        {
            var found = new List<Image>();
            foreach (Transform child in transform)
            {
                if (child.TryGetComponent<Image>(out var img))
                {
                    found.Add(img);
                    continue;
                }

                if (child.childCount > 0 && child.GetChild(0).TryGetComponent<Image>(out var bgImg))
                    found.Add(bgImg);
            }

            _stripBackgrounds = found.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}