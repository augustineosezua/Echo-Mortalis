using UnityEngine;

public static class GamePersistence
{
    public static int ReturningFromPurgatory_ZoneIndex { get; private set; } = 0;
    public static bool PurgatoryWon { get; private set; } = false;
    public static bool IsReturningFromPurgatory => ReturningFromPurgatory_ZoneIndex > 0;

    public static void SetPurgatoryReturn(int zoneIndex)
    {
        ReturningFromPurgatory_ZoneIndex = Mathf.Max(0, zoneIndex);
        PurgatoryWon = false;
    }

    public static void ResolvePurgatory(bool won)
    {
        PurgatoryWon = won;
    }

    public static bool ShouldRestoreCheckpointForScene(int sceneBuildIndex)
    {
        return PurgatoryWon &&
            ReturningFromPurgatory_ZoneIndex > 0 &&
            ReturningFromPurgatory_ZoneIndex == sceneBuildIndex;
    }

    public static void Reset()
    {
        ReturningFromPurgatory_ZoneIndex = 0;
        PurgatoryWon = false;
    }
}
