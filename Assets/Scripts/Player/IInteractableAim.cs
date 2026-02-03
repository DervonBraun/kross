namespace Player
{
    public interface IInteractableAim
    {
        bool CanInteractAim(PlayerContext context);
        void InteractAim(PlayerContext context);
    }
}