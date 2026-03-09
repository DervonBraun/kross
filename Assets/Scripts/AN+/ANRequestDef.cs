using Player.EffectSystem;
using Tasks;
using UnityEngine;

namespace AN_
{
    public enum ANRequestType
    {
        AnalyzeItem,   // даёт Blue
        GenerateCode   // создаёт PendingCode
    }

    public enum CodeType
    {
        Normal,
        Reset
    }

    [CreateAssetMenu(menuName = "KROSS/AN+/AN Request")]
    public class ANRequestDef : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string title;

        [TextArea(3, 10)]
        public string promptText;

        [Header("Chat Output")]
        [TextArea(5, 20)]
        public string responseText;

        [Header("Type")]
        public ANRequestType type;

        [Header("Cost")]
        public TokenAmount cost;

        [Header("Requirements")]
        public Requirement[] requirements;

        [Header("Analyze Output")]
        [Min(0)] public int blueReward = 0;
        public string loreId;
        public EffectDefinition[] grantEffects;
        public int normalCodeReward = 0;
        public int resetCodeReward = 0;
        public bool autoUseAfterPurchase = false;

        [Header("Code Output")]
        public CodeType codeType = CodeType.Normal;

        [Header("Chat Behavior")]
        public bool createTab = true;
        public bool reuseExistingTab = true;
        public bool closeChatAfterUse = false;

        [Header("Tab UI")]
        public Sprite tabIcon;

        [Header("UI (later)")]
        public Sprite[] attachments;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = name;
        }
#endif
    }
}