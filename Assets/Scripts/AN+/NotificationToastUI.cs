using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace AN_
{
    public sealed class NotificationToastUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private NotificationBus _bus;
        [SerializeField] private RectTransform _area; // top-right anchored
        [SerializeField] private NotificationToastItem _prefab;

        [Header("Layout")]
        [SerializeField] private float _width      = 360f;
        [SerializeField] private float _spacing    = 8f;
        [SerializeField] private int   _maxVisible = 6;

        [Header("Timing")]
        [SerializeField] private float _lifetime = 4.0f;

        [Header("Reflow (Y)")]
        [SerializeField] private float _reflowDuration = 0.25f;
        [SerializeField] private Ease  _reflowEase     = Ease.OutCubic;
        [SerializeField] private float _reflowStagger  = 0.015f;

        // ── Background colors (единый тёмный фон или по типу — на ваш вкус) ──
        [Header("Background Colors")]
        [SerializeField] private Color _bgInfo    = new(0.12f, 0.12f, 0.12f, 0.88f);
        [SerializeField] private Color _bgWarning = new(0.20f, 0.16f, 0.06f, 0.90f);
        [SerializeField] private Color _bgError   = new(0.22f, 0.06f, 0.06f, 0.92f);
        [SerializeField] private Color _bgReward  = new(0.06f, 0.20f, 0.10f, 0.90f);
        [SerializeField] private Color _bgOkts    = new(0.06f, 0.12f, 0.22f, 0.90f);
        [SerializeField] private Color _bgAn      = new(0.16f, 0.06f, 0.22f, 0.90f);

        // ── TimeBar colors — меняются по типу ────────────────────────────────
        [Header("TimeBar Colors")]
        [SerializeField] private Color _barInfo    = new(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color _barWarning = new(1.00f, 0.75f, 0.10f, 1f);
        [SerializeField] private Color _barError   = new(0.95f, 0.20f, 0.20f, 1f);
        [SerializeField] private Color _barReward  = new(0.20f, 0.90f, 0.40f, 1f);
        [SerializeField] private Color _barOkts    = new(0.20f, 0.55f, 1.00f, 1f);
        [SerializeField] private Color _barAn      = new(0.80f, 0.30f, 1.00f, 1f);

        // ── Per-type icons ────────────────────────────────────────────────────
        [Header("Icons (по типу уведомления)")]
        [SerializeField] private Sprite _iconInfo;
        [SerializeField] private Sprite _iconWarning;
        [SerializeField] private Sprite _iconError;
        [SerializeField] private Sprite _iconReward;
        [SerializeField] private Sprite _iconOkts;
        [SerializeField] private Sprite _iconAn;

        private readonly List<NotificationToastItem> _items    = new();
        private readonly List<NotificationToastItem> _newItems = new();
        private bool _dirty;

        private void Awake()
        {
            if (_bus != null) _bus.Pushed += OnPushed;
        }

        private void OnDestroy()
        {
            if (_bus != null) _bus.Pushed -= OnPushed;
        }

        private void OnPushed(Notification n)
        {
            if (_prefab == null || _area == null) return;

            var item = Instantiate(_prefab, _area);

            var rt = item.Rect;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);

            // NEWEST on top
            _items.Insert(0, item);
            _newItems.Add(item);

            item.SetupContent(
                n,
                _width,
                _lifetime,
                GetBgColor(n.type),
                GetBarColor(n.type),
                GetIcon(n.type),
                RequestRemove);

            // cap
            while (_items.Count > _maxVisible)
            {
                var last = _items[^1];
                _items.RemoveAt(_items.Count - 1);
                if (last) Destroy(last.gameObject);
            }

            _dirty = true;
        }

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            ReflowBatch();
        }

        private void ReflowBatch()
        {
            float y = 0f;

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (!it) continue;

                float h    = it.Rect.rect.height;
                bool isNew = _newItems.Contains(it);

                if (isNew)
                    it.SetTargetY(-y, animate: false, duration: 0f, ease: _reflowEase);
                else
                    it.SetTargetY(-y, animate: true, duration: _reflowDuration, ease: _reflowEase, delay: i * _reflowStagger);

                y += h + _spacing;
            }

            foreach (var it in _newItems)
                if (it) it.PlayEnter();

            _newItems.Clear();
        }

        private void RequestRemove(NotificationToastItem item)
        {
            int idx = _items.IndexOf(item);
            if (idx >= 0) _items.RemoveAt(idx);
            if (item) Destroy(item.gameObject);
            _dirty = true;
        }

        // ── Lookup helpers ────────────────────────────────────────────────────

        private Color GetBgColor(NotifyType t) => t switch
        {
            NotifyType.Warning => _bgWarning,
            NotifyType.Error   => _bgError,
            NotifyType.Reward  => _bgReward,
            NotifyType.OKTS    => _bgOkts,
            NotifyType.AN      => _bgAn,
            _                  => _bgInfo,
        };

        private Color GetBarColor(NotifyType t) => t switch
        {
            NotifyType.Warning => _barWarning,
            NotifyType.Error   => _barError,
            NotifyType.Reward  => _barReward,
            NotifyType.OKTS    => _barOkts,
            NotifyType.AN      => _barAn,
            _                  => _barInfo,
        };

        private Sprite GetIcon(NotifyType t) => t switch
        {
            NotifyType.Warning => _iconWarning,
            NotifyType.Error   => _iconError,
            NotifyType.Reward  => _iconReward,
            NotifyType.OKTS    => _iconOkts,
            NotifyType.AN      => _iconAn,
            _                  => _iconInfo,
        };
    }
}