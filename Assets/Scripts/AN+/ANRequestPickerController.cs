using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Tasks;

namespace AN_
{
    public sealed class ANRequestPickerController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameState _state;
        [SerializeField] private ANService _service;
        [SerializeField] private ANChatUIController _chatUI;
        [SerializeField] private RectTransform searchSlotContent; // левый-контент внутри слота (pivot 0,0.5)
        [SerializeField] private bool animateSwapInsteadOfSnap = true;
        [Header("Micro FX")]
        [SerializeField, Range(0f, 1f)] private float flyAlpha = 0.95f;
        [SerializeField] private float slotBounceScale = 1.03f;
        [SerializeField] private float slotBounceTime = 0.11f;
        
        private CanvasGroup _selectedCG;
        private GraphicRaycaster _raycaster;
        
        private RectTransform _placeholder;
        private LayoutElement _placeholderLE;
        private bool _isAnimating;
        private ANScanRequestButtonLogic _pendingPick;
        private int _opId;

        private LayoutElement _selectedLE;

        private struct RtSnapshot
        {
            public Vector2 anchorMin, anchorMax, pivot, sizeDelta, anchoredPos;
            public Vector3 localScale;
        }
        private RtSnapshot _rtBeforeMove;

        


        [Tooltip("Контроллер, который спавнит View и умеет находить View по Logic.")]
        [SerializeField] private ANScanButtonsVisibilityController _viewsController;

        [Header("UI Parents")]
        [SerializeField] private RectTransform activeListContent;   // где живут активные кнопки (content)
        [SerializeField] private RectTransform searchSlotAnchor;    // точка в поисковой строке
        [SerializeField] private RectTransform overlayMover;        // верхний слой для анимации

        [Header("Send")]
        [SerializeField] private Button sendButton;

        [Header("Anim")]
        [SerializeField] private float moveTime = 0.22f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;
        [SerializeField] private float selectScale = 1.0f;
        [SerializeField] private float flyScale = 1.02f;

        private ANScanRequestButtonLogic _selectedLogic;
        private ANScanRequestButtonView _selectedView;
        private RectTransform _selectedRT;

        private Transform _selectedOriginalParent;
        private int _selectedOriginalSibling;

        private Sequence _seq;

        private void Awake()
        {
            if (_state == null) _state = FindFirstObjectByType<GameState>(FindObjectsInactive.Include);
            if (_service == null) _service = FindFirstObjectByType<ANService>(FindObjectsInactive.Include);
            if (_chatUI == null) _chatUI = FindFirstObjectByType<ANChatUIController>(FindObjectsInactive.Include);

            if (_viewsController == null)
                _viewsController = FindFirstObjectByType<ANScanButtonsVisibilityController>(FindObjectsInactive.Include);

            if (sendButton != null)
                sendButton.onClick.AddListener(OnSend);
            
            _raycaster = GetComponentInParent<GraphicRaycaster>(true);

            UpdateSendInteractable();
        }
        private CanvasGroup GetOrAddCanvasGroup(RectTransform rt)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        private void SetRaycastForSelected(bool enabled)
        {
            // выключаем клики только на летящей вкладке
            if (_selectedCG != null)
                _selectedCG.blocksRaycasts = enabled;
        }

        private void SetGlobalClicks(bool enabled)
        {
            // опционально: полностью выключить клики по UI на время перелёта
            // чтобы вообще не было гонок из внешних UI.
            if (_raycaster != null)
                _raycaster.enabled = enabled;
        }


        private void OnDestroy()
        {
            if (sendButton != null)
                sendButton.onClick.RemoveListener(OnSend);
        }
        private void SnapshotRT(RectTransform rt)
        {
            _rtBeforeMove = new RtSnapshot
            {
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
                pivot = rt.pivot,
                sizeDelta = rt.sizeDelta,
                anchoredPos = rt.anchoredPosition,
                localScale = rt.localScale
            };
        }

        private void ApplySlotAnchors(RectTransform rt)
        {
            // фиксируем поведение в слоте: левый центр
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
        }

        private void RestoreRT(RectTransform rt)
        {
            rt.anchorMin = _rtBeforeMove.anchorMin;
            rt.anchorMax = _rtBeforeMove.anchorMax;
            rt.pivot     = _rtBeforeMove.pivot;
            rt.sizeDelta = _rtBeforeMove.sizeDelta;
            rt.anchoredPosition = _rtBeforeMove.anchoredPos;
            rt.localScale = _rtBeforeMove.localScale;
        }


        /// <summary>
        /// Вызывается из View при клике: передаем ЛОГИКУ, а не view.
        /// </summary>
        public void OnButtonPressedLogic(ANScanRequestButtonLogic logic)
        {
            if (logic == null || _viewsController == null) return;
        
            if (_isAnimating)
            {
                _pendingPick = logic; // запомнили последний клик
                return;
            }
        
            if (!_viewsController.TryGetView(logic, out var view) || view == null)
                return;
        
            // повторный клик по выбранной
            if (_selectedLogic == logic)
            {
                DeselectAnimated();
                return;
            }
        
            // если что-то уже в слоте: сначала убрать, потом вставить новую
            if (_selectedLogic != null)
            {
                _pendingPick = logic;
                DeselectAnimated(immediate: false);
                return;
            }
        
            SelectAnimated(logic, view);
        }
        private void FinishAnim()
        {
            _isAnimating = false;

            if (_pendingPick != null)
            {
                var next = _pendingPick;
                _pendingPick = null;
                OnButtonPressedLogic(next);
            }
        }




        private void EnsurePlaceholderFor(RectTransform target)
        {
            // Удалим старый, если был
            if (_placeholder != null) Destroy(_placeholder.gameObject);

            var go = new GameObject("Placeholder", typeof(RectTransform), typeof(LayoutElement));
            _placeholder = (RectTransform)go.transform;
            _placeholderLE = go.GetComponent<LayoutElement>();

            // Вставляем в то же место, где был элемент
            _placeholder.SetParent(_selectedOriginalParent, false);
            _placeholder.SetSiblingIndex(_selectedOriginalSibling);

            // Берем размеры из LayoutUtility/rect
            float w = LayoutUtility.GetPreferredSize(target, 0);
            float h = LayoutUtility.GetPreferredSize(target, 1);
            if (w <= 0.01f) w = target.rect.width;
            if (h <= 0.01f) h = target.rect.height;

            _placeholderLE.preferredWidth = w;
            _placeholderLE.preferredHeight = h;
            _placeholderLE.minWidth = w;
            _placeholderLE.minHeight = h;
        }
        private Vector3 GetWorldPos(RectTransform rt)
        {
            // позиция опорной точки rt (его pivot) в мире
            return rt.TransformPoint(rt.rect.center);
        }
        private Vector3 GetLeftCenterWorld(RectTransform rt)
        {
            // локальная точка левого центра: xMin, середина по Y
            Vector3 local = new Vector3(rt.rect.xMin, (rt.rect.yMin + rt.rect.yMax) * 0.5f, 0f);
            return rt.TransformPoint(local);
        }

        private Vector3 GetLeftCenterWorldOfSelected()
        {
            // левый центр у текущей вкладки
            Vector3 local = new Vector3(_selectedRT.rect.xMin, (_selectedRT.rect.yMin + _selectedRT.rect.yMax) * 0.5f, 0f);
            return _selectedRT.TransformPoint(local);
        }
        private void SnapToSearchLeft()
        {
            // предполагаем, что searchSlotContent pivot = (0, 0.5)
            // и кнопка имеет pivot любой (обычно 0.5, 0.5)
            float x = _selectedRT.rect.width * _selectedRT.pivot.x; // это смещение pivot до левого края
            _selectedRT.anchoredPosition = new Vector2(x, 0f);
        }
        
        /// <summary>
        /// Если логика стала невалидной (кнопка пропала), снять выбор.
        /// </summary>
        public void NotifyLogicBecameInvalid(ANScanRequestButtonLogic logic)
        {
            if (_selectedLogic == logic)
                DeselectAnimated(immediate: true);
        }

        private void SelectAnimated(ANScanRequestButtonLogic logic, ANScanRequestButtonView view)
        {
            if (activeListContent == null || searchSlotAnchor == null || overlayMover == null) return;

            _isAnimating = true;
            int myOp = ++_opId;

            _selectedLogic = logic;
            _selectedView = view;
            _selectedRT = view.Rect;
            
            _selectedCG = GetOrAddCanvasGroup(_selectedRT);
            _selectedCG.alpha = 1f;
            _selectedCG.blocksRaycasts = true;


            _selectedOriginalParent = _selectedRT.parent;
            _selectedOriginalSibling = _selectedRT.GetSiblingIndex();

            _selectedLE = _selectedRT.GetComponent<LayoutElement>();
            if (_selectedLE != null) _selectedLE.ignoreLayout = true;

            // сохранили RT, чтобы после слота вернуть как было
            SnapshotRT(_selectedRT);

            // placeholder (у тебя он уже есть в прошлой версии) обязателен для точного возврата
            EnsurePlaceholderFor(_selectedRT);
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeListContent);

            KillSeq();

            Vector3 startWorld = _selectedRT.position;
            Vector3 targetWorld = searchSlotAnchor.position;

            _selectedRT.SetParent(overlayMover, true);
            SetRaycastForSelected(false);
            
            _selectedCG.alpha = flyAlpha;
            _selectedRT.position = startWorld;

            _seq = DOTween.Sequence();
            _seq.Join(_selectedRT.DOMove(targetWorld, moveTime).SetEase(moveEase));
            _seq.Join(_selectedRT.DOScale(flyScale, moveTime * 0.5f).SetEase(Ease.OutQuad));
            _seq.Append(_selectedRT.DOScale(selectScale, moveTime * 0.5f).SetEase(Ease.InOutQuad));
            _seq.Join(_selectedCG.DOFade(1f, moveTime).SetEase(Ease.OutQuad));


            _seq.OnComplete(() =>
            {
                if (myOp != _opId) return; // устаревший onComplete

                // фиксируем в слот
                _selectedRT.SetParent(searchSlotAnchor, false);

                // ключ: задаем слотовые anchors/pivot, чтобы НЕ было “потом выровняло вверх-влево”
                ApplySlotAnchors(_selectedRT);

                // при pivot (0,0.5) достаточно anchoredPosition = 0 чтобы левый край совпал
                _selectedRT.anchoredPosition = Vector2.zero;
                _selectedRT.localScale = Vector3.one; // базовый, чтобы bounce выглядел чисто

                _selectedRT.anchoredPosition = Vector2.zero;

                SetRaycastForSelected(true);

                float baseS = selectScale;
                _selectedRT.localScale = Vector3.one * baseS;

                DOTween.Kill(_selectedRT); // на всякий, если кто-то ещё твины вешает на тот же transform
                DOTween.Sequence()
                    .Append(_selectedRT.DOScale(baseS * slotBounceScale, slotBounceTime).SetEase(Ease.OutQuad))
                    .Append(_selectedRT.DOScale(baseS, slotBounceTime).SetEase(Ease.InOutQuad));

                UpdateSendInteractable();
                FinishAnim();
            });
        }
        
        private void DeselectAnimated(bool immediate = false)
        {
            if (_selectedLogic == null || _selectedRT == null) return;
            if (overlayMover == null) return;

            _isAnimating = true;
            int myOp = ++_opId;

            KillSeq();

            if (immediate)
            {
                ReturnToActiveInstant();
                FinishAnim();
                return;
            }

            if (_placeholder == null)
            {
                ReturnToActiveInstant();
                FinishAnim();
                return;
            }
            

            LayoutRebuilder.ForceRebuildLayoutImmediate(activeListContent);

            Vector3 startWorld = _selectedRT.position;
            Vector3 targetWorld = _placeholder.position; // точная цель
            
            _selectedCG ??= GetOrAddCanvasGroup(_selectedRT);
            SetRaycastForSelected(false);
            _selectedCG.alpha = flyAlpha;


            _selectedRT.SetParent(overlayMover, true);
            _selectedRT.position = startWorld;

            _seq = DOTween.Sequence();
            _seq.Join(_selectedRT.DOMove(targetWorld, moveTime).SetEase(moveEase));
            _seq.Join(_selectedRT.DOScale(flyScale, moveTime * 0.5f).SetEase(Ease.OutQuad));
            _seq.Append(_selectedRT.DOScale(1f, moveTime * 0.5f).SetEase(Ease.InOutQuad));
            _seq.Join(_selectedCG.DOFade(1f, moveTime).SetEase(Ease.OutQuad));


            _seq.OnComplete(() =>
            {
                if (myOp != _opId) return;

                ReturnToActiveInstant();
                FinishAnim();
            });
        }



        private void ReturnToActiveInstant()
        {
            if (_selectedRT == null) return;

            if (_placeholder != null)
            {
                var parent = _placeholder.parent;
                int sibling = _placeholder.GetSiblingIndex();

                _selectedRT.SetParent(parent, false);
                _selectedRT.SetSiblingIndex(sibling);

                Destroy(_placeholder.gameObject);
                _placeholder = null;
                _placeholderLE = null;
            }
            else
            {
                _selectedRT.SetParent(_selectedOriginalParent != null ? _selectedOriginalParent : activeListContent, false);
                _selectedRT.SetSiblingIndex(_selectedOriginalSibling);
            }

            // Важно: вернуть anchors/pivot какими они были у кнопки в списке
            RestoreRT(_selectedRT);

            if (_selectedLE != null) _selectedLE.ignoreLayout = false;

            _selectedRT.localScale = Vector3.one;

            _selectedLogic = null;
            _selectedView = null;
            _selectedRT = null;
            _selectedOriginalParent = null;
            _selectedOriginalSibling = 0;
            _selectedLE = null;

            UpdateSendInteractable();
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeListContent);
            if (_selectedCG != null)
            {
                _selectedCG.alpha = 1f;
                _selectedCG.blocksRaycasts = true;
            }
            _selectedCG = null;

        }



        private void OnSend()
        {
            if (_selectedLogic == null) return;
            if (_state == null || _service == null || _chatUI == null) return;

            var req = _selectedLogic.Request;
            var item = _selectedLogic.Item;
            if (req == null || item == null) return;

            bool ok = _service.MakeRequest(req);
            if (!ok) return;

            _state.MarkItemAnalyzed(item.id);

            _chatUI.OpenFromRequest(req);

            DeselectAnimated(immediate: true);
        }

        private void UpdateSendInteractable()
        {
            if (sendButton != null)
                sendButton.interactable = (_selectedLogic != null);
        }

        private void KillSeq()
        {
            if (_seq != null)
            {
                _seq.Kill();
                _seq = null;
            }

            // Если вдруг выбранная зависла в overlay (из-за kill), возвращаем немедленно
            if (_selectedRT != null && overlayMover != null && _selectedRT.parent == overlayMover)
            {
                ReturnToActiveInstant();
            }
        }

    }
}
