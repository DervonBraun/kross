using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AN_
{
    public sealed class ANScanRequestButtonView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _label;

        private ANScanRequestButtonLogic _logic;
        private ANRequestPickerController _picker;

        public RectTransform Rect => (RectTransform)transform;
        public ANScanRequestButtonLogic Logic => _logic;

        private void Awake()
        {
            if (_button == null) _button = GetComponentInChildren<Button>(true);
            if (_label == null && _button != null)
                _label = _button.GetComponentInChildren<TextMeshProUGUI>(true);

            if (_button != null)
                _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClick);
        }

        public void Bind(ANScanRequestButtonLogic logic, ANRequestPickerController picker)
        {
            _logic = logic;
            _picker = picker;

            if (_label != null && _logic != null && _logic.Request != null)
                _label.SetText(_logic.Request.title);

            var le = GetComponent<LayoutElement>();
            if (le != null && _logic != null)
            {
                le.preferredWidth = _logic.PreferredWidth;
                le.preferredHeight = _logic.PreferredHeight;
            }
        }


        private void OnClick()
        {
            if (_picker == null || _logic == null) return;
            _picker.OnButtonPressedLogic(_logic); // picker работает с логикой
        }
    }
}