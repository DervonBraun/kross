using UI.Flow.Inventory;
using UnityEngine;

namespace UI.Flow
{
    /// <summary>
    /// Регистрирует все сервисы в ServiceLocator WebUIManager'а.
    /// Добавь на тот же GameObject что и WebUIManager.
    /// Script Execution Order: -90 (после WebUIManager = -100).
    /// </summary>
    public sealed class WebUIManagerBootstrap : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private WebUIManager         _webUIManager;
        [SerializeField] private Player.PlayerContext _playerContext;

        [Header("Idle Timer")]
        [SerializeField] private bool _enableIdleTimer = true;

        private IdleTimerService _idleTimer;

        private void Awake()
        {
            if (_webUIManager == null)
                _webUIManager = FindAnyObjectByType<WebUIManager>();

            if (_webUIManager == null)
            {
                Debug.LogError("[WebUIManagerBootstrap] WebUIManager not found!");
                return;
            }

            var locator = _webUIManager.GetServiceLocator();

            locator.Register(_webUIManager);

            if (_playerContext != null)
                locator.Register(_playerContext);
            else
                Debug.LogWarning("[WebUIManagerBootstrap] PlayerContext not assigned.");

            if (_enableIdleTimer)
            {
                _idleTimer = new IdleTimerService();
                locator.Register(_idleTimer);

                _webUIManager.PageOpened += _idleTimer.OnPageOpened;
                _webUIManager.PageClosed += _idleTimer.OnPageClosed;
            }
        }

        private void OnDestroy()
        {
            if (_webUIManager != null && _idleTimer != null)
            {
                _webUIManager.PageOpened -= _idleTimer.OnPageOpened;
                _webUIManager.PageClosed -= _idleTimer.OnPageClosed;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_webUIManager == null) _webUIManager = GetComponent<WebUIManager>();
            if (_playerContext == null) _playerContext = FindAnyObjectByType<Player.PlayerContext>();
        }
#endif
    }
}