using System;
using TrollStrategy.Application;
using TrollStrategy.Content;
using TrollStrategy.Domain;

namespace TrollStrategy.Presentation.Battle
{
    /// <summary>
    /// The battle's screen UI as the battle scene sees it: the deployment panels, the replay controls and
    /// the verdict. The UI layer implements it; the bootstrap hands it to the battle.
    /// </summary>
    public interface IBattleScreen
    {
        event Action StartRequested;
        event Action PauseToggled;
        event Action<float> SpeedChosen;
        event Action CloseRequested;
        /// <summary>A kind was dragged out of the reserve and let go; the pointer shows where.</summary>
        event Action<UnitKind> KindDropped;

        /// <summary>Shows the deployment for this battle; the screen follows the deployment's changes.</summary>
        void Open(BattleDeployment deployment);

        /// <summary>
        /// Where a cell of the board shows on the screen (pixels from the top left), or null; the tutorial pointer
        /// finds a fighter on the board by it.
        /// </summary>
        void LocateCells(Func<Cell, UnityEngine.Vector2?> cellToScreen);

        /// <summary>Answers a refused click or start with the deployment's reason.</summary>
        void Refuse();

        void BeginReplay();
        void ShowReplay(bool paused, float speed, int alivePlayers, int aliveEnemies);
        /// <summary>The verdict; <paramref name="cost"/> adds the burnt stake, the rest and a closed level.</summary>
        void ShowResult(BattleOutcome outcome, int survived, int fallen, int lostItems, BattleCost cost = null);
        void Close();
    }
}
