using UnityEngine;
using UnityEngine.UI;

namespace AN_
{
    [AddComponentMenu("Layout/Flow Layout Group")]
    public sealed class FlowLayoutGroup : LayoutGroup
    {
        [SerializeField] private float spacingX = 16f;
        [SerializeField] private float spacingY = 16f;

        [Header("Stability")]
        [SerializeField] private bool forceNonStretchAnchors = true;
        [SerializeField] private bool forceAnchors = true;
        [SerializeField] private Vector2 forcedAnchor = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 forcedPivot  = new Vector2(0.5f, 0.5f);


        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
        }
        private void Place(RectTransform child, float x, float y, float w, float h)
        {
            // 1) якоря и pivot задаём сами и они НЕ будут “драйвиться” SetChildAlongAxis
            if (forceAnchors)
            {
                child.anchorMin = forcedAnchor;
                child.anchorMax = forcedAnchor;
                child.pivot = forcedPivot;
            }

            // 2) размер
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

            // 3) позиция: LayoutGroup работает в “y вниз”, а anchoredPosition.y вверх.
            // если forcedAnchor=(0.5,0.5) и pivot=(0.5,0.5), то позиция = (x + w/2, -(y + h/2)) относительно якоря.
            float px = x + w * child.pivot.x;
            float py = -(y + h * (1f - child.pivot.y)); // для pivot 0.5 даст -(y + h*0.5)

            child.anchoredPosition = new Vector2(px, py);
        }


        public override void CalculateLayoutInputVertical()
        {
            float width = rectTransform.rect.width;
            if (width <= 0f)
            {
                SetLayoutInputForAxis(0, 0, -1, 1);
                return;
            }

            float limit = width - padding.right;
            float x = padding.left;
            float y = padding.top;

            float rowHeight = 0f;
            float maxY = y;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                NormalizeChild(child);

                float cw = LayoutUtility.GetPreferredSize(child, 0);
                float ch = LayoutUtility.GetPreferredSize(child, 1);

                if (cw <= 0.01f) cw = child.rect.width;
                if (ch <= 0.01f) ch = child.rect.height;

                if (x > padding.left && x + cw > limit)
                {
                    x = padding.left;
                    y += rowHeight + spacingY;
                    rowHeight = 0f;
                }

                rowHeight = Mathf.Max(rowHeight, ch);
                x += cw + spacingX;
                maxY = Mathf.Max(maxY, y + rowHeight);
            }

            float preferredHeight = maxY + padding.bottom;
            SetLayoutInputForAxis(preferredHeight, preferredHeight, -1, 1);
        }

        // не делаем DoLayout дважды
        public override void SetLayoutHorizontal() { }
        public override void SetLayoutVertical() => DoLayout();

        private void DoLayout()
        {
            float width = rectTransform.rect.width;
            if (width <= 0f) return;

            float limit = width - padding.right;
            float x = padding.left;
            float y = padding.top;
            float rowHeight = 0f;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                NormalizeChild(child);

                float cw = LayoutUtility.GetPreferredSize(child, 0);
                float ch = LayoutUtility.GetPreferredSize(child, 1);

                if (cw <= 0.01f) cw = child.rect.width;
                if (ch <= 0.01f) ch = child.rect.height;

                if (x > padding.left && x + cw > limit)
                {
                    x = padding.left;
                    y += rowHeight + spacingY;
                    rowHeight = 0f;
                }
                

                NormalizeChild(child);
                Place(child, x, y, cw, ch);

                rowHeight = Mathf.Max(rowHeight, ch);
                x += cw + spacingX;
            }
        }

        private void NormalizeChild(RectTransform child)
        {
            if (!forceAnchors) return;

            // ставим якоря в центр, без stretch
            child.anchorMin = forcedAnchor;
            child.anchorMax = forcedAnchor;
            child.pivot = forcedPivot;
        }

    }
}
