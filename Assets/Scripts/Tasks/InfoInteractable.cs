using Player;
using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// Простой информационный объект — ящик, терминал, табличка, что угодно.
    /// Наводишься прицелом → TaskHUD показывает заголовок и описание.
    /// Никакой логики выполнения — только отображение.
    ///
    /// Тему можно задать прямо здесь или снаружи через TaskHudThemeAdapter.
    /// </summary>
    public sealed class InfoInteractable : MonoBehaviour,
        IInteractableAimHover,
        IInteractableAimExit,
        IThemeable,
        IInteractableAim
    {
        [Header("Content")]
        [SerializeField] private string _title       = "Объект";
        [SerializeField, TextArea(2, 5)]
        private string _description = "Краткое описание.";

        [Header("Theme (optional — или через TaskHudThemeAdapter)")]
        [SerializeField] private TaskHudTheme _theme;

        // IThemeable
        public void SetTheme(TaskHudTheme theme) => _theme = theme;

        public void OnAimEnter(PlayerContext context)
            => TaskHUD.Instance?.Show(_title, _description, _theme);

        public void OnAimExit()
            => TaskHUD.Instance?.Hide();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_title))
                _title = gameObject.name;
        }
#endif
        public bool CanInteractAim(PlayerContext context)
        {
            return true;
        }

        public void InteractAim(PlayerContext context)
        {
            return;
        }
    }
}