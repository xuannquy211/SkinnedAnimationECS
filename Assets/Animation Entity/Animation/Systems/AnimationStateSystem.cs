using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AnimationProgressSystem))]
public partial struct OnChangeAnimationIndexSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (animatorState, prevAnimatorState, clips, bones) in SystemAPI
                     .Query<RefRO<AnimationClipIndex>, RefRW<PreviousAnimationClipIndex>, DynamicBuffer<AnimationClipBlobReference>,
                         DynamicBuffer<BoneBuffer>>()) {
            
            if (animatorState.ValueRO.Value == prevAnimatorState.ValueRO.Value)
                continue;

            prevAnimatorState.ValueRW.Value = animatorState.ValueRO.Value;
            
            var index = animatorState.ValueRO.Value;
            if (index < clips.Length)
            {
                var clip = clips[index];
                var length = clip.Clip.Value.TransformCurves.Length;

                for (var i = 0; i < length; i++) {
                    ref var transformCurve = ref clip.Clip.Value.TransformCurves[i];
                    var boneIndex = transformCurve.BoneIndex;

                    var buffer = SystemAPI.GetBuffer<AnimationBoneCurveBlobReference>(bones[boneIndex].Bone);
                    var curveCount = transformCurve.Curves.Length;
                    buffer.Clear();
                    buffer.ResizeUninitialized(curveCount);

                    for (var j = 0; j < curveCount; j++) {
                        ref var curve = ref transformCurve.Curves[j];
                        using var builder = new BlobBuilder(Allocator.Temp);
                        ref var boneCurve = ref builder.ConstructRoot<AnimationPropertyCurveBlob>();

                        boneCurve.PropertyType = curve.PropertyType;

                        var totalKey = curve.Keyframes.Length;
                        var boneCurveBlob = builder.Allocate(ref boneCurve.Keyframes, totalKey);
                        for (var k = 0; k < totalKey; ++k) {
                            boneCurveBlob[k] = curve.Keyframes[k];
                        }

                        var blobRef = builder.CreateBlobAssetReference<AnimationPropertyCurveBlob>(Allocator.Persistent);
                        buffer[j] = new AnimationBoneCurveBlobReference() { Curve = blobRef };
                    }

                    var boneTime = SystemAPI.GetComponentRW<AnimationTime>(bones[boneIndex].Bone);
                    boneTime.ValueRW.Value = 0f;

                    var boneLength = SystemAPI.GetComponentRW<AnimationBoneLength>(bones[boneIndex].Bone);
                    boneLength.ValueRW.Value = clip.Clip.Value.Length;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AnimationProgressSystem))]
public partial struct OnChangeAnimationTimeSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (animatorState, clips, bones, time, prevTime) in SystemAPI
                     .Query<RefRO<AnimationClipIndex>, DynamicBuffer<AnimationClipBlobReference>,
                         DynamicBuffer<BoneBuffer>, RefRO<AnimationRootTime>, RefRW<PreviousAnimationRootTime>>()) {
            
            if (Mathf.Abs(time.ValueRO.Value - prevTime.ValueRO.Value) < Mathf.Epsilon)
                continue;
                
            prevTime.ValueRW.Value = time.ValueRO.Value;

            var index = animatorState.ValueRO.Value;
            if (index < clips.Length)
            {
                var clip = clips[index];
                var length = clip.Clip.Value.TransformCurves.Length;

                for (var i = 0; i < length; i++) {
                    ref var transformCurve = ref clip.Clip.Value.TransformCurves[i];
                    var boneIndex = transformCurve.BoneIndex;
                    var boneTime = SystemAPI.GetComponentRW<AnimationTime>(bones[boneIndex].Bone);
                    boneTime.ValueRW.Value = time.ValueRO.Value;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
