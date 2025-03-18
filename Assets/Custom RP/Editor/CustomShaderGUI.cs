using UnityEditor;
using UnityEngine;

public class CustomShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);
        
        // pass and retrieve parameters to class fields
        // --------------------------------------------
        mEditor     = materialEditor;

        // show the configuration option for emission baking
        // -------------------------------------------------
        ConfigureBakedEmission();
    }

    private void ConfigureBakedEmission()
    {
        EditorGUI.BeginChangeCheck();
        
        mEditor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Object target in mEditor.targets)
            {
                Material material = (Material)target;
                material.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    #region Fields

    // editor related
    // --------------
    private MaterialEditor     mEditor;

    #endregion
}
