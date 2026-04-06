using UnityEngine;

public static class CheckpointSystem
{
    public const int DefaultSavedHealth = 100;
    public const int DefaultSavedWeaponIndex = 1;

    public static int LastCheckpointZone = -1;
    public static Vector3 LastCheckpointPosition = Vector3.zero;
    public static int SavedHealth = DefaultSavedHealth;
    public static int SavedWeaponIndex = DefaultSavedWeaponIndex;
    public static bool HasCheckpoint = false;

    public static void Save(int zone, Vector3 position, int hp, int weaponIndex)
    {
        LastCheckpointZone = Mathf.Max(-1, zone);
        LastCheckpointPosition = position;
        SavedHealth = Mathf.Max(1, hp);
        SavedWeaponIndex = Mathf.Max(DefaultSavedWeaponIndex, weaponIndex);
        HasCheckpoint = true;
    }

    public static bool HasCheckpointForScene(int sceneBuildIndex)
    {
        return HasCheckpoint && LastCheckpointZone == sceneBuildIndex;
    }

    public static bool RestorePlayerTo(GameObject player)
    {
        if (!HasCheckpoint || player == null)
            return false;

        player.transform.position = LastCheckpointPosition;
        Health h = player.GetComponent<Health>();
        if (h != null)
            h.SetHealth(SavedHealth);

        PlayerWeaponController w = player.GetComponent<PlayerWeaponController>();
        if (w != null)
            w.SetWeapon(SavedWeaponIndex);

        return true;
    }

    public static void Reset()
    {
        LastCheckpointZone = -1;
        LastCheckpointPosition = Vector3.zero;
        SavedHealth = DefaultSavedHealth;
        SavedWeaponIndex = DefaultSavedWeaponIndex;
        HasCheckpoint = false;
    }
}
