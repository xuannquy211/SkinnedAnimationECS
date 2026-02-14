using System;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

// Components moved to AnimationComponents.cs

public class AnimationEntityBaker : Baker<AnimationClipAuthoring>
{
    public override void Bake(AnimationClipAuthoring authoring)
    {
        if (authoring.animationClipEntities == null || authoring.animationClipEntities.Length == 0)
            return;

        var entity = GetEntity(TransformUsageFlags.Dynamic);
        var boneBuffer = AddBuffer<BoneBuffer>(entity);

        // Map transform path to bone entity and index
        var bonePathToIndex = new System.Collections.Generic.Dictionary<string, int>();

        // First pass: Collect all unique bone paths across all clips to populate BoneBuffer
        // This ensures the bone buffer has all necessary bones. 
        // Optimization: In a real production scenario, we might want to ensure the order is consistent or hierarchical.
        // For now, simple collection.
        foreach (var clip in authoring.animationClipEntities)
        {
            foreach (var transformCurve in clip.transformCurves)
            {
                if (!bonePathToIndex.ContainsKey(transformCurve.transformPath))
                {
                    bonePathToIndex[transformCurve.transformPath] = boneBuffer.Length;
                    var boneEntity = GetEntity(transformCurve.transformTarget, TransformUsageFlags.Dynamic);
                    boneBuffer.Add(new BoneBuffer { Bone = boneEntity });
                    // Cannot add components to other entities here. Handled in SkeletonInitializationSystem.
                }
            }
        }

        using (var builder = new BlobBuilder(Allocator.Temp))
        {
            ref var animationSet = ref builder.ConstructRoot<AnimationSetBlob>();
            var clipsArray = builder.Allocate(ref animationSet.Clips, authoring.animationClipEntities.Length);

            for (int i = 0; i < authoring.animationClipEntities.Length; i++)
            {
                var clip = authoring.animationClipEntities[i];
                ref var clipBlob = ref clipsArray[i];

                clipBlob.Name = clip.name;
                clipBlob.Length = clip.length;

                var transformCurvesArray = builder.Allocate(ref clipBlob.TransformCurves, clip.transformCurves.Length);

                for (var j = 0; j < clip.transformCurves.Length; ++j)
                {
                    var transformCurve = clip.transformCurves[j];
                    ref var transformCurveBlob = ref transformCurvesArray[j];

                    // Use the pre-calculated bone index
                    transformCurveBlob.BoneIndex = bonePathToIndex[transformCurve.transformPath];

                    var curvesArray = builder.Allocate(ref transformCurveBlob.Curves, transformCurve.curves.Length);

                    for (var k = 0; k < transformCurve.curves.Length; ++k)
                    {
                        var curve = transformCurve.curves[k];
                        ref var curveBlob = ref curvesArray[k];

                        curveBlob.PropertyType = curve.propertyType;

                        var keyframesArray = builder.Allocate(ref curveBlob.Keyframes, curve.keyframes.Length);

                        for (var l = 0; l < curve.keyframes.Length; ++l)
                        {
                            var keyframe = curve.keyframes[l];
                            keyframesArray[l] = new KeyframeBlob
                            {
                                Time = keyframe.time,
                                Value = keyframe.value,
                                InTangent = keyframe.inTangent,
                                OutTangent = keyframe.outTangent
                            };
                        }
                    }
                }
            }

            var blobRef = builder.CreateBlobAssetReference<AnimationSetBlob>(Allocator.Persistent);
            AddBlobAsset(ref blobRef, out _);
            AddComponent(entity, new AnimationClipBlobReference { Blob = blobRef });
        }

        AddComponent<AnimationClipIndex>(entity);
        AddComponent<AnimationRootTime>(entity);
        
        // Removed obsolete components:
        // PreviousAnimationClipIndex, PreviousAnimationRootTime, AnimationBoneCurveInitTrigger
        // AnimationTime, AnimationBoneLength are also not added here anymore.
    }
}