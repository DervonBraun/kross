using System.Collections.Generic;
using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// Сервис накопления зелёных токенов.
    ///
    /// Каждую секунду начисляет: tokensPerSecondPerNode × (количество исправных узлов).
    /// Токены копятся в float-буфере хаба; целые единицы доступны для получения игроком.
    ///
    /// GreenNodePoint-ы регистрируются сами через Register/Unregister (OnEnable/OnDisable).
    /// Можно также добавить ссылки прямо в инспекторе через _staticNodes.
    /// </summary>
    public sealed class GreenTokenAccumulator : MonoBehaviour
    {
        public static GreenTokenAccumulator Instance { get; private set; }

        [Header("Production")]
        [SerializeField, Min(0f)] private float _tokensPerSecondPerNode = 1f;

        [Header("Static nodes (можно оставить пустым — узлы регистрируются сами)")]
        [SerializeField] private GreenNodePoint[] _staticNodes = System.Array.Empty<GreenNodePoint>();

        // ── State ─────────────────────────────────────────────────────────────
        private readonly List<GreenNodePoint> _nodes       = new();
        private float                         _accumulator = 0f;
        private float                         _tickTimer   = 0f;
        private const float                   TickInterval = 1f;

        /// <summary>Накопленные целые зелёные токены, готовые к получению.</summary>
        public int PendingTokens => Mathf.FloorToInt(_accumulator);

        /// <summary>Текущая скорость производства (токен/сек).</summary>
        public float ProductionRate => _tokensPerSecondPerNode * OperationalCount;

        /// <summary>Количество исправных узлов прямо сейчас.</summary>
        public int OperationalCount
        {
            get
            {
                int count = 0;
                foreach (var n in _nodes)
                    if (n != null && n.IsOperational) count++;
                return count;
            }
        }

        public int TotalNodeCount => _nodes.Count;

        // ══════════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            foreach (var n in _staticNodes)
                if (n != null) Register(n);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
            if (_tickTimer < TickInterval) return;

            _tickTimer -= TickInterval;
            float earned = _tokensPerSecondPerNode * OperationalCount;
            _accumulator += earned;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════════════════════

        public void Register(GreenNodePoint node)
        {
            if (node != null && !_nodes.Contains(node))
                _nodes.Add(node);
        }

        public void Unregister(GreenNodePoint node)
        {
            _nodes.Remove(node);
        }

        /// <summary>
        /// Забирает все накопленные целые токены из хаба.
        /// Возвращает количество.
        /// </summary>
        public int Collect()
        {
            int amount = PendingTokens;
            if (amount <= 0) return 0;

            _accumulator -= amount; // дробная часть сохраняется
            return amount;
        }
    }
}