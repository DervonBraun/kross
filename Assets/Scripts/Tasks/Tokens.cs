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

        public override string ToString() => $"R:{red} G:{green} B:{blue}";
    }
}