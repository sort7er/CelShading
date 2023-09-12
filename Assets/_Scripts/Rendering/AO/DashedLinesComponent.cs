using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenuForRenderPipeline("Custom/ Dashed Lines", typeof(UniversalRenderPipeline))]
public class DashedLinesComponent : VolumeComponent, IPostProcessComponent
{
    [Header("SSAO settings")]
    public ColorParameter color = new ColorParameter(Color.white);
    public FloatParameter intesity = new FloatParameter(0.5f, true);
    public FloatParameter radius = new FloatParameter(0.25f, true);
    public FloatParameter rotation = new FloatParameter(0, true);
    public FloatParameter tilling = new FloatParameter(50, true);
    public FloatParameter fallOffDistance = new FloatParameter(100, true);
    public ClampedFloatParameter directLightingStrength = new ClampedFloatParameter(0.25f, 0, 1, true);

    public bool IsActive()
    {
        return true;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
}
