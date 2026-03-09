namespace Player
{
    /// <summary>
    /// Дополнительный интерфейс для объектов, которым нужно знать
    /// когда прицел с них ушёл (скрыть UI, сбросить состояние и т.д.).
    /// Реализуется вместе с IInteractableAim.
    /// </summary>
    public interface IInteractableAimExit
    {
        void OnAimExit();
    }
}