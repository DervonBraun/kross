using System;

namespace Player
{
    /// <summary>
    /// Контракт для любого вступления перед открытием инвентаря.
    ///
    /// Примеры реализаций:
    ///   • InventoryIdleIntro  — idle-экран с аватаркой и приветствием
    ///   • BootSequenceIntro   — загрузочная последовательность при первом входе
    ///   • NewsTickerIntro     — бегущая строка с игровыми новостями
    ///
    /// InventoryView спрашивает: ShouldPlay() → если да, вызывает Play(onComplete).
    /// Реализация сама решает когда вызвать onComplete.
    /// </summary>
    public interface IInventoryIntro
    {
        /// <summary>Нужно ли сейчас показывать вступление?</summary>
        bool ShouldPlay();

        /// <summary>
        /// Запускает вступление.
        /// Когда всё готово к открытию инвентаря — вызвать <paramref name="onReady"/>.
        /// </summary>
        void Play(Action onReady);

        /// <summary>
        /// Экстренная остановка (например, игрок закрыл инвентарь во время вступления).
        /// </summary>
        void Cancel();
    }
}