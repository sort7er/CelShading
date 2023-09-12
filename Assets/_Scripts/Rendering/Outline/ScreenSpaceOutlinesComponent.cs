using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenuForRenderPipeline("Custom/ Screen Space Outlines", typeof(UniversalRenderPipeline))]
public class ScreenSpaceOutlinesComponent : VolumeComponent, IPostProcessComponent
{
    [Header("General Outline Settings")]
    public NoInterpColorParameter outlineColor = new NoInterpColorParameter(Color.black);
    public ClampedFloatParameter outlineScale = new ClampedFloatParameter(1, 0, 20, true);

    [Header("Depth Settings")]
    public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(1.5f, 0, 100, true);
    public ClampedFloatParameter robertsCrossMultiplier = new ClampedFloatParameter(100, 0, 500, true);

    [Header("Normal Settings")]
    public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.4f, 0, 1, true);

    [Header("Depth Normal Relation Settings")]
    public ClampedFloatParameter steepAngleThreshold = new ClampedFloatParameter(0.2f, 0, 2, true);
    public ClampedFloatParameter steepAngleMultiplier = new ClampedFloatParameter(25f, 0, 500, true);

    public bool IsActive()
    {
        return true;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
}
