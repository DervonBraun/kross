using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// Extension-методы для TokenAmount, используемые системой heat.
    /// </summary>
    public static class TokenAmountExtensions
    {
        /// <summary>
        /// Возвращает новый TokenAmount, в котором все три валюты умножены на <paramref name="multiplier"/>.
        /// Результат округляется вниз (Floor), минимум 0.
        /// </summary>
        public static TokenAmount Scale(this in TokenAmount amount, float multiplier) => new()
        {
            red   = Mathf.Max(0, Mathf.FloorToInt(amount.red   * multiplier)),
            green = Mathf.Max(0, Mathf.FloorToInt(amount.green * multiplier)),
            blue  = Mathf.Max(0, Mathf.FloorToInt(amount.blue  * multiplier)),
        };
    }
}