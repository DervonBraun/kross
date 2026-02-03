namespace Player
{
    using UnityEngine;

    public interface IFocusBoundsProvider
    {
        bool TryGetFocusBounds(out Bounds bounds);
    }
}