using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor: LightEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        
        RenderingLayerMaskDrawer.Draw(settings.renderingLayerMask, _RenderingLayerMaskLabel);

        // draw the inner and outer spot angle
        // -----------------------------------
        bool shouldDrawAngles = !settings.lightType.hasMultipleDifferentValues;
        shouldDrawAngles &= (LightType)settings.lightType.enumValueIndex == LightType.Spot;
        if (shouldDrawAngles)
        {
            settings.DrawInnerAndOuterSpotAngle();
        }
        
        settings.ApplyModifiedProperties();
    }
    
    private static readonly GUIContent _RenderingLayerMaskLabel = new ("Rendering Layer Mask", "Functional version of above property");
}

