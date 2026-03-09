using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Tasks;

namespace AN_
{
    public sealed class ANScanRequestPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public ScannableItemDef item;
            [Tooltip("Optional override. If null, uses ScannableItemDef.analyzeRequest")]
            public ANRequestDef requestOverride;
            public Button button;
            public TextMeshProUGUI label;
        }

        [Header("Refs")]
        [SerializeField] private GameState _state;
        [SerializeField] private ANService _service;

        [Header("Entries")]
        [SerializeField] private Entry[] _entries;

        [Header("Behavior")]
        [SerializeField] private bool _hideIfNotAnalyzeType = true;

        private void Awake()
        {
            if (_service == null) _service = FindFirstObjectByType<ANService>();
            if (_state == null)
                _state = _service != null ? _service.State : FindFirstObjectByType<GameState>();

            WireButtons();
            ApplyLabels();
            RefreshAll(force: true);
        }

        private void OnEnable()
        {
            if (_state != null)
            {
                _state.ItemScanned += OnItemScanned;
                _state.ItemAnalyzed += OnItemAnalyzed;
            }

            RefreshAll(force: true);
        }

        private void OnDisable()
        {
            if (_state != null)
            {
                _state.ItemScanned -= OnItemScanned;
                _state.ItemAnalyzed -= OnItemAnalyzed;
            }
        }

        private void WireButtons()
        {
            if (_entries == null) return;

            for (int i = 0; i < _entries.Length; i++)
            {
                int index = i;
                var e = _entries[i];
                if (e == null || e.button == null) continue;

                e.button.onClick.RemoveAllListeners();
                e.button.onClick.AddListener(() => OnEntryClicked(index));
            }
        }

        private void ApplyLabels()
        {
            if (_entries == null) return;

            for (int i = 0; i < _entries.Length; i++)
            {
                var e = _entries[i];
                if (e == null || e.label == null) continue;

                var req = ResolveRequest(e);
                if (req != null && !string.IsNullOrWhiteSpace(req.title))
                    e.label.SetText(req.title);
            }
        }

        private void RefreshAll(bool force)
        {
            if (_entries == null) return;

            for (int i = 0; i < _entries.Length; i++)
                RefreshEntry(i, force);
        }

        private void RefreshEntry(int index, bool force)
        {
            if (_state == null || _entries == null) return;
            if (index < 0 || index >= _entries.Length) return;

            var e = _entries[index];
            if (e == null || e.button == null || e.item == null)
            {
                if (force && e != null && e.button != null)
                    e.button.gameObject.SetActive(false);
                return;
            }

            var req = ResolveRequest(e);
            if (req == null)
            {
                if (force) e.button.gameObject.SetActive(false);
                return;
            }

            if (_hideIfNotAnalyzeType && req.type != ANRequestType.AnalyzeItem)
            {
                if (force) e.button.gameObject.SetActive(false);
                return;
            }

            bool show = _state.IsItemScanned(e.item.id) && !_state.IsItemAnalyzed(e.item.id);
            if (e.button.gameObject.activeSelf != show)
                e.button.gameObject.SetActive(show);
            e.button.interactable = show;
        }

        private ANRequestDef ResolveRequest(Entry e)
        {
            if (e == null) return null;
            return e.requestOverride != null ? e.requestOverride : e.item != null ? e.item.analyzeRequest : null;
        }

        private void OnEntryClicked(int index)
        {
            if (_entries == null || _state == null || _service == null) return;
            if (index < 0 || index >= _entries.Length) return;

            var e = _entries[index];
            if (e == null || e.item == null) return;

            var req = ResolveRequest(e);
            if (req == null) return;

            bool ok = _service.MakeRequest(req);
            if (ok)
            {
                _state.MarkItemAnalyzed(e.item.id);
                RefreshEntry(index, force: true);
            }
        }

        private void OnItemScanned(string itemId)
        {
            RefreshByItemId(itemId);
        }

        private void OnItemAnalyzed(string itemId)
        {
            RefreshByItemId(itemId);
        }

        private void RefreshByItemId(string itemId)
        {
            if (_entries == null) return;

            for (int i = 0; i < _entries.Length; i++)
            {
                var e = _entries[i];
                if (e == null || e.item == null) continue;
                if (e.item.id == itemId)
                    RefreshEntry(i, force: true);
            }
        }
    }
}
