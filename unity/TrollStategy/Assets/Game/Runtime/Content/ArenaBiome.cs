namespace TrollStrategy.Content
{
    /// <summary>
    /// The look around an arena level's board. It never changes the battle's rules; a level whose look has no
    /// prefab yet stands on the meadow.
    /// </summary>
    public enum ArenaBiome { Meadow, Forest, Swamp, Graveyard, MountainPass, Snow }

    /// <summary>
    /// How a level's enemies take the cells of their zone: a wall in front, archers behind, the flanks, a crowd in
    /// the middle. The cells themselves are content (<see cref="BattleMissionDefinition.Enemies"/>); the formation
    /// names them for the arena window and the content checks.
    /// </summary>
    public enum BattleFormation { Wall, ArchersBack, Flanks, Crowd }
}
