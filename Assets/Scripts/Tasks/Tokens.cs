using System;
using UnityEngine;

namespace Tasks
{
    public enum TaskColor { Red, Green, Blue }

    [Serializable]
    public struct TokenAmount
    {
        [Min(0)] public int red;
        [Min(0)] public int green;
        [Min(0)] public int blue;

        public static TokenAmount Zero => new TokenAmount();

        public bool CanPay(in TokenAmount cost)
            => red >= cost.red && green >= cost.green && blue >= cost.blue;

        public void Add(in TokenAmount add)
        {
            red += add.red;
            green += add.green;
            blue += add.blue;
        }

        public bool TryPay(in TokenAmount cost)
        {
            if (!CanPay(cost)) return false;
            red -= cost.red;
            green -= cost.green;
            blue -= cost.blue;
            return true;
        }

        /// <summary>
        /// Возвращает новый TokenAmount, где каждый канал умножен на multiplier
        /// и округлён до целого (минимум 0).
        /// </summary>
        public TokenAmount Scale(float multiplier) => new TokenAmount
        {
            red   = Mathf.Max(0, Mathf.RoundToInt(red   * multiplier)),
            green = Mathf.Max(0, Mathf.RoundToInt(green * multiplier)),
            blue  = Mathf.Max(0, Mathf.RoundToInt(blue  * multiplier)),
        };

        public override string ToString() => $"R:{red} G:{green} B:{blue}";
    }
}