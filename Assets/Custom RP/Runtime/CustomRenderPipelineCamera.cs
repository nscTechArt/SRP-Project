using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    // camera settings related
    // -----------------------
    [SerializeField] private CameraSettings m_Settings;
    public CameraSettings Settings => m_Settings ??= new CameraSettings();
    
    // camera profiling related
    // ------------------------
    private ProfilingSampler mSampler;
    public  ProfilingSampler Sampler => mSampler ??= new ProfilingSampler(GetComponent<Camera>().name);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnEnable() => mSampler = null;
#endif
}
