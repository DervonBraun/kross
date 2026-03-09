using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AN_
{
    /// <summary>
    /// Typing effect для TMP_Text.
    ///
    /// Ключевое решение: НЕ используем maxVisibleCharacters для управления видимостью.
    /// maxVisibleCharacters конфликтует с vertex-alpha — TMP сбрасывает colors32 при его изменении.
    /// Вместо этого: maxVisibleCharacters = int.MaxValue всегда,
    /// а видимость символов управляется исключительно через vertex alpha.
    ///
    /// Дополнительно: подписываемся на TEXT_CHANGED_EVENT чтобы восстанавливать
    /// alpha-кэш после любого внутреннего ForceMeshUpdate от TMP.
    ///
    /// Вся логика в Update() — никаких корутин, frame-accurate timing.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ANChatTypingEffect : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField, Min(0.001f)] private float _secPerChar = 0.022f;
        [SerializeField, Min(0f)]     private float _fadeTime   = 0.09f;

        [Header("Punctuation Delays (applied AFTER the character is shown)")]
        [SerializeField, Min(0f)] private float _delayComma   = 0.12f;
        [SerializeField, Min(0f)] private float _delayPeriod  = 0.22f;
        [SerializeField, Min(0f)] private float _delayColon   = 0.10f;
        [SerializeField, Min(0f)] private float _delayBracket = 0.06f;
        [SerializeField, Min(0f)] private float _delayNewline = 0.18f;

        [Header("Fade Curve")]
        [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // ── runtime ────────────────────────────────────────────────────────────────
        private TMP_Text _text;

        private bool  _isPlaying;
        private int   _totalChars;
        private int   _revealedChars;
        private float _timeAccum;

        // Флаг: TMP пересобрал меш → нужно восстановить alpha из кэша
        private bool _meshDirty;

        private readonly List<FadeJob> _activeFades = new(32);
        private bool _needsVertexUpload;

        // Кэш alpha: единственный источник истины о текущей прозрачности каждого символа.
        // Нужен для восстановления после того как TMP сбросит colors32.
        private byte[] _alphaCache = System.Array.Empty<byte>();

        private struct FadeJob
        {
            public int   charIndex;
            public float startTime; // unscaledTime
        }

        // ── events ─────────────────────────────────────────────────────────────────
        public event System.Action      OnComplete;
        public event System.Action<int> OnCharacterRevealed;

        public bool IsPlaying => _isPlaying;

        // ── lifecycle ──────────────────────────────────────────────────────────────

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTMPMeshRebuilt);
            HardHide();
        }

        private void OnDestroy()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTMPMeshRebuilt);
        }

        private void OnEnable()
        {
            if (!_isPlaying)
                HardHide();
        }

        // Вызывается TMP после каждой пересборки меша (смена текста, layout, maxVisibleCharacters и т.д.)
        private void OnTMPMeshRebuilt(Object obj)
        {
            if (!ReferenceEquals(obj, _text)) return;
            // Меш пересобран — colors32 сброшен. Восстановим в Update.
            _meshDirty = true;
        }

        private void Update()
        {
            // Восстанавливаем alpha сразу, до любой другой логики
            if (_meshDirty)
            {
                _meshDirty = false;
                RestoreAlphaFromCache();
                _needsVertexUpload = true;
            }

            if (!_isPlaying && _activeFades.Count == 0)
            {
                if (_needsVertexUpload)
                {
                    _needsVertexUpload = false;
                    _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                }
                return;
            }

            if (_isPlaying)
                TickReveal();

            if (_activeFades.Count > 0)
                TickFades();

            if (_needsVertexUpload)
            {
                _needsVertexUpload = false;
                _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Задать текст и запустить печать.</summary>
        public void PlayText(string text, System.Action onComplete = null)
        {
            Stop();
            if (onComplete != null) OnComplete += onComplete;
            _text.SetText(text ?? string.Empty);
            StartTyping();
        }

        /// <summary>Запустить печать текста, уже стоящего в компоненте.</summary>
        public void Play(System.Action onComplete = null)
        {
            Stop();
            if (onComplete != null) OnComplete += onComplete;
            StartTyping();
        }

        /// <summary>Мгновенно показать весь текст.</summary>
        public void Skip()
        {
            bool wasActive = _isPlaying || _revealedChars < _totalChars;
            StopInternal();

            _text.ForceMeshUpdate();
            SetAllAlphaCache(255);
            RestoreAlphaFromCache();
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            _meshDirty = false;

            if (wasActive) FireComplete();
        }

        /// <summary>Остановить без вызова OnComplete.</summary>
        public void Stop()
        {
            StopInternal();
            OnComplete = null;
        }

        // ── Private ────────────────────────────────────────────────────────────────

        private void StopInternal()
        {
            _isPlaying = false;
            _activeFades.Clear();
            _needsVertexUpload = false;
            _meshDirty = false;
        }

        private void StartTyping()
        {
            _text.ForceMeshUpdate();

            _totalChars    = _text.textInfo.characterCount;
            _revealedChars = 0;
            _timeAccum     = 0f;

            EnsureAlphaCache(_totalChars);
            SetAllAlphaCache(0);

            // Ключевое: maxVisibleCharacters больше не трогаем никогда во время печати.
            // Видимость = только vertex alpha.
            _text.maxVisibleCharacters = int.MaxValue;

            // После SetText+ForceMeshUpdate TMP уже сбросил colors32.
            // _meshDirty сработает, но мы хотим гарантированно применить 0 прямо сейчас.
            RestoreAlphaFromCache();
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            _meshDirty = false;

            _isPlaying = (_totalChars > 0);
            if (!_isPlaying) FireComplete();
        }

        private void TickReveal()
        {
            _timeAccum += Time.unscaledDeltaTime;

            while (_timeAccum >= 0f && _revealedChars < _totalChars)
            {
                int idx = _revealedChars;
                RevealChar(idx);
                OnCharacterRevealed?.Invoke(idx);

                char  c         = GetCharAt(idx);
                float postDelay = GetPunctuationDelay(c);
                _timeAccum -= (_secPerChar + postDelay);
            }

            if (_revealedChars >= _totalChars)
            {
                _isPlaying = false;
                if (_activeFades.Count == 0)
                    Finalize();
            }
        }

        private void RevealChar(int index)
        {
            _revealedChars = index + 1;

            if (_fadeTime <= 0.0001f)
            {
                WriteCharAlpha(index, 255);
            }
            else
            {
                WriteCharAlpha(index, 0);
                _activeFades.Add(new FadeJob
                {
                    charIndex = index,
                    startTime = Time.unscaledTime
                });
            }

            _needsVertexUpload = true;
        }

        private void TickFades()
        {
            float now     = Time.unscaledTime;
            bool  changed = false;

            for (int i = _activeFades.Count - 1; i >= 0; i--)
            {
                var   job = _activeFades[i];
                float t   = Mathf.Clamp01((now - job.startTime) / _fadeTime);
                byte  a   = (byte)(_fadeCurve.Evaluate(t) * 255f);

                WriteCharAlpha(job.charIndex, a);
                changed = true;

                if (t >= 1f)
                {
                    WriteCharAlpha(job.charIndex, 255);
                    _activeFades.RemoveAt(i);
                }
            }

            if (changed) _needsVertexUpload = true;

            if (!_isPlaying && _activeFades.Count == 0)
                Finalize();
        }

        private void Finalize()
        {
            SetAllAlphaCache(255);
            RestoreAlphaFromCache();
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            FireComplete();
        }

        private void HardHide()
        {
            if (_text == null) return;
            _text.ForceMeshUpdate();
            _totalChars = _text.textInfo.characterCount;
            EnsureAlphaCache(_totalChars);
            SetAllAlphaCache(0);
            _text.maxVisibleCharacters = int.MaxValue;
            RestoreAlphaFromCache();
            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            _meshDirty = false;
        }

        // ── Alpha cache ────────────────────────────────────────────────────────────

        private void EnsureAlphaCache(int size)
        {
            if (_alphaCache.Length < size)
                _alphaCache = new byte[size];
        }

        private void SetAllAlphaCache(byte alpha)
        {
            for (int i = 0; i < _totalChars; i++)
                _alphaCache[i] = alpha;
        }

        /// <summary>Записать alpha в кэш + в vertex data меша.</summary>
        private void WriteCharAlpha(int charIndex, byte alpha)
        {
            if ((uint)charIndex < (uint)_alphaCache.Length)
                _alphaCache[charIndex] = alpha;

            ApplyCharAlphaToMesh(charIndex, alpha);
        }

        /// <summary>Применить весь кэш к мешу (после сброса TMP).</summary>
        private void RestoreAlphaFromCache()
        {
            var info  = _text.textInfo;
            int count = Mathf.Min(_totalChars, info.characterCount);
            for (int i = 0; i < count; i++)
                ApplyCharAlphaToMesh(i, _alphaCache[i]);
        }

        private void ApplyCharAlphaToMesh(int charIndex, byte alpha)
        {
            var info = _text.textInfo;
            if ((uint)charIndex >= (uint)info.characterCount) return;

            ref var charInfo = ref info.characterInfo[charIndex];
            if (!charInfo.isVisible) return;

            int meshIndex   = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            var colors = info.meshInfo[meshIndex].colors32;
            if (colors == null || vertexIndex + 3 >= colors.Length) return;

            colors[vertexIndex    ].a = alpha;
            colors[vertexIndex + 1].a = alpha;
            colors[vertexIndex + 2].a = alpha;
            colors[vertexIndex + 3].a = alpha;
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private char GetCharAt(int index)
        {
            var info = _text.textInfo;
            if ((uint)index >= (uint)info.characterCount) return '\0';
            return info.characterInfo[index].character;
        }

        private float GetPunctuationDelay(char c) => c switch
        {
            '.' or '!' or '?' or '…' => _delayPeriod,
            ',' or ';'               => _delayComma,
            ':'                      => _delayColon,
            '(' or ')' or '[' or ']' or '{' or '}' => _delayBracket,
            '\n'                     => _delayNewline,
            _                        => 0f
        };

        private void FireComplete()
        {
            var cb = OnComplete;
            OnComplete = null;
            cb?.Invoke();
        }
    }
}