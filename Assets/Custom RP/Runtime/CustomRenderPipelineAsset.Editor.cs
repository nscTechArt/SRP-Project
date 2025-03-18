public partial class CustomRenderPipelineAsset
{
#if UNITY_EDITOR
    
    // override rendering layer mask names
    // -----------------------------------
    static CustomRenderPipelineAsset()
    {
        mRenderingLayerNames = new string[31];
        for (int i = 0; i < mRenderingLayerNames.Length; i++)
        {
            mRenderingLayerNames[i] = "Layer " + (i + 1);
        }
    }
    private static string[]  mRenderingLayerNames;
    public override string[] renderingLayerMaskNames => mRenderingLayerNames;

#endif
}
