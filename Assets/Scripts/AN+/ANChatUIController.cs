using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AN_
{
    public sealed class ANChatUIController : MonoBehaviour
    {
        [Header("Chat Panel")]
        [SerializeField] private GameObject _chatPanelRoot;
        [SerializeField] private TMP_Text   _chatTitle;
        [SerializeField] private TMP_Text   _chatBody;

        [SerializeField] private ANChatTypingEffect _typingEffect;

        [Header("Thinking Indicator")]
        [Tooltip("Текст-заглушка, который показывается во время генерации ответа (вместо ANThinkingDots).")]
        [SerializeField] private TMP_Text    _thinkingText;
        [Tooltip("CanvasGroup для анимации alpha. Если не задан, будет попытка найти на _thinkingText.")]
        [SerializeField] private CanvasGroup _thinkingCanvasGroup;
        [SerializeField] private string      _thinkingLabel = "Генерирую ответ…";
        [SerializeField, Min(0f)] private float _thinkingFadeIn  = 0.12f;
        [SerializeField, Min(0f)] private float _thinkingFadeOut = 0.10f;
        [SerializeField] private Ease          _thinkingEase    = Ease.OutCubic;

        [Header("Fake Generation Time")]
        [SerializeField, Min(0f)] private float _responseGenMin = 0.8f;
        [SerializeField, Min(0f)] private float _responseGenMax = 2.5f;

        [Header("Tabs List")]
        [SerializeField] private RectTransform _tabsContent;
        [SerializeField] private ANChatTabRow  _tabRowPrefab;
        [SerializeField, Min(0f)] private float _tabAppearTime = 0.18f;
        [SerializeField] private Ease           _tabEase       = Ease.OutCubic;

        [Header("Bubble Auto Height")]
        [SerializeField] private RectTransform _bubbleRoot;
        [SerializeField, Min(0f)] private float _paddingY        = 24f;
        [SerializeField, Min(0f)] private float _minHeight       = 80f;
        [SerializeField, Min(0f)] private float _maxHeight       = 520f;
        [SerializeField, Min(0f)] private float _resizeTweenTime = 0.12f;
        [SerializeField] private Ease          _resizeEase      = Ease.OutCubic;
        [SerializeField, Min(0.01f)] private float _resizeThrottle = 0.06f;

        [Header("Attachments UI")]
        [SerializeField] private RectTransform _attachmentsRoot;
        [SerializeField] private ANChatAttachmentView _attachmentPrefab;
        [SerializeField] private ANChatAttachmentView _attachmentLoadingTile;
        [SerializeField, Min(0)] private int _maxAttachmentsToShow = 8;

        [Header("Attachments Random Load")]
        [SerializeField, Min(0f)] private float _imageLoadMin  = 0.3f;
        [SerializeField, Min(0f)] private float _imageLoadMax  = 1.2f;
        [SerializeField, Min(0f)] private float _attachPopTime = 0.18f;
        [SerializeField] private Ease _attachEase = Ease.OutBack;
        [SerializeField, Min(0f)] private float _attachStagger = 0.06f;

        [Header("Image Zoom (Lightbox)")]
        [SerializeField] private ANChatImageLightbox _lightbox;

        [Header("Markdown")]
        [Tooltip("Применять markdown-парсер к тексту ответа перед отображением.")]
        [SerializeField] private bool _parseMarkdown = true;

        private readonly Dictionary<string, TabData> _tabs = new();
        private readonly Dictionary<string, ANChatTabRow> _rows = new();

        private Tween _resizeTween;
        private Tween _genTween;
        private Tween _thinkingTween;

        private float _nextResizeTime;
        private float _lastHeight = -1f;

        private sealed class TabData
        {
            public string id;
            public string title;
            public string text;
            public Sprite[] attachments;
            public bool attachmentsLoaded;
        }

        private void Awake()
        {
            if (_typingEffect == null && _chatBody != null)
                _typingEffect = _chatBody.GetComponent<ANChatTypingEffect>();

            if (_thinkingCanvasGroup == null && _thinkingText != null)
                _thinkingCanvasGroup = _thinkingText.GetComponent<CanvasGroup>();

            if (_thinkingCanvasGroup == null && _thinkingText != null)
                _thinkingCanvasGroup = _thinkingText.gameObject.AddComponent<CanvasGroup>();

            SetThinking(false);

            if (_chatPanelRoot != null)
                _chatPanelRoot.SetActive(false);

            ClearAttachmentsImmediate();
        }

        private void Update()
        {
            if (_typingEffect != null && _typingEffect.IsPlaying)
                UpdateBubbleHeightFromRendered(animate: true);
        }

        public void HideChat()
        {
            StopAllEffects();
            if (_chatPanelRoot != null)
                _chatPanelRoot.SetActive(false);
        }

        public void OpenFromRequest(ANRequestDef def)
        {
            if (def == null) return;
            OpenFromRequest(def.id, def.title, def.responseText, def.attachments);
        }

        public void OpenFromRequest(string tabId, string tabTitle, string fullText, Sprite[] attachments)
        {
            if (string.IsNullOrWhiteSpace(tabId)) return;

            EnsureTab(tabId, tabTitle, fullText, attachments);
            ShowChatPanel();
            SetTitle(tabTitle);
            RunGenerateThenType(tabId);
        }

        public void OpenTabInstant(string tabId)
        {
            if (!_tabs.TryGetValue(tabId, out var tab)) return;

            ShowChatPanel();
            SetTitle(tab.title);
            StopAllEffects();
            SetBodyTextInstant(tab.text);

            if (tab.attachmentsLoaded)
                ShowAttachmentsInstant(tab.attachments);
            else
                ShowAttachmentsWithRandomLoad(tabId, tab.attachments);
        }

        private void RunGenerateThenType(string tabId)
        {
            StopAllEffects();
            ClearAttachmentsImmediate();

            if (!_tabs.TryGetValue(tabId, out var tab)) return;

            ClearBodyText();
            _lastHeight = -1f;
            UpdateBubbleHeightFromRendered(animate: false);

            SetThinking(true);

            float genTime = Random.Range(
                Mathf.Max(0f, _responseGenMin),
                Mathf.Max(_responseGenMin, _responseGenMax)
            );

            _genTween = DOVirtual.DelayedCall(genTime, () =>
            {
                SetThinking(false);
                TypeText(tabId, tab.text);
            }, ignoreTimeScale: true);
        }

        private void TypeText(string tabId, string rawText)
        {
            if (_chatBody == null || _typingEffect == null) return;

            string displayText = PrepareText(rawText);

            // Важно: не ставим текст «голым» в TMP, иначе он может мигнуть целиком до старта эффекта.
            // Пусть TypingEffect сам поставит и сразу спрячёт символы.
            _typingEffect.PlayText(displayText, onComplete: () => { /* ... */ });

            _lastHeight = -1f;
            UpdateBubbleHeightFromRendered(animate: false);

            if (_tabs.TryGetValue(tabId, out var tab))
                BeginAttachmentsDuringTyping(tabId, tab);

            // PlayText уже запустил эффект.
        }

        private string PrepareText(string raw)
        {
            raw ??= "";
            return _parseMarkdown ? ANMarkdownParser.Parse(raw) : raw;
        }

        private void BeginAttachmentsDuringTyping(string tabId, TabData tab)
        {
            if (tab.attachmentsLoaded)
                ShowAttachmentsInstant(tab.attachments);
            else
                ShowAttachmentsWithRandomLoad(tabId, tab.attachments);
        }

        private void ShowAttachmentsWithRandomLoad(string tabId, Sprite[] sprites)
        {
            if (_attachmentsRoot == null) return;

            int count = sprites == null ? 0 : Mathf.Min(sprites.Length, _maxAttachmentsToShow);
            if (count <= 0)
            {
                MarkAttachmentsLoaded(tabId);
                return;
            }

            var slots = new ANChatAttachmentView[count];
            for (int i = 0; i < count; i++)
            {
                var prefab = _attachmentLoadingTile != null ? _attachmentLoadingTile : _attachmentPrefab;
                if (prefab == null) break;

                var slot = Instantiate(prefab, _attachmentsRoot);
                slot.SetAsLoadingVisual(true);
                slot.SetSprite(null);
                slot.ShowInstant();
                slot.SetInteractable(false);
                slots[i] = slot;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_attachmentsRoot);
            Canvas.ForceUpdateCanvases();

            for (int i = 0; i < count; i++)
            {
                int idx = i;
                float delay = Random.Range(
                    Mathf.Max(0f, _imageLoadMin),
                    Mathf.Max(_imageLoadMin, _imageLoadMax)
                ) + idx * _attachStagger;

                DOVirtual.DelayedCall(delay, () =>
                {
                    if (idx >= slots.Length || slots[idx] == null) return;

                    var slot = slots[idx];
                    slot.SetAsLoadingVisual(false);
                    slot.SetSprite(sprites[idx]);
                    slot.SetInteractable(true);

                    if (_lightbox != null)
                    {
                        var sp = sprites[idx];
                        slot.SetOnClick(() => _lightbox.Show(sp));
                    }

                    slot.PlayPop(_attachPopTime, _attachEase);
                }, ignoreTimeScale: true);
            }

            float markDelay = Mathf.Max(_imageLoadMax, _imageLoadMin)
                              + (count - 1) * _attachStagger + 0.05f;
            DOVirtual.DelayedCall(markDelay, () => MarkAttachmentsLoaded(tabId), ignoreTimeScale: true);
        }

        private void ShowAttachmentsInstant(Sprite[] sprites)
        {
            ClearAttachmentsImmediate();
            if (_attachmentsRoot == null || _attachmentPrefab == null) return;

            int count = sprites == null ? 0 : Mathf.Min(sprites.Length, _maxAttachmentsToShow);
            for (int i = 0; i < count; i++)
            {
                var view = Instantiate(_attachmentPrefab, _attachmentsRoot);
                view.SetAsLoadingVisual(false);
                view.SetSprite(sprites[i]);
                view.ShowInstant();
                view.SetInteractable(true);

                if (_lightbox != null)
                {
                    var sp = sprites[i];
                    view.SetOnClick(() => _lightbox.Show(sp));
                }
            }
        }

        private void ClearAttachmentsImmediate()
        {
            if (_attachmentsRoot == null) return;
            for (int i = _attachmentsRoot.childCount - 1; i >= 0; i--)
                Destroy(_attachmentsRoot.GetChild(i).gameObject);
        }

        private void MarkAttachmentsLoaded(string tabId)
        {
            if (_tabs.TryGetValue(tabId, out var tab))
                tab.attachmentsLoaded = true;
        }

        private void ShowChatPanel()
        {
            if (_chatPanelRoot != null && !_chatPanelRoot.activeSelf)
                _chatPanelRoot.SetActive(true);

            // Сразу после активации режем видимость. До конца кадра ещё успеваем.
            if (_chatBody != null)
                _chatBody.maxVisibleCharacters = 0;
        }

        private void SetTitle(string title)
        {
            if (_chatTitle != null) _chatTitle.SetText(title ?? "");
        }

        private void ClearBodyText()
        {
            if (_chatBody == null) return;
            _chatBody.SetText(string.Empty);
            _chatBody.ForceMeshUpdate();
        }

        private void SetBodyTextInstant(string rawText)
        {
            if (_chatBody == null) return;

            _chatBody.SetText(PrepareText(rawText));
            _chatBody.ForceMeshUpdate();
            _typingEffect?.Skip();

            _lastHeight = -1f;
            UpdateBubbleHeightFromRendered(animate: false);
        }

        private void SetThinking(bool on)
        {
            if (_thinkingText == null || _thinkingCanvasGroup == null) return;

            _thinkingTween?.Kill();
            _thinkingTween = null;

            if (on)
            {
                _thinkingText.SetText(_thinkingLabel ?? string.Empty);
                _thinkingText.gameObject.SetActive(true);
                _thinkingCanvasGroup.blocksRaycasts = false;
                _thinkingCanvasGroup.interactable = false;

                if (_thinkingFadeIn <= 0.001f)
                {
                    _thinkingCanvasGroup.alpha = 1f;
                    return;
                }

                _thinkingCanvasGroup.alpha = 0f;
                _thinkingTween = _thinkingCanvasGroup
                    .DOFade(1f, _thinkingFadeIn)
                    .SetEase(_thinkingEase)
                    .SetUpdate(true);
            }
            else
            {
                if (_thinkingFadeOut <= 0.001f)
                {
                    _thinkingCanvasGroup.alpha = 0f;
                    _thinkingText.gameObject.SetActive(false);
                    return;
                }

                _thinkingTween = _thinkingCanvasGroup
                    .DOFade(0f, _thinkingFadeOut)
                    .SetEase(_thinkingEase)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (_thinkingText != null)
                            _thinkingText.gameObject.SetActive(false);
                    });
            }
        }

        private void EnsureTab(string id, string title, string text, Sprite[] attachments)
        {
            title ??= "";
            text ??= "";

            if (_tabs.TryGetValue(id, out var existing))
            {
                if (!string.IsNullOrWhiteSpace(title)) existing.title = title;
                if (!string.IsNullOrWhiteSpace(text)) existing.text = text;
                existing.attachments = attachments;

                if (_rows.TryGetValue(id, out var row))
                    row.SetLabel(existing.title);

                return;
            }

            var tab = new TabData
            {
                id = id,
                title = title,
                text = text,
                attachments = attachments,
                attachmentsLoaded = false,
            };
            _tabs.Add(id, tab);

            if (_tabRowPrefab != null && _tabsContent != null)
            {
                var rowInstance = Instantiate(_tabRowPrefab, _tabsContent);
                _rows.Add(id, rowInstance);
                rowInstance.Bind(id, tab.title, () => OpenTabInstant(id));
                rowInstance.PlayAppear(_tabsContent, _tabAppearTime, _tabEase);
            }
        }

        private void UpdateBubbleHeightFromRendered(bool animate)
        {
            if (_chatBody == null || _bubbleRoot == null) return;
            if (animate && Time.unscaledTime < _nextResizeTime) return;

            _nextResizeTime = Time.unscaledTime + _resizeThrottle;
            _chatBody.ForceMeshUpdate();

            float rendered = _chatBody.GetRenderedValues(true).y;
            float target = Mathf.Clamp(rendered + _paddingY, _minHeight, _maxHeight);

            if (_lastHeight > 0f && Mathf.Abs(target - _lastHeight) < 0.5f) return;

            _lastHeight = target;
            _resizeTween?.Kill();

            if (!animate || _resizeTweenTime <= 0.001f)
            {
                _bubbleRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, target);
                return;
            }

            float start = _bubbleRoot.rect.height;
            _resizeTween = DOTween.To(
                    () => start,
                    x => { start = x; _bubbleRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, x); },
                    target,
                    _resizeTweenTime
                )
                .SetEase(_resizeEase)
                .SetUpdate(true);
        }

        private void StopAllEffects()
        {
            _typingEffect?.Stop();
            _resizeTween?.Kill();
            _genTween?.Kill();
            _thinkingTween?.Kill();

            _resizeTween = null;
            _genTween = null;
            _thinkingTween = null;

            _nextResizeTime = 0f;
            _lastHeight = -1f;

            SetThinking(false);
        }
    }
}