using UnityEngine;

namespace Tasks
{
    /// <summary>
    /// Вешается на тот же GameObject что и GreenNodePoint.
    /// Автоматически регистрирует/снимает узел в GreenTokenAccumulator
    /// через OnEnable / OnDisable — без ручных ссылок.
    /// </summary>
    [RequireComponent(typeof(GreenNodePoint))]
    public sealed class GreenNodeAutoRegister : MonoBehaviour
    {
        private GreenNodePoint _node;

        private void Awake() => _node = GetComponent<GreenNodePoint>();

        private void OnEnable()
        {
            // Accumulator может появиться чуть позже — ищем с задержкой через Start
        }

        private void Start()
        {
            if (GreenTokenAccumulator.Instance != null)
                GreenTokenAccumulator.Instance.Register(_node);
        }

        private void OnDisable()
        {
            if (GreenTokenAccumulator.Instance != null)
                GreenTokenAccumulator.Instance.Unregister(_node);
        }
    }
}