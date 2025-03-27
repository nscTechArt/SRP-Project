#pragma once

// --------------------------------------INCLUDES-------------------------------------

// ---------------------------------------MACROS--------------------------------------

// --------------------------------TEXTURES AND SAMPLERS------------------------------

// --------------------------------------STRUCTS--------------------------------------

// -------------------------------------FUNCTIONS-------------------------------------
float3 Sheen(Surface surface)
{
    // calculate tint
    // --------------
    float luminance = dot(surface.baseColor, float3(0.3, 0.6, 0.1));
    float3 tint = luminance > 0.0 ? surface.baseColor * rcp(luminance) : 1.0;

    // apply sheen tint
    // ----------------
    return lerp(1.0, tint, surface.sheenTint);
}

float SchlickFresnel(float x)
{
    float v = saturate(1.0 - x);
    float v2 = v * v;
    return v2 * v2 * v;
}

float AnisotropicGTR2(float NdotH, float HdotX, float HdotY, float ax, float ay)
{
    float denom = HdotX * HdotX / (ax * ax) + HdotY * HdotY / (ay * ay) + NdotH * NdotH;
    denom *= denom;
    denom *= PI * ax * ay;
    return rcp(denom);
}

float DisneyBRDF(Surface surface, Light light)
{
    // early out
    // ---------
    float3 L = normalize(light.direction);
    float NdotL = dot(surface.normal, L);
    float NdotV = dot(surface.normal, surface.viewDir);
    if (NdotL < 0.0 || NdotV < 0.0) return 0.0;

    float3 H = normalize(L + surface.viewDir);
    float NdotH = saturate(dot(surface.normal, H));
    float LdotH = saturate(dot(L, H));

    // sheen
    // -----
    float3 sheen = Sheen(surface);

    // diffuse
    // -------
    float FL = SchlickFresnel(NdotL);
    float FV = SchlickFresnel(NdotV);
    float Fd90 = 0.5 + 2.0 * surface.roughness * LdotH * LdotH;
    float Fd = lerp(1.0, Fd90, FL) * lerp(1.0, Fd90, FV);

    // subsurface
    // ----------
    float Fss90 = LdotH * LdotH * surface.roughness;
    float Fss = lerp(1.0, Fss90, FL) * lerp(1.0, Fss90, FV);
    float subsurface = 1.25 * (Fss * (1.0 / (NdotL + NdotV) - 0.5) + 0.5);

    // specular
    // --------
    float alpha = surface.roughness * surface.roughness;
    float aspect = sqrt(1.0 - surface.anisotropy * 0.9);
    float alphaX = max(alpha / aspect, 0.001);
    float alphaY = max(alpha * aspect, 0.001);
    // float D = AnisotropicGTR2(NdotH,)
}