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

public struct AnimationSetBlob
{
    public BlobArray<AnimationClipEntityBlob> Clips;
}

public struct AnimationClipBlobReference : IComponentData
{
    public BlobAssetReference<AnimationSetBlob> Blob;
}

// Remove unnecessary components for bone-based logic
// public struct AnimationBoneCurveBlobReference : IBufferElementData ...
// public struct AnimationBoneCurveInitTrigger ...
// public struct AnimationBoneTag ...

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

public enum PropertyType
{
    Unknown,
    PositionX,
    PositionY,
    PositionZ,
    RotationX,
    RotationY,
    RotationZ,
    RotationW,
    ScaleX,
    ScaleY,
    ScaleZ,
    EulerX,
    EulerY,
    EulerZ
}

public struct AnimationBoneLength : IComponentData
{
    public float Value;
}

public struct SkeletonInitialized : IComponentData, IEnableableComponent
{
}

// Previous components are not strictly needed anymore for data copying, 
// but might be useful for other logic or transitions. 
// For now, I'll keep them to minimize breakage, or remove them if unused.
// In the optimized version, we don't need to copy data to bones, so we don't need checks 
// in a separate system. The ProgressSystem runs every frame.
// However, I will keep them for now to avoid compilation errors in other files 
// before I update them. 
// Actually, let's remove them to force cleanup.

// public struct PreviousAnimationClipIndex ...
// public struct PreviousAnimationRootTime ...
// public struct AnimationTime ...
