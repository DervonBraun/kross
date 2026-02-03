namespace Player
{
    public interface IInteractableDefault
    {
        bool CanInteractDefault(PlayerContext context);
        void InteractDefault(PlayerContext context);
    }
}