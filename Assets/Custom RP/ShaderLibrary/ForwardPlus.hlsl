#pragma once

// --------------------------------------INCLUDES-------------------------------------

// ---------------------------------------MACROS--------------------------------------

// --------------------------------TEXTURES AND SAMPLERS------------------------------

// --------------------------------------GLOBALS--------------------------------------
// xy: screen UV to tile coordinates
//  z: tile per row, as integer
//  w: tile data size, as integer
float4 _ForwardPlusTileSettings;
StructuredBuffer<int> _ForwardPlusTiles;

// --------------------------------------STRUCTS--------------------------------------
struct ForwardPlusTile
{
    int2 coordinates;
    int  index;

    int GetTileDataSize()
    {
        return asint(_ForwardPlusTileSettings.w);
    }

    int GetHeaderIndex()
    {
        return index * GetTileDataSize();
    }

    int GetLightCount()
    {
        return _ForwardPlusTiles[GetHeaderIndex()];
    }

    int GetFirstLightIndexInTile()
    {
        return GetHeaderIndex() + 1;
    }

    int GetLastLightIndexInTile()
    {
        return GetHeaderIndex() + GetLightCount();
    }

    int GetLightIndex(int lightIndexInTile)
    {
        return _ForwardPlusTiles[lightIndexInTile];
    }

    bool IsMinimumEdgePixel(float2 screenUV)
    {
        float2 startUV = coordinates / _ForwardPlusTileSettings.xy;
        return any(screenUV - startUV < _CameraAttachmentSize.xy);
    }

    int GetMaxLightsPerTile()
    {
        return GetTileDataSize() - 1;
    }

    int2 GetScreenSize()
    {
        return int2(round(_CameraAttachmentSize.zw / _ForwardPlusTileSettings.xy));
    }
};

// -------------------------------------FUNCTIONS-------------------------------------
ForwardPlusTile GetForwardPlusTile(float2 screenUV)
{
    ForwardPlusTile tile;
    tile.coordinates = int2(screenUV * _ForwardPlusTileSettings.xy);
    tile.index = tile.coordinates.y * asint(_ForwardPlusTileSettings.z) + tile.coordinates.x;
    return tile;
}