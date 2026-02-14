using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
[UpdateAfter(typeof(ComputeSkinMatricesBakingSystem))]
[BurstCompile]
public partial struct InitiazationBoneAnimation : ISystem {
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var boneAnimationBufferLookup = SystemAPI.GetBufferLookup<AnimationBoneCurveBlobReference>();

        foreach (var (animatorState, clips, bones, boneInitTrigger) in SystemAPI
                     .Query<RefRO<AnimationClipIndex>, DynamicBuffer<AnimationClipBlobReference>,
                         DynamicBuffer<BoneBuffer>, EnabledRefRW<AnimationBoneCurveInitTrigger>>()) {
            var index = animatorState.ValueRO.Value;
            var clip = clips[index];
            var length = clip.Clip.Value.TransformCurves.Length;

            for (var i = 0; i < length; i++) {
                ref var transformCurve = ref clip.Clip.Value.TransformCurves[i];
                var boneIndex = transformCurve.BoneIndex;
                if (!boneAnimationBufferLookup.HasBuffer(bones[boneIndex].Bone)) {
                    ecb.AddBuffer<AnimationBoneCurveBlobReference>(bones[boneIndex].Bone);
                    ecb.AddComponent<AnimationTime>(bones[boneIndex].Bone);
                    ecb.AddComponent<AnimationBoneTag>(bones[boneIndex].Bone);
                    ecb.AddComponent(bones[boneIndex].Bone, new AnimationBoneLength {
                        Value = clip.Clip.Value.Length
                    });
                    ecb.AddComponent(bones[boneIndex].Bone, new PostTransformMatrix { Value = float4x4.identity });
                    boneInitTrigger.ValueRW = true;
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true), UpdateAfter(typeof(InitiazationBoneAnimation))]
[BurstCompile]
public partial struct InitiazationBoneAnimationCurve : ISystem {
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (animatorState, clips, bones, boneTrigger) in SystemAPI
                     .Query<RefRO<AnimationClipIndex>, DynamicBuffer<AnimationClipBlobReference>,
                         DynamicBuffer<BoneBuffer>, EnabledRefRW<AnimationBoneCurveInitTrigger>>()) {
            if (!boneTrigger.ValueRO) continue;

            var index = animatorState.ValueRO.Value;
            var clip = clips[index];
            var length = clip.Clip.Value.TransformCurves.Length;

            for (var i = 0; i < length; i++) {
                ref var transformCurve = ref clip.Clip.Value.TransformCurves[i];
                var boneIndex = transformCurve.BoneIndex;

                var buffer = SystemAPI.GetBuffer<AnimationBoneCurveBlobReference>(bones[boneIndex].Bone);
                var curveCount = transformCurve.Curves.Length;
                buffer.Clear(); // Clear buffer to avoid duplicates if re-init
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
            }

            boneTrigger.ValueRW = false;
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
