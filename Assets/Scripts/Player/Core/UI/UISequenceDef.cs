using System;
using DG.Tweening;
using UnityEngine;

namespace Player
{
    public enum UIAnimationType
    {
        Fade,
        SlideFromLeft,
        SlideFromRight,
        SlideFromBottom,
        SlideFromTop,
        Scale,
        FadeAndSlideFromLeft,
        FadeAndSlideFromRight,
        FadeAndSlideFromBottom,
        FadeAndSlideFromTop,
    }

    /// <summary>
    /// Параметры одного шага последовательности.
    /// Не содержит ссылок на объекты сцены — только настройки анимации.
    /// Сопоставляется с UISequenceElement по индексу в UISequencePlayer.
    /// </summary>
    [Serializable]
    public sealed class UISequenceStep
    {
        [Tooltip("Задержка перед этим шагом.")]
        [Min(0f)] public float delay = 0f;

        [Tooltip("Длительность анимации появления.")]
        [Min(0.01f)] public float duration = 0.3f;

        [Tooltip("Кривая анимации появления.")]
        public Ease ease = Ease.OutCubic;

        [Tooltip("Тип анимации.")]
        public UIAnimationType animationType = UIAnimationType.FadeAndSlideFromLeft;

        [Tooltip("Смещение в пикселях для slide-анимаций.")]
        [Min(0f)] public float slideOffset = 60f;

        [Tooltip("Ждать завершения этого шага перед следующим.\n" +
                 "false = следующий стартует через delay от начала этого (stagger).")]
        public bool waitForComplete = false;
    }

    /// <summary>
    /// ScriptableObject — набор параметров анимации для UI-последовательности.
    /// Создаётся через: Assets → Create → AN/UI/Sequence Def
    ///
    /// Шаги сопоставляются с элементами UISequencePlayer по индексу:
    ///   steps[0] → UISequencePlayer._elements[0]
    ///   steps[1] → UISequencePlayer._elements[1]
    ///   и т.д.
    ///
    /// Сами объекты сцены назначаются в UISequencePlayer (компонент в сцене),
    /// а этот SO хранит только настройки — его можно переиспользовать
    /// для разных наборов объектов.
    /// </summary>
    [CreateAssetMenu(menuName = "AN/UI/Sequence Def", fileName = "UISequenceDef")]
    public sealed class UISequenceDef : ScriptableObject
    {
        [Tooltip("Параметры шагов. steps[i] применяется к _elements[i] в UISequencePlayer.")]
        public UISequenceStep[] steps = Array.Empty<UISequenceStep>();

        [Header("Hide (применяется ко всем элементам при скрытии)")]
        [Min(0.01f)] public float hideDuration = 0.2f;
        public Ease               hideEase     = Ease.InCubic;
        [Tooltip("Stagger между элементами при скрытии (снизу вверх).")]
        [Min(0f)]    public float hideStagger  = 0.03f;
    }
}