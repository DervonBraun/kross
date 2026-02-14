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
        public bool autoUseAfterPurchase = false; // удобно для MVP


        [Header("Code Output")]
        public CodeType codeType = CodeType.Normal;

        [Header("UI (later)")]
        public Sprite[] attachments;
    }
}