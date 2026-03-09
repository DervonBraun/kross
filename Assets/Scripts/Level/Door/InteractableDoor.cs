using System;
using System.Collections.Generic;
using Player;
using UnityEngine;

namespace Level
{
    public enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    public sealed class InteractableDoor : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private DoorSettingsSO _settings;

        [Tooltip("Что реально двигаем (меш/корень двери). Если null, будет this.transform.")]
        [SerializeField] private Transform _doorRoot;

        [Header("Buttons (optional whitelist)")]
        [Tooltip("Если список НЕ пустой: дверь реагирует только на эти кнопки.")]
        [SerializeField] private List<DoorButtonInteractable> _allowedButtons = new();

        [Header("Effect Requirements")]
        [Tooltip("Если задан — дверь проверяет эффекты игрока перед открытием.\n" +
                 "Null = дверь открывается для всех.")]
        [SerializeField] private DoorEffectRequirementSO _effectRequirement;

        [Header("Proximity")]
        [Tooltip("Задержка закрытия после того, как игрок покинул зону (сек).")]
        [SerializeField] private float _proximityCloseDelay = 1f;

        [Header("Debug")]
        [SerializeField] private bool _logState;

        // ── Public state ──────────────────────────────────────────────────────
        public DoorState State { get; private set; } = DoorState.Closed;
        public bool IsOpen   => State == DoorState.Open;
        public bool IsMoving => State == DoorState.Opening || State == DoorState.Closing;

        /// <summary>Срабатывает при любом изменении State.</summary>
        public event Action<DoorState> OnStateChanged;

        /// <summary>
        /// Игрок попытался открыть дверь, но не прошёл проверку эффектов.
        /// Подпишись для UI-фидбека (мигание иконки, звук отказа и т.д.).
        /// </summary>
        public event Action<PlayerContext> OnAccessDenied;

        // ── Motion ───────────────────────────────────────────────────────────
        private Quaternion _closedLocalRot;
        private Quaternion _openLocalRot;
        private Vector3    _closedLocalPos;
        private Vector3    _openLocalPos;

        private float _moveT;
        private float _moveTime;

        // ── Timers ───────────────────────────────────────────────────────────
        private float _autoCloseAt       = -1f;
        private float _nextRandomCheckAt  = -1f;
        private float _proximityCloseAt   = -1f;

        // ── Proximity ────────────────────────────────────────────────────────
        private int _playersInZone;

        // ─────────────────────────────────────────────────────────────────────
        #region Unity lifecycle

        private void Reset()
        {
            _doorRoot = transform;
        }

        private void Awake()
        {
            if (_doorRoot == null) _doorRoot = transform;

            CacheClosedPose();
            RebuildOpenPose();
            ApplyPose(0f);

            ScheduleRandomCheck();
        }

        private void OnValidate()
        {
            if (_doorRoot == null) _doorRoot = transform;
        }

        private void Update()
        {
            TickMotion();
            TickAutoClose();
            TickRandomOpen();
            TickProximityClose();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Proximity (вызывается из ProximityTriggerRelay)

        internal void OnProximityEnter(PlayerContext player)
        {
            _playersInZone++;
            _proximityCloseAt = -1f;

            if (_logState)
                Debug.Log($"[Door] {name} proximity enter (in zone: {_playersInZone})");

            TryOpenForPlayer(player);
        }

        internal void OnProximityExit(PlayerContext player)
        {
            _playersInZone = Mathf.Max(0, _playersInZone - 1);

            if (_logState)
                Debug.Log($"[Door] {name} proximity exit (in zone: {_playersInZone})");

            if (_playersInZone > 0) return;

            _proximityCloseAt = Time.time + Mathf.Max(0f, _proximityCloseDelay);
        }

        private void TickProximityClose()
        {
            if (_proximityCloseAt < 0f) return;
            if (Time.time < _proximityCloseAt) return;

            _proximityCloseAt = -1f;
            Close();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Public API

        public void ToggleFromButton(DoorButtonInteractable button, PlayerContext player)
        {
            if (!IsButtonAllowed(button)) return;

            if (State == DoorState.Closed || State == DoorState.Closing)
                TryOpenForPlayer(player); // проверка эффектов здесь
            else if (State == DoorState.Open || State == DoorState.Opening)
                Close();
        }

        /// <summary>
        /// Попытка открыть дверь с проверкой эффектов игрока.
        /// Используется proximity-системой. Можно вызвать вручную из других систем.
        /// </summary>
        public bool TryOpenForPlayer(PlayerContext player)
        {
            if (!CheckEffectRequirement(player))
            {
                if (_logState)
                    Debug.Log($"[Door] {name} access denied for '{player?.name}'");

                OnAccessDenied?.Invoke(player);
                return false;
            }

            Open();
            return true;
        }

        public void Open()
        {
            if (_settings == null) return;
            if (State == DoorState.Open || State == DoorState.Opening) return;

            BeginMove(opening: true);
        }

        public void Close()
        {
            if (_settings == null) return;
            if (State == DoorState.Closed || State == DoorState.Closing) return;

            BeginMove(opening: false);
        }

        public void ForceSetOpen(bool open)
        {
            if (_settings == null) return;

            _moveTime = 0f;
            _moveT    = open ? 1f : 0f;
            State     = open ? DoorState.Open : DoorState.Closed;

            ApplyPose(_moveT);

            _autoCloseAt      = -1f;
            _proximityCloseAt  = -1f;

            if (open) ArmAutoClose();

            NotifyState();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Effect requirement

        private bool CheckEffectRequirement(PlayerContext player)
        {
            if (_effectRequirement == null) return true;
            if (player == null) return false;

            var manager = player.EffectManager;
            if (manager == null)
            {
                Debug.LogWarning($"[Door] {name}: PlayerContext '{player.name}' has no EffectManager.");
                return false;
            }

            return _effectRequirement.IsSatisfied(manager);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Private — motion

        private void CacheClosedPose()
        {
            _closedLocalRot = _doorRoot.localRotation;
            _closedLocalPos = _doorRoot.localPosition;
        }

        private void RebuildOpenPose()
        {
            if (_settings == null) return;

            var axis = _settings.localRotateAxis.sqrMagnitude < 0.0001f
                ? Vector3.up
                : _settings.localRotateAxis.normalized;

            _openLocalRot = _closedLocalRot * Quaternion.AngleAxis(_settings.openAngle, axis);
            _openLocalPos = _closedLocalPos + _settings.openLocalPositionOffset;
        }

        private void BeginMove(bool opening)
        {
            RebuildOpenPose();

            State = opening ? DoorState.Opening : DoorState.Closing;
            NotifyState();

            _autoCloseAt = -1f;

            // Пересчитываем накопленное время от текущей позиции —
            // нет рывка при смене направления (фикс спама кнопки).
            float duration = opening ? _settings.openDuration : _settings.closeDuration;
            duration = Mathf.Max(0.01f, duration);

            float targetProgress = opening ? _moveT : 1f - _moveT;
            _moveTime = InverseCurve(
                opening ? _settings.openCurve : _settings.closeCurve,
                targetProgress
            ) * duration;

            if (_logState)
                Debug.Log($"[Door] {name} begin {(opening ? "OPEN" : "CLOSE")} at t={_moveT:0.00}");
        }

        private void TickMotion()
        {
            if (_settings == null || !IsMoving) return;

            float duration = State == DoorState.Opening ? _settings.openDuration : _settings.closeDuration;
            duration = Mathf.Max(0.01f, duration);

            _moveTime += Time.deltaTime;
            float t01 = Mathf.Clamp01(_moveTime / duration);

            _moveT = State == DoorState.Opening
                ? EvaluateCurveSafe(_settings.openCurve,  t01)
                : 1f - EvaluateCurveSafe(_settings.closeCurve, t01);

            ApplyPose(_moveT);

            if (t01 < 1f) return;

            if (State == DoorState.Opening)
            {
                State = DoorState.Open;
                ArmAutoClose();
            }
            else
            {
                State = DoorState.Closed;
            }

            NotifyState();

            if (_logState)
                Debug.Log($"[Door] {name} end → {State}");
        }

        private void ApplyPose(float t)
        {
            if (_doorRoot == null || _settings == null) return;

            if (_settings.motionMode is DoorMotionMode.Rotate or DoorMotionMode.RotateAndSlide)
                _doorRoot.localRotation = Quaternion.SlerpUnclamped(_closedLocalRot, _openLocalRot, t);

            if (_settings.motionMode is DoorMotionMode.Slide or DoorMotionMode.RotateAndSlide)
                _doorRoot.localPosition = Vector3.LerpUnclamped(_closedLocalPos, _openLocalPos, t);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Private — timers & misc

        private void ArmAutoClose()
        {
            if (_settings == null || !_settings.autoCloseEnabled) return;
            if (_settings.autoCloseDelay <= 0f) return;

            _autoCloseAt = Time.time + _settings.autoCloseDelay;
        }

        private void TickAutoClose()
        {
            if (_autoCloseAt < 0f || State != DoorState.Open) return;

            if (Time.time >= _autoCloseAt)
            {
                _autoCloseAt = -1f;
                Close();
            }
        }

        private void ScheduleRandomCheck()
        {
            if (_settings == null || !_settings.randomOpenEnabled)
            {
                _nextRandomCheckAt = -1f;
                return;
            }

            _nextRandomCheckAt = Time.time + Mathf.Max(0.05f, _settings.randomCheckInterval);
        }

        private void TickRandomOpen()
        {
            if (_settings == null || !_settings.randomOpenEnabled) return;

            if (_nextRandomCheckAt < 0f) ScheduleRandomCheck();
            if (Time.time < _nextRandomCheckAt) return;

            ScheduleRandomCheck();

            if (State != DoorState.Closed) return;

            float chance = Mathf.Clamp01(_settings.randomOpenChance);
            if (chance > 0f && UnityEngine.Random.value < chance)
                Open();
        }

        private bool IsButtonAllowed(DoorButtonInteractable button)
        {
            if (button == null) return false;
            if (_allowedButtons is not { Count: > 0 }) return true;
            return _allowedButtons.Contains(button);
        }

        private void NotifyState() => OnStateChanged?.Invoke(State);

        private float EvaluateCurveSafe(AnimationCurve curve, float t) =>
            curve is { length: > 0 } ? curve.Evaluate(t) : t;

        /// <summary>
        /// Бинарный поиск обратного значения кривой.
        /// Корректен для монотонных кривых (стандартный случай анимации двери).
        /// </summary>
        private float InverseCurve(AnimationCurve curve, float value, int steps = 16)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < steps; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (EvaluateCurveSafe(curve, mid) < value) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        #endregion
    }
}