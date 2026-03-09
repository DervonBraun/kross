using System.Collections.Generic;
using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// Runtime-реестр heat и бонусов для всех заданий.
    ///
    /// Итоговый мультипликатор награды:
    ///   rewardMultiplier = (1 - heat / maxHeat) * bonusMultiplier
    ///
    /// heat  — растёт при повторных выполнениях, снижает награду
    /// bonus — накапливается от соседних заданий, увеличивает награду, пассивно затухает к 1.0
    /// </summary>
    public sealed class HeatRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, float>          _heat        = new();
        private readonly Dictionary<string, float>          _bonus       = new(); // множитель ≥ 1.0
        private readonly Dictionary<string, int>            _completions = new();
        private readonly Dictionary<string, TaskHeatConfig> _configs     = new();

        // ─── Registration ─────────────────────────────────────────────────────

        public void Register(string taskId, TaskHeatConfig config)
        {
            if (string.IsNullOrWhiteSpace(taskId) || config == null) return;
            _configs[taskId] = config;
            if (!_heat.ContainsKey(taskId))
            {
                _heat[taskId]  = Mathf.Clamp(config.initialHeat, 0f, config.maxHeat);
                _bonus[taskId] = 1f;
            }
            Debug.Log($"[HeatRegistry] Registered '{taskId}' | initialHeat={_heat[taskId]:F2}");
        }

        // ─── Queries ──────────────────────────────────────────────────────────

        public float GetHeat(string taskId)
            => _heat.TryGetValue(taskId, out var h) ? h : 0f;

        public float GetBonus(string taskId)
            => _bonus.TryGetValue(taskId, out var b) ? b : 1f;

        /// <summary>
        /// Итоговый мультипликатор награды:
        ///   (1 - heat/maxHeat) * bonus
        /// Не ограничен сверху — бонус может давать > 100% от base.
        /// </summary>
        public float GetRewardMultiplier(string taskId)
        {
            float max         = _configs.TryGetValue(taskId, out var cfg) ? cfg.maxHeat : 100f;
            float heat        = _heat.TryGetValue(taskId, out var h) ? h : 0f;
            float heatFactor  = 1f - Mathf.Clamp01(heat / max);
            float bonus       = _bonus.TryGetValue(taskId, out var b) ? b : 1f;
            float result      = heatFactor * bonus;

            Debug.Log($"[HeatRegistry] RewardMultiplier '{taskId}' = {result:F3}  " +
                      $"(heatFactor={heatFactor:F3}, bonus={bonus:F3}, heat={heat:F2})");
            return result;
        }

        public bool CanComplete(string taskId)
        {
            if (!_configs.TryGetValue(taskId, out var cfg)) return true;
            if (cfg.maxCompletionsPerSession <= 0) return true;
            _completions.TryGetValue(taskId, out int done);
            return done < cfg.maxCompletionsPerSession;
        }

        // ─── Mutations ────────────────────────────────────────────────────────

        public void OnTaskCompleted(string taskId)
        {
            _completions[taskId] = _completions.GetValueOrDefault(taskId) + 1;

            Debug.Log($"[HeatRegistry] OnTaskCompleted '{taskId}' | has config: {_configs.ContainsKey(taskId)}");

            if (!_configs.TryGetValue(taskId, out var cfg)) return;

            // Нагреть себя
            float heatBefore = GetHeat(taskId);
            WarmUp(taskId, cfg.selfHeatRate, cfg.maxHeat);
            Debug.Log($"[HeatRegistry] WarmUp '{taskId}' | {heatBefore:F2} → {GetHeat(taskId):F2}");

            // Добавить бонус соседям
            foreach (var inf in cfg.influences)
            {
                if (string.IsNullOrWhiteSpace(inf.targetTaskId)) continue;
                if (inf.rewardMultiplier <= 0f) continue;

                float bonusBefore = GetBonus(inf.targetTaskId);
                AddBonus(inf.targetTaskId, inf.rewardMultiplier);
                float bonusAfter = GetBonus(inf.targetTaskId);

                Debug.Log($"[HeatRegistry] Bonus '{taskId}'→'{inf.targetTaskId}' | " +
                          $"rewardMultiplier={inf.rewardMultiplier:F3} | bonus {bonusBefore:F3} → {bonusAfter:F3}");
            }
        }

        public void ResetAll()
        {
            _heat.Clear();
            _bonus.Clear();
            _completions.Clear();
            foreach (var (id, cfg) in _configs)
            {
                _heat[id]  = Mathf.Clamp(cfg.initialHeat, 0f, cfg.maxHeat);
                _bonus[id] = 1f;
            }
        }

        public void ResetTask(string taskId)
        {
            _completions.Remove(taskId);
            float initial = _configs.TryGetValue(taskId, out var cfg) ? cfg.initialHeat : 0f;
            float max     = cfg != null ? cfg.maxHeat : 100f;
            _heat[taskId]  = Mathf.Clamp(initial, 0f, max);
            _bonus[taskId] = 1f;
        }

        // ─── Unity lifecycle ─────────────────────────────────────────────────

        private void Update()
        {
            if (_configs.Count == 0) return;
            float dt = Time.deltaTime;

            foreach (var (id, cfg) in _configs)
            {
                // Пассивное остывание heat → 0
                if (cfg.passiveCooldownRate > 0f && _heat.TryGetValue(id, out float heat) && heat > 0f)
                    _heat[id] = Mathf.Lerp(heat, 0f, cfg.passiveCooldownRate * dt);

                // Пассивное затухание бонуса → 1.0
                if (cfg.passiveCooldownRate > 0f && _bonus.TryGetValue(id, out float bonus) && bonus > 1f)
                    _bonus[id] = Mathf.Lerp(bonus, 1f, cfg.passiveCooldownRate * dt);
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private void WarmUp(string taskId, float rate, float maxHeat)
        {
            if (rate <= 0f) return;
            _heat.TryGetValue(taskId, out float current);
            _heat[taskId] = Mathf.Lerp(current, maxHeat, rate);
        }

        /// <summary>
        /// Умножает текущий бонус задания на rewardMultiplier.
        /// Работает даже если задание не зарегистрировано — бонус хранится независимо.
        /// </summary>
        private void AddBonus(string taskId, float rewardMultiplier)
        {
            _bonus.TryGetValue(taskId, out float current);
            if (current <= 0f) current = 1f;
            _bonus[taskId] = current * rewardMultiplier;
        }
    }
}