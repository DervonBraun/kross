using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// ScriptableObject-тема для TaskHUD.
    /// Создай нужные пресеты через Create → KROSS → UI → HUD Theme.
    /// </summary>
    [CreateAssetMenu(menuName = "KROSS/UI/HUD Theme")]
    public sealed class TaskHudTheme : ScriptableObject
    {
        [Header("Title Row")]
        public Color titleBgColor      = new(0.08f, 0.08f, 0.08f, 0.92f);
        public Color titleTextColor    = Color.white;

        [Header("Price / Info Row")]
        public Color priceBgColor      = new(0.12f, 0.12f, 0.12f, 0.92f);
        public Color primaryTextColor  = Color.white;

        [Header("Heat / Strike (только для задач с heat)")]
        public Color strikeColor       = new(0.6f, 0.6f, 0.6f, 1f);
        public Color modifiedTextColor = new(1f, 0.85f, 0.2f, 1f);

        // ── Пресеты ────────────────────────────────────────────────────────────
        // Можно сделать статические фабричные методы для быстрого создания в коде.
        // В реальных данных используй Create → ScriptableObject в редакторе.
    }
}