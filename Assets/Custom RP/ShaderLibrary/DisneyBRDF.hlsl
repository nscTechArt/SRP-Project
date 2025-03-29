#pragma once

// --------------------------------------INCLUDES-------------------------------------

// ---------------------------------------MACROS--------------------------------------

// --------------------------------TEXTURES AND SAMPLERS------------------------------

// --------------------------------------STRUCTS--------------------------------------

// -------------------------------------FUNCTIONS-------------------------------------
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

float AnisotropicSmithGGX(float NdotS, float SdotX, float SdotY, float ax, float ay) {
    return rcp(NdotS + sqrt(Square(SdotX * ax) + Square(SdotY * ay) + Square(NdotS)));
}

// Isotropic Generalized Trowbridge Reitz with gamma == 1
float GTR1(float NdotH, float a) {
    float a2 = a * a;
    float t = 1.0f + (a2 - 1.0f) * NdotH * NdotH;
    return (a2 - 1.0f) / (PI * log(a2) * t);
}

float SmithGGX(float alphaSquared, float NdotL, float NdotV) {
    float a = NdotV * sqrt(alphaSquared + NdotL * (NdotL - alphaSquared * NdotL));
    float b = NdotL * sqrt(alphaSquared + NdotV * (NdotV - alphaSquared * NdotV));

    return 0.5f / (a + b);
}

float3 DisneyBRDF(Surface surface, Light light)
{
    float3 L = normalize(light.direction);
    float NdotL = dot(surface.normal, L);
    float NdotV = dot(surface.normal, surface.viewDir);
    if (NdotL < 0.0 || NdotV < 0.0) return 0.0;

    float3 H = normalize(L + surface.viewDir);
    float NdotH = saturate(dot(surface.normal, H));
    float LdotH = saturate(dot(L, H));

    // sheen
    // -----
    // calculate tint
    // --------------
    float luminance = dot(surface.baseColor, float3(0.3, 0.6, 0.1));
    float3 tint = luminance > 0.0 ? surface.baseColor * rcp(luminance) : 1.0;
    float3 sheen = lerp(1.0, tint, surface.sheenTint);
    float3 spec0 = lerp(surface.specular * 0.08 * lerp(1.0, tint, surface.specularTint), surface.baseColor, surface.metallic);

    // diffuse
    // -------
    float FL = SchlickFresnel(NdotL);
    float FV = SchlickFresnel(NdotV);
    float Fss90 = surface.roughness * LdotH * LdotH;
    float Fd90 = 0.5 + 2.0 * Fss90;
    float Fd = lerp(1.0, Fd90, FL) * lerp(1.0, Fd90, FV);

    // subsurface
    // ----------
    float Fss = lerp(1.0, Fss90, FL) * lerp(1.0, Fss90, FV);
    float ss = 1.25 * (Fss * (rcp(NdotL + NdotV) - 0.5) + 0.5);

    // specular
    // --------
    float alpha = surface.roughness * surface.roughness;
    float aspectRatio = sqrt(1.0 - surface.anisotropy * 0.9);
    float alphaX = max(alpha / aspectRatio, 0.001);
    float alphaY = max(alpha * aspectRatio, 0.001);
    float Ds = AnisotropicGTR2(NdotH, dot(H, surface.tangent), dot(H, surface.bitangent), alphaX, alphaY);

    // geometry attenuation
    // --------------------
    float GAlphaSquared = Square(0.5 + surface.roughness * 0.5);
    float GAlphaX = max(GAlphaSquared / aspectRatio, 0.01);
    float GAlphaY = max(GAlphaSquared * aspectRatio, 0.01);
    float GL = AnisotropicSmithGGX(NdotL, dot(L, surface.tangent), dot(L, surface.bitangent), GAlphaX, GAlphaY);
    float GV = AnisotropicSmithGGX(NdotV, dot(surface.viewDir, surface.tangent), dot(surface.viewDir, surface.bitangent), GAlphaX, GAlphaY);
    float G = GL * GV;
    // fresnel reflectance
    // -------------------
    float FH = SchlickFresnel(LdotH);
    float3 F = lerp(spec0, 1.0, FH);

    sheen = FH * surface.sheen * sheen;

    // clearcoat
    // ---------
    float Dr = GTR1(NdotH, lerp(0.1, 0.001, surface.clearCoatGloss));
    float Fr = lerp(0.04, 1.0, FH);
    float Gr = SmithGGX(NdotL, NdotV, 0.25);

    float3 diffuse = rcp(PI) * (lerp(Fd, ss, surface.subSurface) * surface.baseColor + sheen) * (1.0 - surface.metallic);
    float3 specular = Ds * F * G;
    float3 clearcoat = 0.25 * surface.clearCoat * Gr * Fr * Dr;

    return diffuse + specular + clearcoat;
}