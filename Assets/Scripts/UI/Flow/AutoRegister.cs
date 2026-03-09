using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Добавь на любой MonoBehaviour реализующий IPageView если не хочешь
    /// наследоваться от PageViewBase. Регистрация через WebUIManager.ScanAndRegisterViews —
    /// этот компонент просто маркер для сканирования.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoRegisterPageView : MonoBehaviour
    {
        // Регистрация происходит в WebUIManager.Start через ScanAndRegisterViews.
        // Этот компонент оставлен для обратной совместимости и как явный маркер в иерархии.
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Регистрирует PageFlowDefinition в WebUIManager при Awake.
    /// Удобно для страниц, которые добавляются в сцену динамически.
    /// </summary>
    public sealed class AutoRegisterFlowDefinition : MonoBehaviour
    {
        [SerializeField] private PageFlowDefinition _definition;

        private void Awake()
        {
            if (_definition == null)
            {
                Debug.LogError($"[AutoRegisterFlowDefinition] Definition not set on '{gameObject.name}'.");
                return;
            }

            var mgr = FindAnyObjectByType<WebUIManager>();
            if (mgr == null)
            {
                Debug.LogError("[AutoRegisterFlowDefinition] WebUIManager not found.");
                return;
            }

            mgr.Register(_definition);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Базовый MonoBehaviour для IPageView.
    /// Наследуй, укажи PageId в Inspector, переопредели Show/Hide.
    ///
    /// Регистрация в WebUIManager происходит автоматически через
    /// WebUIManager.Start → ScanAndRegisterViews(). Никакого FindAnyObjectByType здесь нет.
    /// </summary>
    public abstract class PageViewBase : MonoBehaviour, IPageView
    {
        [Header("Page View")]
        [SerializeField] private PageId _viewId;

        public PageId ViewId => _viewId;

        protected virtual void Awake()
        {
            HideInstant();
        }

        public virtual void ShowInstant() => gameObject.SetActive(true);
        public virtual void HideInstant() => gameObject.SetActive(false);
    }
}