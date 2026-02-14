using System.Collections.Generic;
using UnityEngine;

namespace AN_
{
    public sealed class NotificationLog : MonoBehaviour
    {
        [SerializeField] private NotificationBus _bus;
        [SerializeField, Min(10)] private int _capacity = 200;

        private readonly List<Notification> _items = new();
        public IReadOnlyList<Notification> Items => _items;

        private void Awake()
        {
            if (_bus != null) _bus.Pushed += OnPushed;
        }

        private void OnDestroy()
        {
            if (_bus != null) _bus.Pushed -= OnPushed;
        }

        private void OnPushed(Notification n)
        {
            if (_items.Count >= _capacity) _items.RemoveAt(0);
            _items.Add(n);
        }
    }
}