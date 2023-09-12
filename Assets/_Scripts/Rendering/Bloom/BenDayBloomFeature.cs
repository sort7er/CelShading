using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class BenDayBloomFeature : ScriptableRendererFeature
{

    [SerializeField] private Shader m_bloomShader;
    [SerializeField] private Shader m_benDayBloomShader;

    private Material m_bloomMaterial;
    private Material m_benDayBloomMaterial;


    private BenDayBloomPass m_benDayBloomPass;

    public override void Create()
    {
        m_bloomMaterial = CoreUtils.CreateEngineMaterial(m_bloomShader);
        m_benDayBloomMaterial = CoreUtils.CreateEngineMaterial(m_benDayBloomShader);

        m_benDayBloomPass = new BenDayBloomPass(m_bloomMaterial, m_benDayBloomMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_benDayBloomPass);
    }



    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_bloomMaterial);
        CoreUtils.Destroy(m_benDayBloomMaterial);
    }
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if(renderingData.cameraData.cameraType == CameraType.Game)
        {
            m_benDayBloomPass.ConfigureInput(ScriptableRenderPassInput.Color);
            m_benDayBloomPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            m_benDayBloomPass.SetTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
        }
    }
}
