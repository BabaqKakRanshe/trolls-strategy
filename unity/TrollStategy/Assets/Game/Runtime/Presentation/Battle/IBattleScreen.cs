using System;
using TrollStrategy.Application;
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

        /// <summary>Shows the deployment for this battle; the screen follows the deployment's changes.</summary>
        void Open(BattleDeployment deployment);

        /// <summary>Answers a refused click or start with the deployment's reason.</summary>
        void Refuse();

        void BeginReplay();
        void ShowReplay(bool paused, float speed, float seconds, int alivePlayers, int aliveEnemies);
        void ShowResult(BattleOutcome outcome, int survived, int fallen, int lostItems);
        void Close();
    }
}
