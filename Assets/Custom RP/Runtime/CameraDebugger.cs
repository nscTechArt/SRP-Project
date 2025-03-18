using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public static class CameraDebugger
{
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Initialize(Shader shader)
    {
        mMaterial = CoreUtils.CreateEngineMaterial(shader);

        var opacity = new DebugUI.FloatField()
        {
            displayName = "Opacity",
            tooltip = "The opacity of the debug overlay",
            min = static () => 0.0f,
            max = static () => 1.0f,
            getter = static () => mOpacity,
            setter = static value => mOpacity = value
        };
        var showTiles = new DebugUI.BoolField()
        {
            displayName = "Show Tiles",
            tooltip = "Whether the debug overlay is shown",
            getter = static () => mShowTiles,
            setter = static value => mShowTiles = value
        };
        DebugManager.instance.GetPanel(kPanelName, true).children.Add(showTiles, opacity);
        
    }
    
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Render(RenderGraphContext context)
    {
        CommandBuffer cmd = context.cmd;
        
        cmd.SetGlobalFloat(_DebugOpacity, mOpacity);
        
        cmd.DrawProcedural(Matrix4x4.identity, mMaterial, 0, MeshTopology.Triangles, 3);
        
        context.renderContext.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }
    
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Dispose()
    {
        CoreUtils.Destroy(mMaterial);    
        
        DebugManager.instance.RemovePanel(kPanelName);
    }

    public static bool IsActive => mShowTiles && mOpacity > 0.0f;

    private static bool     mShowTiles;
    private static Material mMaterial;
    private static float    mOpacity = 0.5f;
    
    private static readonly int _DebugOpacity = Shader.PropertyToID("_DebugOpacity");

    private const string kPanelName = "Forward+";
}
