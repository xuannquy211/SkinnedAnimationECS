using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
[UpdateBefore(typeof(AnimationProgressSystem))] 
public partial struct AnimationCrossFadeSystem : ISystem {
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AnimationCrossFade>();
    }

    public void OnUpdate(ref SystemState state) {
        var job = new AnimationCrossFadeJob { 
            DeltaTime = SystemAPI.Time.DeltaTime,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(false)
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct AnimationCrossFadeJob : IJobEntity {
        public float DeltaTime;
        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> LocalTransformLookup;
        [NativeDisableParallelForRestriction] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;

        private void Execute(Entity entity, EnabledRefRW<AnimationCrossFade> crossFadeEnabled, ref AnimationCrossFade crossFade, ref AnimationClipIndex clipIndex, ref AnimationRootTime rootTime, in AnimationClipBlobReference blobRef, in DynamicBuffer<BoneBuffer> boneBuffer, ref DynamicBuffer<CrossFadeBoneData> crossFadeBoneBuffer) {
            
            if (!blobRef.Blob.IsCreated) return;
            ref var animationSet = ref blobRef.Blob.Value;
            
            // Initialization Phase: Capture current pose
            if (!crossFade.Initialized) {
                if (crossFadeBoneBuffer.Length < boneBuffer.Length) {
                    crossFadeBoneBuffer.ResizeUninitialized(boneBuffer.Length);
                }

                for (int i = 0; i < boneBuffer.Length; i++) {
                    Entity bone = boneBuffer[i].Bone;
                    if (LocalTransformLookup.HasComponent(bone)) {
                        var transform = LocalTransformLookup[bone];
                        float3 scale = new float3(1, 1, 1);
                        if (PostTransformMatrixLookup.HasComponent(bone)) {
                             // Extract scale from matrix if possible, or just assume identity for now if complex.
                             // For simplicity and standard usage, assuming uniform scale or usually 1.
                             // Properly extracting scale from 4x4 matrix:
                             // float4x4 m = PostTransformMatrixLookup[bone].Value;
                             // scale = new float3(math.length(m.c0.xyz), math.length(m.c1.xyz), math.length(m.c2.xyz));
                        }
                        
                        crossFadeBoneBuffer[i] = new CrossFadeBoneData {
                            Position = transform.Position,
                            Rotation = transform.Rotation,
                            Scale = scale // Simplify scale for now
                        };
                    }
                }
                crossFade.Initialized = true;
                crossFade.Timer = 0f;
            }

            // Update Timer
            crossFade.Timer += DeltaTime;
            float t = math.clamp(crossFade.Timer / crossFade.Duration, 0f, 1f);

            // Logic: Blend from Captured Pose -> Target Animation at Time 0
            if (crossFade.TargetAnimationIndex >= 0 && crossFade.TargetAnimationIndex < animationSet.Clips.Length) {
                ref var targetClip = ref animationSet.Clips[crossFade.TargetAnimationIndex];
                
                // Get Target Pose at Time 0
                for (int i = 0; i < targetClip.TransformCurves.Length; i++) {
                    ref var transformCurve = ref targetClip.TransformCurves[i];
                    int boneIndex = transformCurve.BoneIndex;

                    if (boneIndex < 0 || boneIndex >= boneBuffer.Length) continue;
                    
                    // Start Pose (from capture)
                    var startPose = crossFadeBoneBuffer[boneIndex];
                    
                    // Target Pose (Time = 0)
                    float3 targetPos = startPose.Position;
                    quaternion targetRot = startPose.Rotation;
                    float3 targetScale = startPose.Scale;

                    // Evaluate curve at time 0
                    for (int j = 0; j < transformCurve.Curves.Length; j++) {
                        ref var curve = ref transformCurve.Curves[j];
                        float value = Evaluate(ref curve, 0f); // Time 0

                        switch (curve.PropertyType) {
                            case PropertyType.PositionX: targetPos.x = value; break;
                            case PropertyType.PositionY: targetPos.y = value; break;
                            case PropertyType.PositionZ: targetPos.z = value; break;
                            case PropertyType.RotationX: targetRot.value.x = value; break;
                            case PropertyType.RotationY: targetRot.value.y = value; break;
                            case PropertyType.RotationZ: targetRot.value.z = value; break;
                            case PropertyType.RotationW: targetRot.value.w = value; break;
                            case PropertyType.ScaleX: targetScale.x = value; break;
                            case PropertyType.ScaleY: targetScale.y = value; break;
                            case PropertyType.ScaleZ: targetScale.z = value; break;
                        }
                    }
                    if (math.lengthsq(targetRot.value) > 0.0001f) targetRot = math.normalize(targetRot);


                    // Lerp
                    float3 newPos = math.lerp(startPose.Position, targetPos, t);
                    quaternion newRot = math.slerp(startPose.Rotation, targetRot, t);
                    // Scale lerp if needed

                    // Apply to bone
                    Entity boneEntity = boneBuffer[boneIndex].Bone;
                    if (LocalTransformLookup.HasComponent(boneEntity)) {
                        var transform = LocalTransformLookup[boneEntity];
                        transform.Position = newPos;
                        transform.Rotation = newRot;
                        LocalTransformLookup[boneEntity] = transform;
                    }
                }
            }
            
            // Completion
            if (crossFade.Timer >= crossFade.Duration) {
                 clipIndex.Value = crossFade.TargetAnimationIndex;
                 rootTime.Value = 0f;
                 crossFadeEnabled.ValueRW = false; // Disable this system's effect
            }
        }
        
         private float Evaluate(ref AnimationPropertyCurveBlob curve, float time) {
            ref var keyframes = ref curve.Keyframes;
            if (keyframes.Length == 0) return 0f;
            return keyframes[0].Value; // At time 0, usually the first keyframe (or close to it)
        }
    }
}
