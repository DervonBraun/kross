using System;
using UnityEngine;

namespace AN_
{
    public enum NotifyType
    {
        Info,
        Warning,
        Error,
        Reward,
        OKTS,
        AN
    }

    [Serializable]
    public struct Notification
    {
        public NotifyType type;
        public string title;
        public string message;
        public Sprite icon;
        public float time; // Time.time
    }

    public sealed class NotificationBus : MonoBehaviour
    {
        public event Action<Notification> Pushed;

        public void Push(NotifyType type, string title, string message, Sprite icon = null)
        {
            Debug.Log(type + " : " + title + " : " + message);
            Pushed?.Invoke(new Notification
            {
                type = type,
                title = title,
                message = message,
                icon = icon,
                time = Time.time
            });
        }
    }
}