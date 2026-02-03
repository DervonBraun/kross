using UnityEngine;
using DG.Tweening;

namespace Player
{
    public class LensUIController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform _canvasRoot;

        [Header("Petals")]
        [SerializeField] private RectTransform _petalTL;
        [SerializeField] private RectTransform _petalTR;
        [SerializeField] private RectTransform _petalBR;
        [SerializeField] private RectTransform _petalBL;

        [Header("Tracking")]
        [SerializeField] private float _paddingPx = 12f;
        [SerializeField] private float _smoothTime = 0.08f;
        [SerializeField] private float _minRectSizePx = 8f;

        [Header("Idle (no target)")]
        [SerializeField] private float _idleSizePx = 160f;
        [SerializeField] private Vector2 _idleCenterOffset = new Vector2(0f, 0f);

        [Header("Show/Hide Anim (DOTween)")]
        [SerializeField] private float _showDuration = 0.20f;
        [SerializeField] private float _hideDuration = 0.12f;
        [SerializeField] private Ease _showEase = Ease.OutCubic;
        [SerializeField] private Ease _hideEase = Ease.InCubic;

        private Camera _cam;
        private bool _aimActive;
        private bool _visible;
        private PlayerContext _context;
        private InteractionDetector _detector;

        private Component _currentTarget;
        private IInteractableAim _aimTarget;          // кешируем, чтобы не искать каждый кадр (опционально)
        private IFocusBoundsProvider _boundsProvider;


        private Vector2 _vTL, _vTR, _vBR, _vBL;

        private void Awake()
        {
            _context = GetComponent<PlayerContext>();
            _detector = _context.InteractionDetector;
            _cam = _context != null ? _context.Camera : null;

            if (_context != null)
                _context.CameraStateChanged += OnCameraStateChanged;

            if (_detector != null)
                _detector.TargetChanged += OnTargetChanged;

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (_context != null)
                _context.CameraStateChanged -= OnCameraStateChanged;

            if (_detector != null)
                _detector.TargetChanged -= OnTargetChanged;
        }

        private void Update()
        {
            if (!_aimActive) return;

            if (_cam == null && _context != null) _cam = _context.Camera;
            if (_cam == null) return;

            // 1) Пытаемся получить rect цели
            bool hasTargetRect = TryGetTargetScreenRect(out Rect targetRect);

            // 2) Если цели нет, используем idle rect в центре
            Rect rectToUse = hasTargetRect ? targetRect : GetIdleScreenRect();

            // 3) Паддинг, минимальный размер
            rectToUse = ApplyPadding(rectToUse, _paddingPx);

            if (rectToUse.width < _minRectSizePx || rectToUse.height < _minRectSizePx)
                rectToUse = GetIdleScreenRect(); // fallback

            // 4) Лепестки в Aim должны быть всегда видимы
            Show();

            // 5) Screen corners -> Local corners
            var sTL = new Vector2(rectToUse.xMin, rectToUse.yMax);
            var sTR = new Vector2(rectToUse.xMax, rectToUse.yMax);
            var sBR = new Vector2(rectToUse.xMax, rectToUse.yMin);
            var sBL = new Vector2(rectToUse.xMin, rectToUse.yMin);

            if (!ScreenToLocal(_canvasRoot, sTL, out var lTL)) return;
            if (!ScreenToLocal(_canvasRoot, sTR, out var lTR)) return;
            if (!ScreenToLocal(_canvasRoot, sBR, out var lBR)) return;
            if (!ScreenToLocal(_canvasRoot, sBL, out var lBL)) return;

            // 6) Smooth follow
            _petalTL.anchoredPosition = Vector2.SmoothDamp(_petalTL.anchoredPosition, lTL, ref _vTL, _smoothTime);
            _petalTR.anchoredPosition = Vector2.SmoothDamp(_petalTR.anchoredPosition, lTR, ref _vTR, _smoothTime);
            _petalBR.anchoredPosition = Vector2.SmoothDamp(_petalBR.anchoredPosition, lBR, ref _vBR, _smoothTime);
            _petalBL.anchoredPosition = Vector2.SmoothDamp(_petalBL.anchoredPosition, lBL, ref _vBL, _smoothTime);
        }

        private void OnCameraStateChanged(CameraState state)
        {
            _aimActive = state == CameraState.Aim;

            if (_aimActive)
            {
                Show(); // теперь показываем сразу
            }
            else
            {
                _currentTarget = null;
                _boundsProvider = null;
                Hide();
            }
        }

        private void OnTargetChanged(Component target)
        {
            _currentTarget = target;

            if (_currentTarget == null)
            {
                _aimTarget = null;
                _boundsProvider = null; // НЕ Hide(), в Aim остаётся idle
                return;
            }

            // LensUI имеет смысл только для Aim-интерактива
            _aimTarget = _currentTarget.GetComponentInParent<IInteractableAim>();
            if (_aimTarget == null)
            {
                _boundsProvider = null;
                return;
            }

            _boundsProvider = TryResolveBoundsProvider(_currentTarget);
        }

        private bool TryGetTargetScreenRect(out Rect rect)
        {
            rect = default;

            if (_currentTarget == null) return false;
            if (_aimTarget == null) return false;        // NEW
            if (_boundsProvider == null) return false;
            if (!_boundsProvider.TryGetFocusBounds(out var bounds)) return false;

            if (!TryGetScreenRect(bounds, _cam, out var r)) return false;
            rect = r;
            return true;
        }

        private Rect GetIdleScreenRect()
        {
            // idle rect вокруг центра экрана
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + _idleCenterOffset;
            float half = _idleSizePx * 0.5f;
            return Rect.MinMaxRect(center.x - half, center.y - half, center.x + half, center.y + half);
        }

        private static Rect ApplyPadding(Rect r, float pad)
        {
            r.xMin -= pad; r.yMin -= pad;
            r.xMax += pad; r.yMax += pad;
            return r;
        }

        private IFocusBoundsProvider TryResolveBoundsProvider(Component target)
        {
            if (target == null) return null;
            return target.GetComponentInParent<IFocusBoundsProvider>();
        }

        private static bool ScreenToLocal(RectTransform root, Vector2 screen, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out local);
        }

        private static bool TryGetScreenRect(Bounds b, Camera cam, out Rect rect)
        {
            Vector3 min = b.min;
            Vector3 max = b.max;

            Vector3[] pts =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < pts.Length; i++)
            {
                var sp = cam.WorldToScreenPoint(pts[i]);
                if (sp.z <= 0.0001f) { rect = default; return false; }

                minX = Mathf.Min(minX, sp.x);
                minY = Mathf.Min(minY, sp.y);
                maxX = Mathf.Max(maxX, sp.x);
                maxY = Mathf.Max(maxY, sp.y);
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private void Show()
        {
            if (_visible) return;
            _visible = true;

            SetPetalsActive(true);
            KillPetalTweens();

            AnimateScale(_petalTL, 1f, _showDuration, _showEase);
            AnimateScale(_petalTR, 1f, _showDuration, _showEase);
            AnimateScale(_petalBR, 1f, _showDuration, _showEase);
            AnimateScale(_petalBL, 1f, _showDuration, _showEase);
        }

        private void Hide()
        {
            if (!_visible) return;
            _visible = false;

            KillPetalTweens();

            HideScale(_petalTL);
            HideScale(_petalTR);
            HideScale(_petalBR);
            HideScale(_petalBL);
        }

        private void HideImmediate()
        {
            KillPetalTweens();

            if (_petalTL) _petalTL.localScale = Vector3.zero;
            if (_petalTR) _petalTR.localScale = Vector3.zero;
            if (_petalBR) _petalBR.localScale = Vector3.zero;
            if (_petalBL) _petalBL.localScale = Vector3.zero;

            SetPetalsActive(false);
            _visible = false;
        }

        private void SetPetalsActive(bool on)
        {
            if (_petalTL) _petalTL.gameObject.SetActive(on);
            if (_petalTR) _petalTR.gameObject.SetActive(on);
            if (_petalBR) _petalBR.gameObject.SetActive(on);
            if (_petalBL) _petalBL.gameObject.SetActive(on);
        }

        private void KillPetalTweens()
        {
            if (_petalTL) _petalTL.DOKill();
            if (_petalTR) _petalTR.DOKill();
            if (_petalBR) _petalBR.DOKill();
            if (_petalBL) _petalBL.DOKill();
        }

        private static void AnimateScale(RectTransform rt, float to, float dur, Ease ease)
        {
            if (!rt) return;
            rt.localScale = Vector3.zero;
            rt.DOScale(to, dur).SetEase(ease).SetUpdate(true);
        }

        private void HideScale(RectTransform rt)
        {
            if (!rt) return;
            rt.DOScale(0f, _hideDuration)
              .SetEase(_hideEase)
              .SetUpdate(true)
              .OnComplete(() => { if (rt) rt.gameObject.SetActive(false); });
        }
    }
}
