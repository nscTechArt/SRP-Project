#pragma once

// -----------------------------------------------------------------------------------
// ---------------------------------------MACROS--------------------------------------
// -----------------------------------------------------------------------------------
#define MIN_PERCEPTUAL_ROUGHNESS 0.045

// -----------------------------------------------------------------------------------
// --------------------------------------STRUCTS--------------------------------------
// -----------------------------------------------------------------------------------
struct BRDF
{
    float3 diffuseColor;
    float3 f0;
    float  roughness;
    float  perceptualRoughness;
    float  fresnel;
};

// -----------------------------------------------------------------------------------
// -------------------------------------FUNCTIONS-------------------------------------
// -----------------------------------------------------------------------------------
BRDF GetBRDF(Surface surface)
{
    BRDF brdf;

    // diffuse
    // -------
    brdf.diffuseColor = surface.color * (1 - surface.metallic);

    // f0
    float dielectricF0 = 0.16 * surface.reflectance * surface.reflectance;
    brdf.f0 = surface.color * surface.metallic + dielectricF0 * (1 - surface.metallic);

    // roughness
    // ---------
    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.perceptualRoughness = clamp(brdf.perceptualRoughness, MIN_PERCEPTUAL_ROUGHNESS, 1.0);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);

    // fresnel
    // -------
    brdf.fresnel = saturate(surface.smoothness + surface.metallic);
    
    return brdf;
}

float D_GGX_Custom(float roughness, float NdotH)
{
    float oneMinusNdotH2 = 1.0 - NdotH * NdotH;
    float a = roughness * NdotH;
    float k = roughness / (oneMinusNdotH2 + a * a);
    float d = k * k * rcp(PI);
    return d;
}

float V_SmithGGXCorrelated(float roughness, float NdotL, float NdotV)
{
    float a2 = Square(roughness);
    float lambdaV = NdotL * sqrt((NdotV - a2 * NdotV) * NdotV + a2);
    float lambdaL = NdotV * sqrt((NdotL - a2 * NdotL) * NdotL + a2);
    float v = 0.5 * rcp(lambdaV + lambdaL);
    return v;
}

float3 F_Schlick_SRP(float3 f0, float f90, float VdotH)
{
    return f0 + (f90 - f0) * pow(1.0 - VdotH, 5.0);
}

float3 F_Schlick_Custom(float3 f0, float VdotH)
{
    float f = pow(1.0 - VdotH, 5.0);
    return f + (1.0 - f) * f0;
}

float3 SpecularLobe(float roughness, float3 f0, float NdotH, float NdotL, float NdotV, float VdotH)
{
    float D = D_GGX_Custom(roughness, NdotH);
    float V = V_SmithGGXCorrelated(roughness, NdotL, NdotV);
    float3 F = F_Schlick_Custom(f0, VdotH);
    return D * V * F;
}

float Fd_Burley(float roughness, float NdotV, float NdotL, float LdotH)
{
    float f90 = 0.5 + 2.0 * roughness * LdotH * LdotH;
    float lightScatter = F_Schlick_SRP(1.0, f90, NdotL);
    float viewScatter = F_Schlick_SRP(1.0, f90, NdotV);
    return lightScatter * viewScatter * rcp(PI);
}

float3 DiffuseLobe(BRDF brdf, float NdotV, float NdotL, float LdotH)
{
    return brdf.diffuseColor * Fd_Burley(brdf.roughness, NdotV, NdotL, LdotH);
}

float3 DirectBRDF(Surface surface, BRDF brdf, Light light)
{
    float NdotL = dot(surface.normal, light.direction);
    if (NdotL <= 0.0) return 0.0;

    float3 h = normalize(surface.viewDir + light.direction);
    
    float NdotH = saturate(dot(surface.normal, h));
    float NdotV = saturate(dot(surface.normal, surface.viewDir));
    float VdotH = saturate(dot(surface.viewDir, h));
    float LdotH = saturate(dot(light.direction, h));
    
    float3 fr = SpecularLobe(brdf.roughness, brdf.f0, NdotH, NdotL, NdotV, VdotH);
    float3 fd = Fd_Burley(brdf.roughness, NdotV, NdotL, LdotH) * brdf.diffuseColor;

    float3 color = fd + fr;
    return color;
}

float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular)
{
    float fresnelStrength = surface.fresnel * Pow4(1.0 - saturate(dot(surface.normal, surface.viewDir)));

    float3 reflection = specular * lerp(brdf.f0, brdf.fresnel, fresnelStrength);
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    
    
    return (diffuse * brdf.diffuseColor + reflection) * surface.occlusion;
}
