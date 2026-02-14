using System;
using EffectSystem;
using Player.EffectSystem;
using Tasks;
using UnityEngine;
// твой EffectManager namespace
// если EffectDefinition лежит тут (как у тебя)

namespace AN_
{
    public sealed class ANService : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameState _state;
        [SerializeField] private EffectManager _effects;
        [SerializeField] private NotificationBus _notify;

        [Header("Requests (SO)")]
        [Tooltip("Запрос, который покупает/генерирует коды за токены.")]
        [SerializeField] private ANRequestDef _generateCodeRequest;

        [Tooltip("Запрос, который генерирует 'сообщение' и выдает Blue токены.")]
        [SerializeField] private ANRequestDef _blueMessageRequest;

        // События на будущее UI (виджеты/нотификации/лог)
        public event Action<ANRequestDef> RequestSucceeded;
        public event Action<ANRequestDef, string> RequestFailed;

        #region UI entrypoints (Unity Button friendly)
        public void UI_GenerateCode()
        {
            MakeRequest(_generateCodeRequest);
        }

        public void UI_GenerateBlueMessage()
        {
            MakeRequest(_blueMessageRequest);
        }
        #endregion

        /// <summary>
        /// Универсальный запуск запроса. В MVP ты дергаешь его двумя кнопками.
        /// </summary>
        public bool MakeRequest(ANRequestDef def)
        {
            if (def == null) return Fail(def, "ANRequestDef is null");
            if (_state == null) return Fail(def, "GameState missing");

            // requirements (если ты используешь их уже сейчас)
            if (!RequirementEvaluator.AreAllMet(def.requirements, _state))
                return Fail(def, $"Requirements not met: {SafeId(def)}");

            // cost
            if (!TokenService.TryPay(_state, def.cost))
                return Fail(def, $"Not enough tokens: {SafeId(def)} cost={def.cost}");

            // Выполнение по типу запроса
            switch (def.type)
            {
                case ANRequestType.GenerateCode:
                    ApplyGenerateCode(def);
                    break;

                case ANRequestType.AnalyzeItem:
                    ApplyBlueMessage(def);
                    break;

                default:
                    // Если у тебя нет enum или тип другой, можно просто трактовать как “сообщение”
                    ApplyBlueMessage(def);
                    break;
            }

            RequestSucceeded?.Invoke(def);
            return true;
        }
        public bool SubmitScannedItem(ScannableItemDef item)
        {
            if (item == null) return Fail(null, "ScannableItemDef is null");
            if (_state == null) return Fail(null, "GameState missing");

            if (string.IsNullOrWhiteSpace(item.id))
                return Fail(null, "ScannableItemDef has empty id");

            var added = _state.MarkItemScanned(item.id);
            if (!added)
            {
                _notify?.Push(NotifyType.Info, "LensUI", $"Уже отсканировано: {item.displayName}", item.icon);
                return false;
            }

            _notify?.Push(NotifyType.AN, "LensUI", $"Файл добавлен: {item.displayName}", item.icon);
            return true;
        }


        private void ApplyGenerateCode(ANRequestDef def)
        {
            // Награда: коды как ресурс (инвентарь)
            if (def.normalCodeReward > 0)
                _state.AddNormalCodes(def.normalCodeReward);

            if (def.resetCodeReward > 0)
                _state.AddResetCodes(def.resetCodeReward);

            // (опционально) лор/эффекты
            _state.TryUnlockLore(def.loreId);
            GrantEffects(def.grantEffects);

            _notify?.Push(
                NotifyType.AN,
                "AN+",
                $"Код получен. Normal +{def.normalCodeReward}, Reset +{def.resetCodeReward}. " +
                $"Codes: N={_state.NormalCodes} R={_state.ResetCodes}"
            );
        }

        private void ApplyBlueMessage(ANRequestDef def)
        {
            // Награда: Blue токены
            if (def.blueReward > 0)
            {
                var add = TokenAmount.Zero;
                add.blue = def.blueReward;
                _state.AddTokens(add);
            }

            // (опционально) лор/эффекты
            _state.TryUnlockLore(def.loreId);
            GrantEffects(def.grantEffects);

            _notify?.Push(
                NotifyType.Reward,
                "AN+ сообщение",
                $"+{def.blueReward} Blue. Wallet: {_state.Wallet}"
            );
        }

        private void GrantEffects(EffectDefinition[] list)
        {
            if (_effects == null || list == null) return;

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null) continue;
                _effects.Add(list[i]);
            }
        }

        private bool Fail(ANRequestDef def, string reason)
        {
            RequestFailed?.Invoke(def, reason);
            _notify?.Push(NotifyType.Error, "AN+ ошибка", reason);
            return false;
        }

        private static string SafeId(ANRequestDef def)
            => def != null && !string.IsNullOrWhiteSpace(def.id) ? def.id : "<no-id>";

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_state == null) _state = GetComponent<GameState>();
            if (_effects == null) _effects = GetComponentInChildren<EffectManager>();
        }
#endif
    }
}
