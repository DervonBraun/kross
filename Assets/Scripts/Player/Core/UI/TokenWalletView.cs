using TMPro;
using Tasks;
using UnityEngine;

namespace Player
{
    public sealed class TokenWalletView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameState _state;
        [SerializeField] private TextMeshProUGUI _redText;
        [SerializeField] private TextMeshProUGUI _greenText;
        [SerializeField] private TextMeshProUGUI _blueText;

        [Header("Format")]
        [SerializeField] private string _redFormat = "{0}";
        [SerializeField] private string _greenFormat = "{0}";
        [SerializeField] private string _blueFormat = "{0}";

        private TokenAmount _last;
        private bool _hasLast;

        private void Awake()
        {
            if (_state == null) _state = FindFirstObjectByType<GameState>();
            Refresh(force: true);
        }

        private void OnEnable()
        {
            Refresh(force: true);
        }

        private void Update()
        {
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            if (_state == null) return;

            var wallet = _state.Wallet;
            if (!force && _hasLast && WalletEquals(wallet, _last))
                return;

            _last = wallet;
            _hasLast = true;

            SetText(_redText, _redFormat, wallet.red);
            SetText(_greenText, _greenFormat, wallet.green);
            SetText(_blueText, _blueFormat, wallet.blue);
        }

        private static bool WalletEquals(in TokenAmount a, in TokenAmount b)
            => a.red == b.red && a.green == b.green && a.blue == b.blue;

        private static void SetText(TextMeshProUGUI label, string format, int value)
        {
            if (label == null) return;
            if (string.IsNullOrEmpty(format))
                label.SetText(value.ToString());
            else
                label.SetText(format, value);
        }
    }
}
