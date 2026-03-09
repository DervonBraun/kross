using UnityEngine;

namespace Level
{
    public enum DoorMotionMode
    {
        Rotate,
        Slide,
        RotateAndSlide
    }

    [CreateAssetMenu(menuName = "KROSS/Interactions/Door Settings", fileName = "DoorSettings")]
    public sealed class DoorSettingsSO : ScriptableObject
    {
        [Header("Motion")]
        public DoorMotionMode motionMode = DoorMotionMode.Rotate;

        [Tooltip("Сколько секунд занимает открытие (до 1.0 по кривой).")]
        [Min(0.01f)] public float openDuration = 0.6f;

        [Tooltip("Сколько секунд занимает закрытие (до 1.0 по кривой).")]
        [Min(0.01f)] public float closeDuration = 0.5f;

        [Tooltip("Кривая прогресса открытия (t:0..1 -> value:0..1).")]
        public AnimationCurve openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("Кривая прогресса закрытия (t:0..1 -> value:0..1).")]
        public AnimationCurve closeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Rotate")]
        [Tooltip("Локальная ось вращения (например (0,1,0) для Y).")]
        public Vector3 localRotateAxis = Vector3.up;

        [Tooltip("Угол открытия в градусах (положительный/отрицательный).")]
        public float openAngle = 90f;

        [Header("Slide")]
        [Tooltip("Смещение в локальных координатах в открытом состоянии.")]
        public Vector3 openLocalPositionOffset = Vector3.zero;

        [Header("Auto Close")]
        public bool autoCloseEnabled = true;

        [Tooltip("Через сколько секунд после открытия закрыть дверь автоматически.")]
        [Min(0f)] public float autoCloseDelay = 2.5f;

        [Header("Random Open (creepy mode)")]
        public bool randomOpenEnabled = false;

        [Tooltip("Интервал проверок (сек). На каждом тике может случайно открыться, если дверь закрыта.")]
        [Min(0.05f)] public float randomCheckInterval = 5f;

        [Tooltip("Вероятность открытия на один тик (0..1). Пример: 0.05 = 5% каждый интервал.")]
        [Range(0f, 1f)] public float randomOpenChance = 0.05f;
    }
}