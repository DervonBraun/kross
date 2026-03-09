using System;
using System.Collections.Generic;
using EffectSystem;
using UnityEngine;
// твой EffectManager namespace

namespace Tasks
{
    public sealed class GameState : MonoBehaviour
    {
        [Header("Tokens")]
        [SerializeField] private TokenAmount _wallet;

        [Header("Codes (inventory)")]
        [SerializeField, Min(0)] private int _normalCodes;
        [SerializeField, Min(0)] private int _resetCodes;

        [Header("World State (optional now)")]
        [SerializeField, Min(0)] private int _accessLevel = 0;
        [SerializeField, Min(0)] private int _oktsStage = 0;

        [Header("Refs")]
        [SerializeField] private EffectManager _effects;

        // Для дебага/простоты: лор можно хранить set'ом.
        private readonly HashSet<string> _unlockedLoreIds = new();
        private readonly HashSet<string> _scannedItemIds = new();
        private readonly HashSet<string> _analyzedItemIds = new();

        public event Action<string> ItemScanned;
        public event Action<string> ItemAnalyzed;

        public TokenAmount Wallet => _wallet;

        public int NormalCodes => _normalCodes;
        public int ResetCodes => _resetCodes;

        public int AccessLevel => _accessLevel;
        public int OktsStage => _oktsStage;

        #region Tokens
        public void AddTokens(in TokenAmount add) => _wallet.Add(add);
        public bool TryPayTokens(in TokenAmount cost) => _wallet.TryPay(cost);
        public bool CanPayTokens(in TokenAmount cost) => _wallet.CanPay(cost);
        #endregion

        #region Codes
        public void AddNormalCodes(int amount)
        {
            if (amount <= 0) return;
            _normalCodes = Mathf.Max(0, _normalCodes + amount);
        }

        public void AddResetCodes(int amount)
        {
            if (amount <= 0) return;
            _resetCodes = Mathf.Max(0, _resetCodes + amount);
        }

        public bool TryConsumeNormalCode(int amount = 1)
        {
            if (amount <= 0) return true;
            if (_normalCodes < amount) return false;
            _normalCodes -= amount;
            return true;
        }

        public bool TryConsumeResetCode(int amount = 1)
        {
            if (amount <= 0) return true;
            if (_resetCodes < amount) return false;
            _resetCodes -= amount;
            return true;
        }
        #endregion

        #region Effects (requirements)
        public bool HasEffectId(string effectId)
            => _effects != null && _effects.Has(effectId);

        public bool HasEffectTag(string tag)
            => _effects != null && _effects.HasTag(tag);
        #endregion

        #region Lore (optional)
        public bool TryUnlockLore(string loreId)
        {
            if (string.IsNullOrWhiteSpace(loreId)) return false;
            return _unlockedLoreIds.Add(loreId);
        }

        public bool HasLore(string loreId)
            => !string.IsNullOrWhiteSpace(loreId) && _unlockedLoreIds.Contains(loreId);
        #endregion

        #region Scanned Items (AN+)
        public int ScannedItemCount => _scannedItemIds.Count;

        public bool IsItemScanned(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            return _scannedItemIds.Contains(itemId);
        }

        /// <summary>
        /// Отмечает предмет как отсканированный. Возвращает true, если добавлен впервые.
        /// </summary>
        public bool MarkItemScanned(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (!_scannedItemIds.Add(itemId)) return false;
            ItemScanned?.Invoke(itemId);
            return true;
        }

        public bool IsItemAnalyzed(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            return _analyzedItemIds.Contains(itemId);
        }

        /// <summary>
        /// Отмечает предмет как проанализированный. Возвращает true, если добавлен впервые.
        /// </summary>
        public bool MarkItemAnalyzed(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (!_analyzedItemIds.Add(itemId)) return false;
            ItemAnalyzed?.Invoke(itemId);
            return true;
        }

        /// <summary>
        /// MVP: "потратить" один любой сканнутый файл на анализ.
        /// Возвращает id потраченного файла.
        /// </summary>
        public bool TryConsumeOneScannedItem(out string consumedItemId)
        {
            consumedItemId = null;

            // HashSet не даёт "взять первый" напрямую, поэтому пробежка.
            foreach (var id in _scannedItemIds)
            {
                consumedItemId = id;
                break;
            }

            if (consumedItemId == null) return false;

            _scannedItemIds.Remove(consumedItemId);
            return true;
        }
        #endregion

        
        #region OKTS Stage

        /// <summary>
        /// Повышает стадию ОКТС на 1. Вызывается из CycleManager.
        /// </summary>
        public void IncrementOktsStage()
        {
            _oktsStage = Mathf.Min(_oktsStage + 1, int.MaxValue);
        }

        /// <summary>
        /// Сбрасывает стадию ОКТС (например, при рестарте / Game Over).
        /// </summary>
        public void ResetOktsStage()
        {
            _oktsStage = 0;
        }

        #endregion
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_effects == null) _effects = GetComponentInChildren<EffectManager>();
        }
#endif
    }
}
