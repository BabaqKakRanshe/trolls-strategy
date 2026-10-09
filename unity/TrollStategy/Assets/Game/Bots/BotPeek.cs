using TrollStrategy.Domain;

namespace TrollStrategy.Bots
{
    /// <summary>
    /// A look at the book: no command of the game. It never reaches the session; it costs the player's time (the
    /// look counts as a move), and the show bot's hands open and close the book for it.
    /// </summary>
    public sealed class BotPeek : IGameCommand
    {
    }
}
