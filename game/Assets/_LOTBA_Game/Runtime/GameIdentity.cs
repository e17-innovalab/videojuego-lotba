namespace Lotba.Game
{
    /// <summary>Single source of truth for the player-facing game name and its approval status.</summary>
    public static class GameIdentity
    {
        public const string Title = "Frená a Tiempo";
        public const bool IsProvisional = true;
        public static string DevelopmentLabel => IsProvisional ? "DEMO · NOMBRE PROVISIONAL" : "DEMO";
        public static string FooterLabel => IsProvisional ? "NOMBRE PROVISIONAL" : string.Empty;
    }
}
