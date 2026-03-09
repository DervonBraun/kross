using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tasks
{
    /// <summary>
    /// Screen Space Overlay HUD — две полоски слева экрана.
    ///
    /// Ключевая идея анимации:
    ///   TitleRow и PriceRow — это одновременно RectMask2D-контейнер И анимируемый объект.
    ///   Их sizeDelta.x растёт от 0 до нужной ширины.
    ///
    /// Иерархия:
    ///   TaskHUD [VerticalLayoutGroup]
    ///   ├── TitleRow  [RectMask2D, sizeDelta.x анимируется]   ← _titleRow
    ///   │   ├── TitleBg   [Image, stretch]
    ///   │   └── TitleText [TMP_Text]                          ← _titleLabel
    ///   └── PriceRow  [RectMask2D, sizeDelta.x анимируется]   ← _priceRow
    ///       ├── PriceBg      [Image, stretch]
    ///       └── PriceContent [HorizontalLayoutGroup]
    ///           ├── BasePriceText    [TMP_Text]               ← _basePriceLabel
    ///           └── CurrentPriceText [TMP_Text]               ← _currentPriceLabel
    ///
    /// Публичное API:
    ///   Show(title, base, current, heatAffected, theme)  — задачи с токен-наградой
    ///   Show(title, info, theme)                         — произвольный текст (узел, хаб, etc.)
    ///   Hide()
    /// </summary>
    public sealed class TaskHUD : MonoBehaviour
    {
        public static TaskHUD Instance { get; private set; }

        // ─── Rows ──────────────────────────────────────────────────────────────
        [Header("Rows (RectMask2D + анимируется sizeDelta.x)")]
        [SerializeField] private RectTransform _titleRow;
        [SerializeField] private RectTransform _priceRow;

        // ─── Backgrounds ───────────────────────────────────────────────────────
        [Header("Backgrounds (первый дочерний Image в каждом Row)")]
        [SerializeField] private Image _titleBg;
        [SerializeField] private Image _priceBg;

        // ─── Labels ────────────────────────────────────────────────────────────
        [Header("Labels")]
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _basePriceLabel;
        [SerializeField] private TMP_Text _currentPriceLabel;

        // ─── Typing effects ────────────────────────────────────────────────────
        [Header("Typing Effects (optional)")]
        [SerializeField] private AN_.ANChatTypingEffect _titleTyping;
        [SerializeField] private AN_.ANChatTypingEffect _basePriceTyping;
        [SerializeField] private AN_.ANChatTypingEffect _currentPriceTyping;

        // ─── Animation ────────────────────────────────────────────────────────
        [Header("Animation")]
        [SerializeField] private float _barDuration  = 0.28f;
        [SerializeField] private float _staggerDelay = 0.07f;
        [SerializeField] private Ease  _showEase     = Ease.OutCubic;
        [SerializeField] private Ease  _hideEase     = Ease.InCubic;
        [SerializeField] private float _typingDelay  = 0.05f;

        // ─── Default theme (используется если тема не передана) ───────────────
        [Header("Default Theme (fallback)")]
        [SerializeField] private TaskHudTheme _defaultTheme;

        // ─── Internal ─────────────────────────────────────────────────────────
        private Sequence _showSeq;
        private Sequence _hideSeq;
        private bool     _visible;
        private float    _titleTargetWidth;
        private float    _priceTargetWidth;

        // Текущий режим показа — нужен для корректного измерения ширины
        private bool _infoMode; // true = режим произвольного текста (без basePriceLabel)

        // ══════════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Если bg-ссылки не проставлены в инспекторе — найдём сами
            TryAutoFindBgs();
            ApplyTheme(_defaultTheme);
            SetHiddenInstant();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Public API — задача с токен-наградой (старый вариант + тема)
        // ══════════════════════════════════════════════════════════════════════

        /// <param name="theme">Если null — используется DefaultTheme из инспектора.</param>
        public void Show(string displayName,
                         TokenAmount baseReward, TokenAmount currentReward,
                         bool heatAffected,
                         TaskHudTheme theme = null)
        {
            _infoMode = false;
            PrepareShow(theme);
            SetTextsReward(displayName, baseReward, currentReward, heatAffected, theme ?? _defaultTheme);
            StartCoroutineShow(heatAffected);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Public API — произвольный информационный текст (узел, хаб, etc.)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Показывает HUD с заголовком и одной строкой информации.
        /// Идеально для: "Узел A — 85% / "Хаб — 120 G накоплено" и т.п.
        /// </summary>
        /// <param name="theme">Если null — используется DefaultTheme.</param>
        public void Show(string title, string info, TaskHudTheme theme = null)
        {
            _infoMode = true;
            PrepareShow(theme);
            SetTextsInfo(title, info, theme ?? _defaultTheme);
            StartCoroutineShow(heatAffected: false);
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;

            _showSeq?.Kill();
            StopAllCoroutines();
            StopTypingEffects();

            _hideSeq = DOTween.Sequence();

            _hideSeq.Append(
                DOTween.To(
                    () => GetRowWidth(_priceRow),
                    w  => SetRowWidth(_priceRow, w),
                    0f, _barDuration * 0.8f
                ).SetEase(_hideEase)
            );

            _hideSeq.Insert(_staggerDelay,
                DOTween.To(
                    () => GetRowWidth(_titleRow),
                    w  => SetRowWidth(_titleRow, w),
                    0f, _barDuration * 0.8f
                ).SetEase(_hideEase)
            );

            _hideSeq.OnComplete(() => gameObject.SetActive(false));
            _hideSeq.Play();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Internals — подготовка показа
        // ══════════════════════════════════════════════════════════════════════

        private void PrepareShow(TaskHudTheme theme)
        {
            _hideSeq?.Kill();
            StopTypingEffects();

            ApplyTheme(theme ?? _defaultTheme);
        }

        private void StartCoroutineShow(bool heatAffected)
        {
            if (_visible)
            {
                StartCoroutine(ShowAfterLayout(heatAffected, instant: true));
                return;
            }

            _visible = true;
            gameObject.SetActive(true);
            SetRowWidth(_titleRow, 0f);
            SetRowWidth(_priceRow, 0f);

            StartCoroutine(ShowAfterLayout(heatAffected, instant: false));
        }

        // ══════════════════════════════════════════════════════════════════════
        // Coroutine
        // ══════════════════════════════════════════════════════════════════════

        private System.Collections.IEnumerator ShowAfterLayout(bool heatAffected, bool instant)
        {
            yield return null; // один кадр → layout rebuild

            _titleTargetWidth = MeasureRowWidth(_titleLabel);
            _priceTargetWidth = MeasureRowWidth(_basePriceLabel.gameObject.activeSelf
                ? _basePriceLabel
                : _currentPriceLabel);

            if (instant)
            {
                SetRowWidth(_titleRow, _titleTargetWidth);
                SetRowWidth(_priceRow, _priceTargetWidth);
                PlayTypingEffects(heatAffected);
                yield break;
            }

            _showSeq?.Kill();
            _showSeq = DOTween.Sequence();

            _showSeq.Append(
                DOTween.To(
                    () => GetRowWidth(_titleRow),
                    w  => SetRowWidth(_titleRow, w),
                    _titleTargetWidth, _barDuration
                ).SetEase(_showEase)
            );

            _showSeq.Insert(_staggerDelay,
                DOTween.To(
                    () => GetRowWidth(_priceRow),
                    w  => SetRowWidth(_priceRow, w),
                    _priceTargetWidth, _barDuration
                ).SetEase(_showEase)
            );

            _showSeq.InsertCallback(_barDuration + _typingDelay,
                () => PlayTypingEffects(heatAffected));

            _showSeq.Play();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Text setters
        // ══════════════════════════════════════════════════════════════════════

        private void SetTextsReward(string displayName,
                                    TokenAmount baseReward, TokenAmount currentReward,
                                    bool heatAffected,
                                    TaskHudTheme theme)
        {
            _titleLabel.color = theme.titleTextColor;
            _titleLabel.SetText(displayName);

            if (heatAffected)
            {
                _basePriceLabel.gameObject.SetActive(true);
                _basePriceLabel.color = theme.strikeColor;
                _basePriceLabel.SetText($"<s>{FormatReward(baseReward)}</s>");

                _currentPriceLabel.color = theme.modifiedTextColor;
                _currentPriceLabel.SetText(FormatReward(currentReward));
            }
            else
            {
                _basePriceLabel.gameObject.SetActive(false);
                _currentPriceLabel.color = theme.primaryTextColor;
                _currentPriceLabel.SetText(FormatReward(baseReward));
            }
        }

        private void SetTextsInfo(string title, string info, TaskHudTheme theme)
        {
            _titleLabel.color = theme.titleTextColor;
            _titleLabel.SetText(title);

            _basePriceLabel.gameObject.SetActive(false);
            _currentPriceLabel.color = theme.primaryTextColor;
            _currentPriceLabel.SetText(info);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Theme application
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyTheme(TaskHudTheme theme)
        {
            if (theme == null) return;
            if (_titleBg != null) _titleBg.color = theme.titleBgColor;
            if (_priceBg != null) _priceBg.color = theme.priceBgColor;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════

        private void PlayTypingEffects(bool heatAffected)
        {
            _titleTyping?.Play();
            _currentPriceTyping?.Play();
            if (heatAffected) _basePriceTyping?.Play();
        }

        private void StopTypingEffects()
        {
            _titleTyping?.Stop();
            _basePriceTyping?.Stop();
            _currentPriceTyping?.Stop();
        }

        private float MeasureRowWidth(TMP_Text referenceLabel)
        {
            float textWidth = referenceLabel.preferredWidth;

            if (_basePriceLabel.gameObject.activeSelf
                && ReferenceEquals(referenceLabel, _basePriceLabel))
            {
                textWidth = _basePriceLabel.preferredWidth
                          + _currentPriceLabel.preferredWidth
                          + 8f;
            }

            return textWidth + 24f;
        }

        private static float GetRowWidth(RectTransform rt)  => rt.sizeDelta.x;
        private static void  SetRowWidth(RectTransform rt, float w)
        {
            var sd = rt.sizeDelta;
            sd.x   = w;
            rt.sizeDelta = sd;
        }

        private void SetHiddenInstant()
        {
            SetRowWidth(_titleRow, 0f);
            SetRowWidth(_priceRow, 0f);
            gameObject.SetActive(false);
        }

        private void TryAutoFindBgs()
        {
            if (_titleBg == null && _titleRow != null && _titleRow.childCount > 0)
                _titleRow.GetChild(0).TryGetComponent(out _titleBg);
            if (_priceBg == null && _priceRow != null && _priceRow.childCount > 0)
                _priceRow.GetChild(0).TryGetComponent(out _priceBg);
        }

        private static string FormatReward(in TokenAmount t)
        {
            var sb = new System.Text.StringBuilder();
            if (t.red   > 0) sb.Append($"{t.red}R ");
            if (t.green > 0) sb.Append($"{t.green}G ");
            if (t.blue  > 0) sb.Append($"{t.blue}B ");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : "0";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoFindBgs();
        }
#endif
    }
}