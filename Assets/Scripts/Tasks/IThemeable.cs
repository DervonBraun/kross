namespace Tasks
{
    /// <summary>
    /// Компонент поддерживает внешнюю инъекцию темы TaskHUD.
    /// TaskHudThemeAdapter ищет этот интерфейс и передаёт SO-тему.
    /// </summary>
    public interface IThemeable
    {
        void SetTheme(TaskHudTheme theme);
    }
}