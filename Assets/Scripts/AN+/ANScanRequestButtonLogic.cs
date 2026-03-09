using UnityEngine;
using Tasks;

namespace AN_
{
    public sealed class ANScanRequestButtonLogic : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private ScannableItemDef _item;
        [SerializeField] private ANRequestDef _requestOverride;
        
        [Header("Layout")]
        [SerializeField] private float preferredWidth = 300f;
        [SerializeField] private float preferredHeight = 120f;

        public float PreferredWidth => preferredWidth;
        public float PreferredHeight => preferredHeight;


        [Header("Behavior")]
        [SerializeField] private bool _hideIfNotAnalyzeType = true;

        private ANRequestDef _request;

        public ScannableItemDef Item => _item;
        public ANRequestDef Request => _request;

        private void Awake()
        {
            _request = _requestOverride != null ? _requestOverride :
                _item != null ? _item.analyzeRequest : null;
        }

        public bool ShouldShow(GameState state)
        {
            if (state == null || _item == null || _request == null) return false;

            if (_hideIfNotAnalyzeType && _request.type != ANRequestType.AnalyzeItem)
                return false;

            return state.IsItemScanned(_item.id) && !state.IsItemAnalyzed(_item.id);
        }
    }
}