using UnityEngine;
using Unity.Entities;
using Unity.Collections;

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

public struct AnimationTime : IComponentData
{
    public float Value;
}

public struct AnimationBoneLength : IComponentData
{
    public float Value;
}

public class AnimationEntityBaker : Baker<AnimationClipAuthoring>
{
    public override void Bake(AnimationClipAuthoring authoring)
    {
        if (authoring.animationClipEntities == null)
            return;

        var entity = GetEntity(TransformUsageFlags.Dynamic);
        var buffer = AddBuffer<AnimationClipBlobReference>(entity);
        var boneBuffer = AddBuffer<BoneBuffer>(entity);

        foreach (var clip in authoring.animationClipEntities)
        {
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<AnimationClipEntityBlob>();

                root.Name = clip.name;
                root.Length = clip.length;

                var transformCurvesArray = builder.Allocate(ref root.TransformCurves, clip.transformCurves.Length);

                for (var i = 0; i < clip.transformCurves.Length; ++i)
                {
                    var transformCurve = clip.transformCurves[i];
                    ref var transformCurveBlob = ref transformCurvesArray[i];

                    transformCurveBlob.BoneIndex = boneBuffer.Length;
                    boneBuffer.Add(new BoneBuffer()
                    {
                        Bone = GetEntity(transformCurve.transformTarget, TransformUsageFlags.Dynamic)
                    });

                    var curvesArray = builder.Allocate(ref transformCurveBlob.Curves, transformCurve.curves.Length);

                    for (var j = 0; j < transformCurve.curves.Length; ++j)
                    {
                        var curve = transformCurve.curves[j];
                        ref var curveBlob = ref curvesArray[j];

                        curveBlob.PropertyType = curve.propertyType;

                        var keyframesArray = builder.Allocate(ref curveBlob.Keyframes, curve.keyframes.Length);

                        for (var k = 0; k < curve.keyframes.Length; ++k)
                        {
                            var keyframe = curve.keyframes[k];
                            keyframesArray[k] = new KeyframeBlob
                            {
                                Time = keyframe.time,
                                Value = keyframe.value,
                                InTangent = keyframe.inTangent,
                                OutTangent = keyframe.outTangent
                            };
                        }
                    }
                }

                var blobRef = builder.CreateBlobAssetReference<AnimationClipEntityBlob>(Allocator.Persistent);
                AddBlobAsset(ref blobRef, out _);
                buffer.Add(new AnimationClipBlobReference { Clip = blobRef });
            }
        }

        AddComponent<AnimationClipIndex>(entity);
        AddComponent<AnimationTime>(entity);
        AddComponent<AnimationBoneCurveInitTrigger>(entity);
    }
}