using System;
using UnityEngine;

[Serializable]
public class ForwardPlusSettings
{
    public TileSize m_TileSize;
    [Range(0, 99)] 
    public int      m_MaxLightsPerTile;
    
    public enum TileSize
    {
        Default, _16 = 16, _32 = 32, _64 = 64, _128 = 128, _256 = 256
    }
}
