#pragma once

// -------------------------------------FUNCTIONS-------------------------------------
bool RenderingLayersOverlap(Surface surface, Light light)
{
    return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

float3 LightContribution(Surface surface, Light light)
{
    // calculate light contribution
    // ----------------------------
    float3 lightContribution = light.color * light.attenuation;
    
    // apply Lambert's cosine law
    // --------------------------
    float NdotL = saturate(dot(surface.normal, light.direction));
    lightContribution *= NdotL;
    
    return lightContribution;
}

float3 GetLightingForSingleLight(Surface surface, BRDF brdf, Light light)
{
    return LightContribution(surface, light) * DirectBRDF(surface, brdf, light);
    // return LightContribution(surface, light) * DisneyBRDF(surface, light);
}

float3 GetLighting(Fragment fragment, Surface surface, BRDF brdf, GI gi)
{
    // shadow data for current fragment
    // --------------------------------
    FragmentShadowData fragShadowData = GetFragmentShadowData(surface);
    fragShadowData.shadowMask = gi.shadowMask;
    
    // color starts from brdf.diffuse with gi.diffuse applied
    // ------------------------------------------------------
    float3 color = IndirectBRDF(surface, brdf, gi.diffuse, gi.specular);
    
    // accumulate realtime lighting for all dir lights
    // -----------------------------------------------
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        Light currentLight = GetDirectionalLight(i, surface, fragShadowData);

        // skip light if it doesn't affect the current rendering layer
        if (!RenderingLayersOverlap(surface, currentLight)) continue;
        
        color += GetLightingForSingleLight(surface, brdf, currentLight);
    }

    // accumulate realtime lighting for all other lights
    // -------------------------------------------------
    ForwardPlusTile tile = GetForwardPlusTile(fragment.screenUV);
    int firstLightIndex = tile.GetFirstLightIndexInTile();
    int lasLightIndex = tile.GetLastLightIndexInTile();
    for (int i = firstLightIndex; i <= lasLightIndex; i++)
    {
        Light currentLight = GetOtherLight(tile.GetLightIndex(i), surface, fragShadowData);

        // skip light if it doesn't affect the current rendering layer
        if (!RenderingLayersOverlap(surface, currentLight)) continue;
        
        color += GetLightingForSingleLight(surface, brdf, currentLight);
    }
    
    return color;
}