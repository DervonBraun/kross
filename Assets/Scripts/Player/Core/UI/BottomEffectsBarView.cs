using System.Collections.Generic;
using DG.Tweening;
using EffectSystem;
using Player.EffectSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Player
{
    public sealed class BottomEffectsBarView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private EffectManager _effects;
        [SerializeField] private RectTransform _root;
        [SerializeField] private EffectBarSegmentView _segmentPrefab;

        [Header("Behavior")]
        [Min(0)]
        [SerializeField] private int _maxShown = 8;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float _moveDuration = 0.22f;
        [SerializeField, Min(0f)] private float _enterDuration = 0.22f;
        [SerializeField, Min(0f)] private float _exitDuration = 0.18f;
        [SerializeField] private Ease _moveEase = Ease.OutCubic;
        [SerializeField] private Ease _enterEase = Ease.OutCubic;
        [SerializeField] private Ease _exitEase = Ease.InCubic;

        [Tooltip("Насколько далеко за край уводить/заводить сегменты (в пикселях).")]
        [SerializeField, Min(0f)] private float _offscreenPadding = 40f;

        [Tooltip("Если true, при первом построении не анимируем.")]
        [SerializeField] private bool _skipAnimOnFirstBuild = true;

        private HorizontalLayoutGroup _layout;
        private bool _builtOnce;

        // active views by effect id (MVP: эффекты уникальные)
        private readonly Dictionary<string, EffectBarSegmentView> _viewsById = new();

        // pool for reuse
        private readonly Stack<EffectBarSegmentView> _pool = new();

        // running tweens to kill on rebuild
        private readonly List<Tween> _running = new();

        private void Awake()
        {
            if (_root == null) _root = (RectTransform)transform;
            _layout = _root.GetComponent<HorizontalLayoutGroup>();
        }

        private void OnEnable()
        {
            if (_effects != null)
                _effects.EffectChanged += OnEffectsChanged;

            RebuildAnimated();
        }

        private void OnDisable()
        {
            if (_effects != null)
                _effects.EffectChanged -= OnEffectsChanged;

            KillRunningTweens();
        }

        private void OnEffectsChanged(EffectChangeType type, EffectInstance inst)
        {
            RebuildAnimated();
        }

        public void RebuildAnimated()
        {
            bool instant = _skipAnimOnFirstBuild && !_builtOnce;
            _builtOnce = true;

            if (_effects == null || _root == null || _segmentPrefab == null)
                return;

            KillRunningTweens();

            var existingBefore = new HashSet<string>(_viewsById.Keys);

            var desired = BuildDesiredList(_effects.Active, _maxShown);

            var leaving = new List<string>();
            foreach (var id in _viewsById.Keys)
                if (!ContainsId(desired, id))
                    leaving.Add(id);
            
            for (int i = 0; i < leaving.Count; i++)
            {
                var id = leaving[i];
                if (_viewsById.TryGetValue(id, out var v))
                    v.SetIgnoreLayout(true);
            }

            // FIRST: позиции и ширины ДО layout-перестановки
            var firstPos = new Dictionary<string, Vector2>(_viewsById.Count);
            var firstW   = new Dictionary<string, float>(_viewsById.Count);

            foreach (var kv in _viewsById)
            {
                var slot = kv.Value.Slot;
                firstPos[kv.Key] = slot.anchoredPosition;
                firstW[kv.Key]   = slot.rect.width;
                kv.Value.KillTweens();
            }

            // Ensure views for desired
            for (int i = 0; i < desired.Count; i++)
            {
                var inst = desired[i];
                var id = inst.Def.Id;

                if (!_viewsById.TryGetValue(id, out var view))
                {
                    view = GetFromPoolOrCreate();
                    _viewsById.Add(id, view);
                    view.gameObject.SetActive(true);
                }

                view.Bind(inst);
            }

            // sibling order
            for (int i = 0; i < desired.Count; i++)
                _viewsById[desired[i].Def.Id].transform.SetSiblingIndex(i);

            // LAST: включаем layout, получаем target позиции/ширины
            var targetsPos = new Dictionary<string, Vector2>(desired.Count);
            var targetsW   = new Dictionary<string, float>(desired.Count);

            if (_layout != null) _layout.enabled = true;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_root);

            for (int i = 0; i < desired.Count; i++)
            {
                var id = desired[i].Def.Id;
                var slot = _viewsById[id].Slot;
                targetsPos[id] = slot.anchoredPosition;
                targetsW[id]   = slot.rect.width;
            }

            // выключаем layout, чтобы позиции не перетирались во время твинов
            if (_layout != null) _layout.enabled = false;

            float rootW = _root.rect.width;
            float enterShift = rootW + _offscreenPadding;
            float exitShift  = rootW + _offscreenPadding;

            // INVERT: вернуть слоты на FIRST, а визуал масштабировать под новую ширину
            foreach (var kv in _viewsById)
            {
                var id = kv.Key;
                var view = kv.Value;

                // вернем позицию, если была
                if (firstPos.TryGetValue(id, out var p))
                    view.Slot.anchoredPosition = p;

                // flip по ширине: old/new (для тех, кто останется или был)
                if (targetsW.TryGetValue(id, out var newW) && firstW.TryGetValue(id, out var oldW))
                    view.SetFlipScale(oldW, newW);
            }

            // Новые: старт справа, визуал сжат
            for (int i = 0; i < desired.Count; i++)
            {
                var id = desired[i].Def.Id;
                bool isNew = !existingBefore.Contains(id);
                if (!isNew) continue;
                

                var view = _viewsById[id];
                var tPos = targetsPos[id];

                view.Slot.anchoredPosition = new Vector2(tPos.x + enterShift, tPos.y);
                view.ResetState();
                view.SetVisualScaleX(0f);
                view.Fade(1f, 0f, Ease.Linear); // гарантируем видимость
            }

            if (instant)
            {
                for (int i = 0; i < desired.Count; i++)
                {
                    var id = desired[i].Def.Id;
                    _viewsById[id].Slot.anchoredPosition = targetsPos[id];
                    _viewsById[id].SetVisualScaleX(1f);
                }

                foreach (var id in leaving)
                    DespawnToPool(id);

                if (_layout != null)
                {
                    _layout.enabled = true;
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
                }
                return;
            }

            // PLAY: moving + visual resize smoothing
            for (int i = 0; i < desired.Count; i++)
            {
                var id = desired[i].Def.Id;
                bool isNew = !existingBefore.Contains(id);

                var view = _viewsById[id];
                var slot = view.Slot;
                
                if (isNew)
                {
                    view.ResetState();      // гарантируем scaleX=1
                    view.SetVisualScaleX(0f); // и уже осознанно схлопываем для въезда
                }

                float dur  = isNew ? _enterDuration : _moveDuration;
                Ease  ease = isNew ? _enterEase : _moveEase;

                // позиция слота
                _running.Add(slot.DOAnchorPos(targetsPos[id], dur).SetEase(ease).SetUpdate(true));

                // визуал мягко "дорастает" до новой ширины (scaleX -> 1)
                view.AnimateVisualToNormal(dur, ease);
            }

            // leaving: уезжает влево + схлоп визуала
            for (int i = 0; i < leaving.Count; i++)
            {
                var id = leaving[i];
                if (!_viewsById.TryGetValue(id, out var view)) continue;

                var slot = view.Slot;
                var endPos = slot.anchoredPosition + new Vector2(-exitShift, 0f);

                view.KillTweens();
                view.Fade(0f, _exitDuration, _exitEase);
                view.SetVisualScaleX(1f);
                view.AnimateVisualToNormal(0f, Ease.Linear); // no-op, просто чтобы не осталось старых твинов
                view.SetVisualScaleX(1f);

                // схлопнуть визуал на выходе (приятнее)
                _running.Add(view.TweenVisualScaleX(0f, _exitDuration, _exitEase));

                var moveTw = slot.DOAnchorPos(endPos, _exitDuration)
                    .SetEase(_exitEase)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (_viewsById.TryGetValue(id, out var current) && current == view)
                            DespawnToPool(id);
                    });

                _running.Add(moveTw);
            }

            // вернуть layout после анимаций
            float longest = Mathf.Max(_moveDuration, _enterDuration, _exitDuration);
            bool hasLeaving = leaving.Count > 0;

            _running.Add(DOVirtual.DelayedCall(longest + 0.01f, () =>
            {
                if (_layout != null && !hasLeaving)
                {
                    _layout.enabled = true;
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
                }
            }).SetUpdate(true));

        }




        private List<EffectInstance> BuildDesiredList(IReadOnlyList<EffectInstance> active, int maxShown)
        {
            int count = active.Count;
            if (maxShown > 0) count = Mathf.Min(count, maxShown);

            var result = new List<EffectInstance>(count);
            for (int i = 0; i < count; i++)
                result.Add(active[i]);

            return result;
        }

        private bool ContainsId(List<EffectInstance> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Def.Id == id) return true;
            return false;
        }

        private EffectBarSegmentView GetFromPoolOrCreate()
        {
            EffectBarSegmentView v;

            if (_pool.Count > 0)
            {
                v = _pool.Pop();
                v.gameObject.SetActive(true);
            }
            else
            {
                v = Instantiate(_segmentPrefab, _root);
                v.gameObject.SetActive(true);
            }

            v.ResetState(); // <-- ключевое

            return v;
        }

        private void DespawnToPool(string id)
        {
            if (!_viewsById.TryGetValue(id, out var view)) return;

            // сбросить layout-исключение и твины, чтобы не тащить мусор из "leaving" в пул
            view.SetIgnoreLayout(false);
            view.ResetState();

            view.gameObject.SetActive(false);
            _viewsById.Remove(id);
            _pool.Push(view);
        }


        private void KillRunningTweens()
        {
            for (int i = 0; i < _running.Count; i++)
                _running[i]?.Kill();

            _running.Clear();
        }

        private bool WasJustCreated(EffectBarSegmentView view) => false; // оставлено специально, см. ниже
    }
}
