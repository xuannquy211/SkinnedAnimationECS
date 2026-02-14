using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

public struct KeyframeBlob
{
    public float Time;
    public float Value;
    public float InTangent;
    public float OutTangent;
}

public struct AnimationPropertyCurveBlob
{
    public PropertyType PropertyType;
    public BlobArray<KeyframeBlob> Keyframes;
}

public struct TransformCurveEntityBlob
{
    public int BoneIndex;
    public BlobArray<AnimationPropertyCurveBlob> Curves;
}

public struct AnimationClipEntityBlob
{
    public FixedString512Bytes Name;
    public float Length;
    public BlobArray<TransformCurveEntityBlob> TransformCurves;
}

public struct AnimationClipBlobReference : IBufferElementData
{
    public BlobAssetReference<AnimationClipEntityBlob> Clip;
}

public struct AnimationBoneCurveBlobReference : IBufferElementData
{
    public BlobAssetReference<AnimationPropertyCurveBlob> Curve;
}

public struct AnimationBoneCurveInitTrigger : IComponentData, IEnableableComponent
{
}

public struct AnimationBoneTag : IComponentData
{
}

public struct BoneBuffer : IBufferElementData
{
    public Entity Bone;
}

public struct AnimationClipIndex : IComponentData
{
    public int Value;
}

public struct AnimationRootTime : IComponentData
{
    public float Value;
}

public struct PreviousAnimationClipIndex : IComponentData
{
    public int Value;
}

public struct PreviousAnimationRootTime : IComponentData
{
    public float Value;
}

public struct AnimationTime : IComponentData
{
    public float Value;
}

public struct AnimationBoneLength : IComponentData
{
    public float Value;
}
