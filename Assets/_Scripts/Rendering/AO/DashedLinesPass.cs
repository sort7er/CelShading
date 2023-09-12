using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static DashedLinesFeature;

public class DashedLinesPass : ScriptableRenderPass
{

    //The only thing I care about
    private DashedLinesComponent m_dashedLines;

    private Material m_ssaoMaterial;
    private Material m_dashedLinesMaterial;
    private RTHandle m_CameraColorTarget;
    private RTHandle m_CameraDepthTarget;


    private float intensity;
    private float radius;
    private float fallOffDistance;
    private float directLightingStrength;

    //Unity stuff from SSAO script


    // Properties
    //private bool isRendererDeferred => m_Renderer != null && m_Renderer is UniversalRenderer /*&& ((UniversalRenderer)m_Renderer).renderingModeRequested == RenderingMode.Deferred*/;

    // Internal Variables
    internal string profilerTag;

    // Private Variables
    private bool m_SupportsR8RenderTextureFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8);
    private int m_BlueNoiseTextureIndex = 0;
    private float m_BlurRandomOffsetX = 0f;
    private float m_BlurRandomOffsetY = 0f;
    private Material m_Material;
    private Texture2D[] m_BlueNoiseTextures;
    private Vector4[] m_CameraTopLeftCorner = new Vector4[2];
    private Vector4[] m_CameraXExtent = new Vector4[2];
    private Vector4[] m_CameraYExtent = new Vector4[2];
    private Vector4[] m_CameraZExtent = new Vector4[2];
    private RTHandle[] m_SSAOTextures = new RTHandle[4];
    private BlurTypes m_BlurType = BlurTypes.Bilateral;
    private Matrix4x4[] m_CameraViewProjections = new Matrix4x4[2];
    //private ProfilingSampler m_ProfilingSampler = ProfilingSampler.Get(URPProfileId.SSAO);
    private ScriptableRenderer m_Renderer = null;
    private RenderTextureDescriptor m_AOPassDescriptor;
    private ScreenSpaceAmbientOcclusionSettings m_CurrentSettings;

    // Constants
    private const int k_FinalTexID = 3;
    private const string k_SSAOTextureName = "_ScreenSpaceOcclusionTexture";
    private const string k_SSAOAmbientOcclusionParamName = "_AmbientOcclusionParam";

    // Statics
    private static readonly int s_SSAOParamsID = Shader.PropertyToID("_SSAOParams");
    private static readonly int s_SSAOBlueNoiseParamsID = Shader.PropertyToID("_SSAOBlueNoiseParams");
    private static readonly int s_LastKawasePass = Shader.PropertyToID("_LastKawasePass");
    private static readonly int s_BlueNoiseTextureID = Shader.PropertyToID("_BlueNoiseTexture");
    private static readonly int s_CameraViewXExtentID = Shader.PropertyToID("_CameraViewXExtent");
    private static readonly int s_CameraViewYExtentID = Shader.PropertyToID("_CameraViewYExtent");
    private static readonly int s_CameraViewZExtentID = Shader.PropertyToID("_CameraViewZExtent");
    private static readonly int s_ProjectionParams2ID = Shader.PropertyToID("_ProjectionParams2");
    private static readonly int s_KawaseBlurIterationID = Shader.PropertyToID("_KawaseBlurIteration");
    private static readonly int s_CameraViewProjectionsID = Shader.PropertyToID("_CameraViewProjections");
    private static readonly int s_CameraViewTopLeftCornerID = Shader.PropertyToID("_CameraViewTopLeftCorner");

    private static readonly int[] m_BilateralTexturesIndices = { 0, 1, 2, k_FinalTexID };
    private static readonly ShaderPasses[] m_BilateralPasses = { ShaderPasses.BilateralBlurHorizontal, ShaderPasses.BilateralBlurVertical, ShaderPasses.BilateralBlurFinal };
    private static readonly ShaderPasses[] m_BilateralAfterOpaquePasses = { ShaderPasses.BilateralBlurHorizontal, ShaderPasses.BilateralBlurVertical, ShaderPasses.BilateralAfterOpaque };

    private static readonly int[] m_GaussianTexturesIndices = { 0, 1, k_FinalTexID, k_FinalTexID };
    private static readonly ShaderPasses[] m_GaussianPasses = { ShaderPasses.GaussianBlurHorizontal, ShaderPasses.GaussianBlurVertical };
    private static readonly ShaderPasses[] m_GaussianAfterOpaquePasses = { ShaderPasses.GaussianBlurHorizontal, ShaderPasses.GaussianAfterOpaque };

    private static readonly int[] m_KawaseTexturesIndices = { 0, k_FinalTexID };
    private static readonly ShaderPasses[] m_KawasePasses = { ShaderPasses.KawaseBlur };
    private static readonly ShaderPasses[] m_KawaseAfterOpaquePasses = { ShaderPasses.KawaseAfterOpaque };

    // Enums
    private enum BlurTypes
    {
        Bilateral,
        Gaussian,
        Kawase,
    }

    private enum ShaderPasses
    {
        AmbientOcclusion = 0,

        BilateralBlurHorizontal = 1,
        BilateralBlurVertical = 2,
        BilateralBlurFinal = 3,
        BilateralAfterOpaque = 4,

        GaussianBlurHorizontal = 5,
        GaussianBlurVertical = 6,
        GaussianAfterOpaque = 7,

        KawaseBlur = 8,
        KawaseAfterOpaque = 9,
    }

    internal DashedLinesPass(Material dashedLinesMaterial)
    {
        m_CurrentSettings = new ScreenSpaceAmbientOcclusionSettings();

        renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        m_dashedLinesMaterial = dashedLinesMaterial;
    }
    internal bool Setup(ref ScreenSpaceAmbientOcclusionSettings featureSettings, ref ScriptableRenderer renderer, ref Material material, ref Texture2D[] blueNoiseTextures)
    {
        m_BlueNoiseTextures = blueNoiseTextures;
        m_Material = material;
        m_Renderer = renderer;
        m_CurrentSettings = featureSettings;

        VolumeStack stack = VolumeManager.instance.stack;
        m_dashedLines = stack.GetComponent<DashedLinesComponent>();

        intensity = m_dashedLines.intesity.value;
        radius = m_dashedLines.radius.value;
        fallOffDistance = m_dashedLines.fallOffDistance.value;
        directLightingStrength = m_dashedLines.directLightingStrength.value;

        


        // Ask for a Depth or Depth + Normals textures
        switch (m_CurrentSettings.Source)
        {
            case ScreenSpaceAmbientOcclusionSettings.DepthSource.Depth:
                ConfigureInput(ScriptableRenderPassInput.Depth);
                break;
            case ScreenSpaceAmbientOcclusionSettings.DepthSource.DepthNormals:
                ConfigureInput(ScriptableRenderPassInput.Normal);// need depthNormal prepass for forward-only geometry
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Blur settings
        switch (m_CurrentSettings.BlurQuality)
        {
            case ScreenSpaceAmbientOcclusionSettings.BlurQualityOptions.High:
                m_BlurType = BlurTypes.Bilateral;
                break;
            case ScreenSpaceAmbientOcclusionSettings.BlurQualityOptions.Medium:
                m_BlurType = BlurTypes.Gaussian;
                break;
            case ScreenSpaceAmbientOcclusionSettings.BlurQualityOptions.Low:
                m_BlurType = BlurTypes.Kawase;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }


        return m_Material != null
            && intensity > 0.0f
            && radius > 0.0f
            && fallOffDistance > 0.0f;
    }

    /// <inheritdoc/>
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        int downsampleDivider = m_CurrentSettings.Downsample ? 2 : 1;

#if ENABLE_VR && ENABLE_XR_MODULE
                int eyeCount = renderingData.cameraData.xr.enabled && renderingData.cameraData.xr.singlePassEnabled ? 2 : 1;
#else
        int eyeCount = 1;
#endif
        for (int eyeIndex = 0; eyeIndex < eyeCount; eyeIndex++)
        {
            Matrix4x4 view = renderingData.cameraData.GetViewMatrix(eyeIndex);
            Matrix4x4 proj = renderingData.cameraData.GetProjectionMatrix(eyeIndex);
            m_CameraViewProjections[eyeIndex] = proj * view;

            // camera view space without translation, used by SSAO.hlsl ReconstructViewPos() to calculate view vector.
            Matrix4x4 cview = view;
            cview.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
            Matrix4x4 cviewProj = proj * cview;
            Matrix4x4 cviewProjInv = cviewProj.inverse;

            Vector4 topLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1, 1, -1, 1));
            Vector4 topRightCorner = cviewProjInv.MultiplyPoint(new Vector4(1, 1, -1, 1));
            Vector4 bottomLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1, -1, -1, 1));
            Vector4 farCentre = cviewProjInv.MultiplyPoint(new Vector4(0, 0, 1, 1));
            m_CameraTopLeftCorner[eyeIndex] = topLeftCorner;
            m_CameraXExtent[eyeIndex] = topRightCorner - topLeftCorner;
            m_CameraYExtent[eyeIndex] = bottomLeftCorner - topLeftCorner;
            m_CameraZExtent[eyeIndex] = farCentre;
        }

        m_Material.SetVector(s_ProjectionParams2ID, new Vector4(1.0f / renderingData.cameraData.camera.nearClipPlane, 0.0f, 0.0f, 0.0f));
        m_Material.SetMatrixArray(s_CameraViewProjectionsID, m_CameraViewProjections);
        m_Material.SetVectorArray(s_CameraViewTopLeftCornerID, m_CameraTopLeftCorner);
        m_Material.SetVectorArray(s_CameraViewXExtentID, m_CameraXExtent);
        m_Material.SetVectorArray(s_CameraViewYExtentID, m_CameraYExtent);
        m_Material.SetVectorArray(s_CameraViewZExtentID, m_CameraZExtent);

        // Update keywords
        CoreUtils.SetKeyword(m_Material, k_OrthographicCameraKeyword, renderingData.cameraData.camera.orthographic);
        CoreUtils.SetKeyword(m_Material, k_AOBlueNoiseKeyword, false);
        CoreUtils.SetKeyword(m_Material, k_AOInterleavedGradientKeyword, false);


        switch (m_CurrentSettings.AOMethod)
        {
            case ScreenSpaceAmbientOcclusionSettings.AOMethodOptions.BlueNoise:
                CoreUtils.SetKeyword(m_Material, k_AOBlueNoiseKeyword, true);

                m_BlueNoiseTextureIndex = (m_BlueNoiseTextureIndex + 1) % m_BlueNoiseTextures.Length;
                m_BlurRandomOffsetX = UnityEngine.Random.value;
                m_BlurRandomOffsetY = UnityEngine.Random.value;

                Texture2D noiseTexture = m_BlueNoiseTextures[m_BlueNoiseTextureIndex];
                m_Material.SetTexture(s_BlueNoiseTextureID, noiseTexture);



                m_Material.SetVector(s_SSAOParamsID, new Vector4(
                    intensity,    // Intensity
                    radius * 1.5f,// Radius
                    1.0f / downsampleDivider,       // Downsampling
                    fallOffDistance       // Falloff
                ));

                m_Material.SetVector(s_SSAOBlueNoiseParamsID, new Vector4(
                    renderingData.cameraData.camera.pixelWidth / (float)noiseTexture.width,    // X Scale
                    renderingData.cameraData.camera.pixelHeight / (float)noiseTexture.height,  // Y Scale
                    m_BlurRandomOffsetX,                                                // X Offset
                    m_BlurRandomOffsetY                                                 // Y Offset
                ));
                break;
            case ScreenSpaceAmbientOcclusionSettings.AOMethodOptions.InterleavedGradient:
                CoreUtils.SetKeyword(m_Material, k_AOInterleavedGradientKeyword, true);


                // Update SSAO parameters in the material
                m_Material.SetVector(s_SSAOParamsID, new Vector4(
                    intensity,// Intensity
                    radius * 1.5f,   // Radius
                    1.0f / downsampleDivider,   // Downsampling
                    fallOffDistance   // Falloff
                ));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        CoreUtils.SetKeyword(m_Material, k_SampleCountLowKeyword, false);
        CoreUtils.SetKeyword(m_Material, k_SampleCountMediumKeyword, false);
        CoreUtils.SetKeyword(m_Material, k_SampleCountHighKeyword, false);
        switch (m_CurrentSettings.Samples)
        {
            case ScreenSpaceAmbientOcclusionSettings.AOSampleOption.High:
                CoreUtils.SetKeyword(m_Material, k_SampleCountHighKeyword, true);
                break;
            case ScreenSpaceAmbientOcclusionSettings.AOSampleOption.Medium:
                CoreUtils.SetKeyword(m_Material, k_SampleCountMediumKeyword, true);
                break;
            default:
                CoreUtils.SetKeyword(m_Material, k_SampleCountLowKeyword, true);
                break;
        }
        CoreUtils.SetKeyword(m_Material, k_OrthographicCameraKeyword, renderingData.cameraData.camera.orthographic);

        // Set the source keywords...
        if (m_CurrentSettings.Source == ScreenSpaceAmbientOcclusionSettings.DepthSource.Depth)
        {
            CoreUtils.SetKeyword(m_Material, k_SourceDepthNormalsKeyword, false);
            switch (m_CurrentSettings.NormalSamples)
            {
                case ScreenSpaceAmbientOcclusionSettings.NormalQuality.Low:
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthLowKeyword, true);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthMediumKeyword, false);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthHighKeyword, false);
                    break;
                case ScreenSpaceAmbientOcclusionSettings.NormalQuality.Medium:
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthLowKeyword, false);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthMediumKeyword, true);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthHighKeyword, false);
                    break;
                case ScreenSpaceAmbientOcclusionSettings.NormalQuality.High:
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthLowKeyword, false);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthMediumKeyword, false);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthHighKeyword, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else
        {
            CoreUtils.SetKeyword(m_Material, k_SourceDepthLowKeyword, false);
            CoreUtils.SetKeyword(m_Material, k_SourceDepthMediumKeyword, false);
            CoreUtils.SetKeyword(m_Material, k_SourceDepthHighKeyword, false);
            CoreUtils.SetKeyword(m_Material, k_SourceDepthNormalsKeyword, true);
        }

        // Set up the descriptors
        RenderTextureDescriptor descriptor = cameraTargetDescriptor;
        descriptor.msaaSamples = 1;
        descriptor.depthBufferBits = 0;

        // AO PAss
        m_AOPassDescriptor = descriptor;
        m_AOPassDescriptor.width /= downsampleDivider;
        m_AOPassDescriptor.height /= downsampleDivider;
        bool useRedComponentOnly = m_SupportsR8RenderTextureFormat && m_BlurType > BlurTypes.Bilateral;
        m_AOPassDescriptor.colorFormat = useRedComponentOnly ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

        // Allocate textures for the AO and blur
        RenderingUtils.ReAllocateIfNeeded(ref m_SSAOTextures[0], m_AOPassDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSAO_OcclusionTexture0");
        RenderingUtils.ReAllocateIfNeeded(ref m_SSAOTextures[1], m_AOPassDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSAO_OcclusionTexture1");
        RenderingUtils.ReAllocateIfNeeded(ref m_SSAOTextures[2], m_AOPassDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSAO_OcclusionTexture2");

        // Upsample setup
        m_AOPassDescriptor.width *= downsampleDivider;
        m_AOPassDescriptor.height *= downsampleDivider;
        m_AOPassDescriptor.colorFormat = m_SupportsR8RenderTextureFormat ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;

        // Allocate texture for the final SSAO results
        RenderingUtils.ReAllocateIfNeeded(ref m_SSAOTextures[k_FinalTexID], m_AOPassDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSAO_OcclusionTexture");

        // Configure targets and clear color
        //ConfigureTarget(m_SSAOTextures[k_FinalTexID]);
        ConfigureClear(ClearFlag.None, Color.white);
    }

    /// <inheritdoc/>
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (m_Material == null)
        {
            Debug.LogErrorFormat("{0}.Execute(): Missing material. ScreenSpaceAmbientOcclusion pass will not execute. Check for missing reference in the renderer resources.", GetType().Name);
            return;
        }

        CommandBuffer cmd = CommandBufferPool.Get();

        using (new ProfilingScope(cmd, new ProfilingSampler("Dashed Lines SSAO")))
        {
            // We only want URP shaders to sample SSAO if After Opaque is off.
            if (!m_CurrentSettings.AfterOpaque)
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ScreenSpaceOcclusion, true);

            //PostProcessUtils.SetSourceSize(cmd, m_AOPassDescriptor);

            cmd.SetGlobalTexture(k_SSAOTextureName, m_SSAOTextures[k_FinalTexID]);
            cmd.SetGlobalTexture("_SSAOTexture", m_SSAOTextures[k_FinalTexID]);

            m_dashedLinesMaterial.SetFloat("_Rotation", m_dashedLines.rotation.value);
            m_dashedLinesMaterial.SetFloat("_Tilling", m_dashedLines.tilling.value);
            m_dashedLinesMaterial.SetColor("_Color", m_dashedLines.color.value);

#if ENABLE_VR && ENABLE_XR_MODULE
                    if (renderingData.cameraData.xr.supportsFoveatedRendering)
                    {
                        // If we are downsampling we can't use the VRS texture
                        // If it's a non uniform raster foveated rendering has to be turned off because it will keep applying non uniform for the other passes.
                        // When calculating normals from depth, this causes artifacts that are amplified from VRS when going to say 4x4. Thus we disable foveated because of that
                        if (m_CurrentSettings.Downsample || SystemInfo.foveatedRenderingCaps == FoveatedRenderingCaps.NonUniformRaster ||
                            (SystemInfo.foveatedRenderingCaps == FoveatedRenderingCaps.FoveationImage && m_CurrentSettings.Source == ScreenSpaceAmbientOcclusionSettings.DepthSource.Depth))
                        {
                            cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Disabled);
                        }
                        // If we aren't downsampling and it's a VRS texture we can apply foveation in this case
                        else if (SystemInfo.foveatedRenderingCaps == FoveatedRenderingCaps.FoveationImage)
                        {
                            cmd.SetFoveatedRenderingMode(FoveatedRenderingMode.Enabled);
                        }
                    }
#endif

            if (m_BlurType == BlurTypes.Kawase)
            {
                cmd.SetGlobalInt(s_LastKawasePass, 1);
                cmd.SetGlobalFloat(s_KawaseBlurIterationID, 0);
            }

            GetPassOrder(m_BlurType, m_CurrentSettings.AfterOpaque, out int[] textureIndices, out ShaderPasses[] shaderPasses);

            // Execute the SSAO
            RTHandle cameraDepthTargetHandle = m_Renderer.cameraDepthTargetHandle;
            RenderAndSetBaseMap(ref cmd, ref renderingData, ref m_Renderer, ref m_Material, ref cameraDepthTargetHandle, ref m_SSAOTextures[0], ShaderPasses.AmbientOcclusion);

            // Execute the Blur Passes
            for (int i = 0; i < shaderPasses.Length; i++)
            {
                int baseMapIndex = textureIndices[i];
                int targetIndex = textureIndices[i + 1];
                RenderAndSetBaseMap(ref cmd, ref renderingData, ref m_Renderer, ref m_Material, ref m_SSAOTextures[baseMapIndex], ref m_SSAOTextures[targetIndex], shaderPasses[i]);
            }

      
            // Set the global SSAO Params

            cmd.SetGlobalVector(k_SSAOAmbientOcclusionParamName, new Vector4(0, 0, 0, directLightingStrength));

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }

    private static void RenderAndSetBaseMap(ref CommandBuffer cmd, ref RenderingData renderingData, ref ScriptableRenderer renderer, ref Material mat, ref RTHandle baseMap, ref RTHandle target, ShaderPasses pass)
    {
        if (IsAfterOpaquePass(ref pass))
            Blitter.BlitCameraTexture(cmd, baseMap, renderer.cameraColorTargetHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, mat, (int)pass);

        else if (baseMap.rt == null)
        {
            // Obsolete usage of RTHandle aliasing a RenderTargetIdentifier
            Vector2 viewportScale = baseMap.useScaling ? new Vector2(baseMap.rtHandleProperties.rtHandleScale.x, baseMap.rtHandleProperties.rtHandleScale.y) : Vector2.one;

            // Will set the correct camera viewport as well.
            CoreUtils.SetRenderTarget(cmd, target);
            Blitter.BlitTexture(cmd, baseMap.nameID, viewportScale, mat, (int)pass);
        }

        else
            Blitter.BlitCameraTexture(cmd, baseMap, target, mat, (int)pass);
    }

    private static void GetPassOrder(BlurTypes blurType, bool isAfterOpaque, out int[] textureIndices, out ShaderPasses[] shaderPasses)
    {
        switch (blurType)
        {
            case BlurTypes.Bilateral:
                textureIndices = m_BilateralTexturesIndices;
                shaderPasses = isAfterOpaque ? m_BilateralAfterOpaquePasses : m_BilateralPasses;
                break;
            case BlurTypes.Gaussian:
                textureIndices = m_GaussianTexturesIndices;
                shaderPasses = isAfterOpaque ? m_GaussianAfterOpaquePasses : m_GaussianPasses;
                break;
            case BlurTypes.Kawase:
                textureIndices = m_KawaseTexturesIndices;
                shaderPasses = isAfterOpaque ? m_KawaseAfterOpaquePasses : m_KawasePasses;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static bool IsAfterOpaquePass(ref ShaderPasses pass)
    {
        return pass == ShaderPasses.BilateralAfterOpaque
               || pass == ShaderPasses.GaussianAfterOpaque
               || pass == ShaderPasses.KawaseAfterOpaque;
    }

    /// <inheritdoc/>
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException("cmd");

        if (!m_CurrentSettings.AfterOpaque)
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ScreenSpaceOcclusion, false);
    }

    public void Dispose()
    {
        m_SSAOTextures[0]?.Release();
        m_SSAOTextures[1]?.Release();
        m_SSAOTextures[2]?.Release();
        m_SSAOTextures[3]?.Release();
    }
}
