using UnityEngine;

namespace AN_
{
    [CreateAssetMenu(menuName = "KROSS/AN+/Scannable Item")]
    public class ScannableItemDef : ScriptableObject
    {
        public string id;
        public string displayName;

        [TextArea(3, 10)]
        public string filePayload;     // “файл” для анализа (пока строка)

        public Sprite icon;

        [Header("AN+")]
        public ANRequestDef analyzeRequest; // какой запрос доступен для этого предмета
    }
}