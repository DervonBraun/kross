using System.Collections.Generic;
using System.Linq;
using Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AN_
{
    public sealed class ANScanButtonsVisibilityController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private RectTransform buttonsRoot;                 // Content списка
        [SerializeField] private ANScanRequestButtonView buttonViewPrefab; // Префаб кнопки (View)

        [Header("Refs")]
        [SerializeField] private GameState _state;
        [SerializeField] private ANRequestPickerController _picker;

        [Header("Refresh")]
        [SerializeField] private float refreshInterval = 0.2f;

        [Header("Order")]
        [Tooltip("Если включено, сортируем по siblingIndex логик (как они стоят на объекте/в иерархии).")]
        [SerializeField] private bool keepLogicOrder = true;

        private readonly List<ANScanRequestButtonLogic> _logics = new();
        private readonly Dictionary<ANScanRequestButtonLogic, ANScanRequestButtonView> _views = new();

        private float _t;

        private void Awake()
        {
            if (_state == null)
                _state = FindFirstObjectByType<GameState>(FindObjectsInactive.Include);

            if (_picker == null)
                _picker = FindFirstObjectByType<ANRequestPickerController>(FindObjectsInactive.Include);

            RebuildLogicCache();
            EnsureViews();
            RefreshAll(forceReorder: true);
        }

        private void Update()
        {
            _t -= Time.unscaledDeltaTime;
            if (_t > 0f) return;
            _t = refreshInterval;

            RefreshAll(forceReorder: false);
        }

        public void RebuildLogicCache()
        {
            _logics.Clear();

            // Берем логики на этом объекте и дочерних (включая неактивные)
            GetComponentsInChildren(true, _logics);

            if (keepLogicOrder)
            {
                // оставляем порядок по иерархии/сиблингам
                _logics.Sort((a, b) =>
                    a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            }
        }

        private void EnsureViews()
        {
            if (buttonsRoot == null || buttonViewPrefab == null) return;

            // создаем view для каждой logic
            for (int i = 0; i < _logics.Count; i++)
            {
                var logic = _logics[i];
                if (logic == null) continue;

                if (_views.ContainsKey(logic) && _views[logic] != null)
                    continue;

                var view = Instantiate(buttonViewPrefab, buttonsRoot);
                view.name = $"btn_{logic.name}";
                view.Bind(logic, _picker);

                _views[logic] = view;
            }

            // чистим отвалившиеся
            var dead = _views.Where(kv => kv.Key == null || kv.Value == null).Select(kv => kv.Key).ToList();
            for (int i = 0; i < dead.Count; i++)
                _views.Remove(dead[i]);
        }

        public void RefreshAll(bool forceReorder)
        {
            if (_state == null) return;

            // если логики добавились/удалились во время игры
            // можно реже дергать, но для надежности:
            RebuildLogicCache();
            EnsureViews();

            // видимость
            for (int i = 0; i < _logics.Count; i++)
            {
                var logic = _logics[i];
                if (logic == null) continue;

                if (!_views.TryGetValue(logic, out var view) || view == null)
                    continue;

                bool show = logic.ShouldShow(_state);

                if (view.gameObject.activeSelf != show)
                    view.gameObject.SetActive(show);

                // чтобы скрытые не занимали место в layout
                var le = view.GetComponent<LayoutElement>();
                if (le != null) le.ignoreLayout = !show;
            }

            if (forceReorder)
                ReorderVisibleViews();
        }
        public bool TryGetView(ANScanRequestButtonLogic logic, out ANScanRequestButtonView view)
        {
            view = null;

            if (logic == null)
                return false;

            if (!_views.TryGetValue(logic, out var found))
                return false;

            if (found == null)
                return false;

            view = found;
            return true;
        }


        private void ReorderVisibleViews()
        {
            // строгий порядок: видимые view идут по порядку logics
            int idx = 0;
            for (int i = 0; i < _logics.Count; i++)
            {
                var logic = _logics[i];
                if (logic == null) continue;

                if (!_views.TryGetValue(logic, out var view) || view == null) continue;
                if (!view.gameObject.activeSelf) continue;

                view.transform.SetSiblingIndex(idx++);
            }

            // пинок лейауту
            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonsRoot);
        }
    }
}
