using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// Универсальный адаптер темы для TaskHUD.
    ///
    /// Повесь на любой GameObject, брось SO-тему — готово.
    /// Ищет все компоненты, реализующие IThemeable (в т.ч. на дочерних объектах),
    /// и вызывает SetTheme() при старте и при смене темы через SetTheme() в рантайме.
    ///
    /// Совместимо с: GreenNodePoint, GreenTokenHub, TaskInteractable — и любым
    /// компонентом, который реализует Tasks.Green.IThemeable.
    /// </summary>
    public sealed class TaskHudThemeAdapter : MonoBehaviour
    {
        [SerializeField] private TaskHudTheme _theme;

        [Tooltip("Искать IThemeable на дочерних объектах тоже?")]
        [SerializeField] private bool _includeChildren = true;

        private IThemeable[] _targets;

        private void Awake() => Apply();

        /// <summary>Сменить тему в рантайме (например при смене фазы цикла).</summary>
        public void SetTheme(TaskHudTheme theme)
        {
            _theme = theme;
            Apply();
        }

        private void Apply()
        {
            _targets = _includeChildren
                ? GetComponentsInChildren<IThemeable>()
                : GetComponents<IThemeable>();

            if (_theme == null) return;

            foreach (var t in _targets)
                t.SetTheme(_theme);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Предпросмотр в редакторе: применяем сразу при изменении поля
            if (!Application.isPlaying) return;
            Apply();
        }
#endif
    }
}