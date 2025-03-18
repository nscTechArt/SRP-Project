using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public partial class CustomRenderPipeline
{
    private partial void InitializeForEditor();
    private partial void DisposeForEditor();

#if UNITY_EDITOR

    private partial void InitializeForEditor()
    {
        // override how lightmapper sets up its light data
        // -----------------------------------------------
        Lightmapping.SetDelegate(_LightmappingDelegate);
    }

    private partial void DisposeForEditor()
    {
        // reset lightmapper delegate when pipeline gets disposed
        // ------------------------------------------------------
        Lightmapping.ResetDelegate();
    }
    
    // delegate to a method that
    // transfer data from Light[] to a NativeArray<LightDataGI>
    // --------------------------------------------------------
    private static readonly Lightmapping.RequestLightsDelegate _LightmappingDelegate = (lights, output) =>
    {
        var lightData = new LightDataGI();
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            
            switch (light.type)
            {
                case LightType.Directional:
                    var direLight = new DirectionalLight();
                    LightmapperUtils.Extract(light, ref direLight);
                    lightData.Init(ref direLight);
                    break;
                
                case LightType.Point:
                    var pointLight = new PointLight();
                    LightmapperUtils.Extract(light, ref pointLight);
                    lightData.Init(ref pointLight);
                    break;
                
                case LightType.Spot:
                    var spotLight = new SpotLight();
                    LightmapperUtils.Extract(light, ref spotLight);
                    spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                    spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                    lightData.Init(ref spotLight);
                    break;
                
                case LightType.Area:
                    var rectLight = new RectangleLight();
                    LightmapperUtils.Extract(light, ref rectLight);
                    rectLight.mode = LightMode.Baked;
                    lightData.Init(ref rectLight);
                    break;

                case LightType.Disc:
                default:
                    lightData.InitNoBake(light.GetInstanceID());
                    break;
            }

            lightData.falloff = FalloffType.InverseSquared;
            output[i] = lightData;
        }
    };

#endif
}
