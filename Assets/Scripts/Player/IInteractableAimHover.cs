namespace Player
{
    /// <summary>
    /// Реагирует на наведение/уход прицела — без нажатия кнопки.
    /// Используется для отображения информационного UI (метки, подсказки).
    ///
    /// Отличие от IInteractableAim:
    ///   IInteractableAimHover — пассивный, срабатывает при наведении
    ///   IInteractableAim      — активный,  срабатывает по нажатию кнопки
    /// </summary>
    public interface IInteractableAimHover
    {
        void OnAimEnter(PlayerContext context);
    }
}