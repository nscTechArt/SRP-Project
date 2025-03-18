using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct ForwardPlusTilesJob : IJobFor
{
    public void Execute(int tileIndex)
    {
        // calculate current tile bounds
        // -----------------------------
        int y = tileIndex / tilesPerRow;
        int x = tileIndex - y * tilesPerRow;
        float4 currentTileBounds = float4(x, y, x + 1, y + 1) * tileScreenUVSize.xyxy;

        // calculate current tile data offset in tileData buffer
        // -----------------------------------------------------
        int headerIndex = tileIndex * tileDataSize;
        int dataIndex = headerIndex;
        
        // find lights that intersect with current tile
        // --------------------------------------------
        int lightsInTileCount = 0;
        for (int i = 0; i < otherLightCount; i++)
        {
            float4 currentLightBounds = lightBounds[i];
            if (all(float4(currentLightBounds.xy, currentTileBounds.xy) <= float4(currentTileBounds.zw, currentLightBounds.zw)))
            {
                tileData[++dataIndex] = i;
                if (++lightsInTileCount >= maxLightsPerTile) break;
            }
        }
        tileData[headerIndex] = lightsInTileCount;
    }

    public int    otherLightCount;
    public int    maxLightsPerTile;
    public int    tilesPerRow;
    public int    tileDataSize;
    public float2 tileScreenUVSize;

    [ReadOnly] 
    public NativeArray<float4> lightBounds;

    [WriteOnly, NativeDisableParallelForRestriction]
    public NativeArray<int> tileData;
}
