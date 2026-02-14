using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(AnimationProgressSystem))]
public partial struct OnChangeAnimationIndexSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (animatorState, clips, bones) in SystemAPI
                     .Query<RefRO<AnimationClipIndex>, DynamicBuffer<AnimationClipBlobReference>,
                         DynamicBuffer<BoneBuffer>>().WithChangeFilter<AnimationClipIndex>()) {
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

        foreach (var (animatorState, clips, bones, time) in SystemAPI
                     .Query<RefRO<AnimationClipIndex>, DynamicBuffer<AnimationClipBlobReference>,
                         DynamicBuffer<BoneBuffer>, RefRO<AnimationTime>>().WithChangeFilter<AnimationTime>()) {
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

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial struct AnimationProgressSystem : ISystem {
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        var job = new AnimationProgressJob { DeltaTime = SystemAPI.Time.DeltaTime };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
    
    [BurstCompile]
    public partial struct AnimationProgressJob : IJobEntity {
        public float DeltaTime;
        
        private void Execute(ref LocalTransform transform, ref AnimationTime time, ref PostTransformMatrix postMatrix, 
            in DynamicBuffer<AnimationBoneCurveBlobReference> buffer, in AnimationBoneLength length, AnimationBoneTag tag) {
            time.Value += DeltaTime;
            if (time.Value > length.Value) {
                var timeTarget = time.Value - length.Value;
                time.Value = timeTarget;
            }
            
            var positionChange = false;
            var rotationChange = false;
            var eulerChange = false;
            var scaleChange = false;

            var position = transform.Position;
            var rotation = transform.Rotation;
            var euler = float3.zero;
            var scale = new float3(1f, 1f, 1f);

            for (int i = 0; i < buffer.Length; i++) {
                var curveRef = buffer[i];
                var type = curveRef.Curve.Value.PropertyType;
                var value = Evaluate(curveRef.Curve, time.Value);
                switch (type) {
                    case PropertyType.PositionX:
                        position.x = value;
                        positionChange = true;
                        break;
                    case PropertyType.PositionY:
                        position.y = value;
                        positionChange = true;
                        break;
                    case PropertyType.PositionZ:
                        position.z = value;
                        positionChange = true;
                        break;
                    case PropertyType.RotationX:
                        rotation.value.x = value;
                        rotationChange = true;
                        break;
                    case PropertyType.RotationY:
                        rotation.value.y = value;
                        rotationChange = true;
                        break;
                    case PropertyType.RotationZ:
                        rotation.value.z = value;
                        rotationChange = true;
                        break;
                    case PropertyType.RotationW:
                        rotation.value.w = value;
                        rotationChange = true;
                        break;
                    case PropertyType.EulerX:
                        euler.x = value;
                        eulerChange = true;
                        break;
                    case PropertyType.EulerY:
                        euler.y = value;
                        eulerChange = true;
                        break;
                    case PropertyType.EulerZ:
                        euler.z = value;
                        eulerChange = true;
                        break;
                    case PropertyType.ScaleX:
                        scale.x = value;
                        scaleChange = true;
                        break;
                    case PropertyType.ScaleY:
                        scale.y = value;
                        scaleChange = true;
                        break;
                    case PropertyType.ScaleZ:
                        scale.z = value;
                        scaleChange = true;
                        break;
                }
            }
            
            if (positionChange) transform.Position = position;
            if (rotationChange) {
                transform.Rotation = math.normalize(rotation);
            }
            if (eulerChange) transform.Rotation = quaternion.Euler(euler);
            if (scaleChange) {
                postMatrix.Value = float4x4.Scale(scale);
            }
        }
        
        [BurstCompile]
        private float Evaluate(in BlobAssetReference<AnimationPropertyCurveBlob> curveRef, float time) {
            ref var keyframes = ref curveRef.Value.Keyframes;
            var keyCount = keyframes.Length;
            if (keyCount == 0) return 0f;
            if (keyCount == 1) return keyframes[0].Value;

            if (time <= keyframes[0].Time) return keyframes[0].Value;
            if (time >= keyframes[keyCount - 1].Time) return keyframes[keyCount - 1].Value;

            // Binary search for the interval
            int low = 0;
            int high = keyCount - 1;
            while (low < high) {
                int mid = (low + high) / 2;
                if (time < keyframes[mid].Time) {
                    high = mid;
                } else {
                    low = mid + 1;
                }
            }

            int index = low - 1; // keyframes[index] <= time < keyframes[index+1]
            if (index < 0 || index >= keyCount - 1) return 0f;

            var k0 = keyframes[index];
            var k1 = keyframes[index + 1];

            float t = (time - k0.Time) / (k1.Time - k0.Time);

            float m0 = k0.OutTangent * (k1.Time - k0.Time);
            float m1 = k1.InTangent * (k1.Time - k0.Time);

            float t2 = t * t;
            float t3 = t2 * t;

            float h00 = 2 * t3 - 3 * t2 + 1;
            float h10 = t3 - 2 * t2 + t;
            float h01 = -2 * t3 + 3 * t2;
            float h11 = t3 - t2;

            return h00 * k0.Value + h10 * m0 + h01 * k1.Value + h11 * m1;
        }
    }
}