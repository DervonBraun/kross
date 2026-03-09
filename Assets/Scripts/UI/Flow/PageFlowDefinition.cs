using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// ScriptableObject-ассет, описывающий сценарий открытия и закрытия страницы.
    ///
    /// Шаги верхнего уровня — отдельные SO-ассеты (переиспользуемые между flow-ами).
    /// ConditionalStep / GroupStep хранят вложенные шаги через [SerializeReference] внутри.
    ///
    /// Структура каталогов:
    ///   Assets/UI/Flows/{Page}/          ← PageFlowDefinition SO
    ///   Assets/UI/Flows/{Page}/Steps/    ← step-ассеты этой страницы
    ///   Assets/UI/Flows/Shared/          ← LockInputStep, UnlockInputStep и т.д.
    /// </summary>
    [CreateAssetMenu(menuName = "UI/Page Flow Definition")]
    public class PageFlowDefinition : ScriptableObject
    {
        [Header("Identity")]
        public PageId PageId;

        [Header("Conflict")]
        public NavigationPolicy ConflictPolicy = NavigationPolicy.Reject;

        [Header("Priority (used for pending slot replacement)")]
        [Min(0)] public int Priority = 0;

        [Header("Open Steps")]
        public FlowStepAsset[] OpenSteps;

        [Header("Close Steps")]
        public FlowStepAsset[] CloseSteps;

        /// <summary>
        /// Возвращает массив шагов для текущей фазы из PageContext.
        /// </summary>
        public FlowStepAsset[] StepsForPhase(FlowPhase phase) => phase switch
        {
            FlowPhase.Opening => OpenSteps,
            FlowPhase.Closing => CloseSteps,
            _                 => null,
        };
    }
}
