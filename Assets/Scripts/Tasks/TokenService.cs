namespace Tasks
{
    public static class TokenService
    {
        public static bool CanPay(GameState s, in TokenAmount cost)
            => s != null && s.Wallet.CanPay(cost);

        public static bool TryPay(GameState s, in TokenAmount cost)
            => s != null && s.TryPayTokens(cost);

        public static void Add(GameState s, in TokenAmount add)
        {
            if (s == null) return;
            s.AddTokens(add);
        }
    }
}