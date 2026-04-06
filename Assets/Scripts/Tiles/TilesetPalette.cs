using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Zone1_Palette", menuName = "Echo Mortalis/Tileset Palette")]
public class TilesetPalette : ScriptableObject
{
    [System.Serializable]
    public class TileEntry
    {
        public string tileName;
        public Sprite sprite;
        public TileType tileType;
        public bool hasCollision;
        // Runtime tile asset reference — populated by Zone1SetupWizard.
        [HideInInspector] public TileBase tileAsset;
    }

    public enum TileType
    {
        Floor,
        Wall,
        Ceiling,
        Decoration,
        Special
    }

    [Header("Tile Entries")]
    public TileEntry[] tiles;

    // Returns the TileBase for the given tile name, or null if not found.
    public TileBase GetTile(string name)
    {
        if (tiles == null)
            return null;

        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].tileName == name && tiles[i].tileAsset != null)
                return tiles[i].tileAsset;
        }

        return null;
    }

}
