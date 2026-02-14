using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial struct AnimationProgressSystem : ISystem {
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        var job = new AnimationProgressJob { 
            DeltaTime = SystemAPI.Time.DeltaTime,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(false),
            AnimationBoneLengthLookup = SystemAPI.GetComponentLookup<AnimationBoneLength>(false)
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
    
    [BurstCompile]
    public partial struct AnimationProgressJob : IJobEntity {
        public float DeltaTime;
        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> LocalTransformLookup;
        [NativeDisableParallelForRestriction] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;
        [NativeDisableParallelForRestriction] public ComponentLookup<AnimationBoneLength> AnimationBoneLengthLookup;
        // Actually, with Skeleton Architecture, we use Root Time.
        
        private void Execute(Entity entity, ref AnimationRootTime rootTime, in AnimationClipIndex clipIndex, in AnimationClipBlobReference blobRef, in DynamicBuffer<BoneBuffer> boneBuffer) {
            
            if (!blobRef.Blob.IsCreated) return;
            
            ref var animationSet = ref blobRef.Blob.Value;
            if (clipIndex.Value < 0 || clipIndex.Value >= animationSet.Clips.Length) return;
            
            ref var clip = ref animationSet.Clips[clipIndex.Value];
            
            // Update Root Time
            rootTime.Value += DeltaTime;
            if (rootTime.Value > clip.Length) {
                rootTime.Value = rootTime.Value % clip.Length; // Loop
            }
            
            float currentTime = rootTime.Value;

            // Iterate over transform curves in the clip
            for (int i = 0; i < clip.TransformCurves.Length; i++) {
                ref var transformCurve = ref clip.TransformCurves[i];
                int boneIndex = transformCurve.BoneIndex;
                
                if (boneIndex < 0 || boneIndex >= boneBuffer.Length) continue;
                
                Entity boneEntity = boneBuffer[boneIndex].Bone;
                
                // Ensure bone exists and has LocalTransform
                if (!LocalTransformLookup.HasComponent(boneEntity)) continue;

                var transform = LocalTransformLookup[boneEntity];
                var position = transform.Position;
                var rotation = transform.Rotation;
                var scale = 1f; // Default scale logic - wait, PostTransformMatrix handles scale usually.
                
                bool positionChange = false;
                bool rotationChange = false;
                bool scaleChange = false;
                
                // If PostTransformMatrix exists, use it for scale.
                float3 currentScale = new float3(1, 1, 1);
                bool hasPostMatrix = PostTransformMatrixLookup.HasComponent(boneEntity);
                if (hasPostMatrix) {
                    // Extract scale from matrix? Or just assume 1? 
                    // Usually we overwrite scale if curve exists.
                }

                for (int j = 0; j < transformCurve.Curves.Length; j++) {
                    ref var curve = ref transformCurve.Curves[j];
                    float value = Evaluate(ref curve, currentTime);
                    
                    switch (curve.PropertyType) {
                        case PropertyType.PositionX: position.x = value; positionChange = true; break;
                        case PropertyType.PositionY: position.y = value; positionChange = true; break;
                        case PropertyType.PositionZ: position.z = value; positionChange = true; break;
                        
                        case PropertyType.RotationX: rotation.value.x = value; rotationChange = true; break;
                        case PropertyType.RotationY: rotation.value.y = value; rotationChange = true; break;
                        case PropertyType.RotationZ: rotation.value.z = value; rotationChange = true; break;
                        case PropertyType.RotationW: rotation.value.w = value; rotationChange = true; break;
                        
                        // Euler handling is expensive and complex in burst without extra quaternion math. 
                        // Assuming rotation curves are preferentially used or baking converts to ResampleRotation.
                        // If we must support Euler:
                        // case PropertyType.EulerX: ...
                        
                        case PropertyType.ScaleX: currentScale.x = value; scaleChange = true; break;
                        case PropertyType.ScaleY: currentScale.y = value; scaleChange = true; break;
                        case PropertyType.ScaleZ: currentScale.z = value; scaleChange = true; break;
                    }
                }
                
                if (positionChange) transform.Position = position;
                if (rotationChange) transform.Rotation = math.normalize(rotation); // Normalize to be safe
                
                LocalTransformLookup[boneEntity] = transform;
                
                if (scaleChange && hasPostMatrix) {
                    PostTransformMatrixLookup[boneEntity] = new PostTransformMatrix { Value = float4x4.Scale(currentScale) };
                }
                
                 // Update AnimationBoneLength if needed (for debug or other systems)
                if (AnimationBoneLengthLookup.HasComponent(boneEntity)) {
                     var boneLength = AnimationBoneLengthLookup[boneEntity];
                     boneLength.Value = clip.Length;
                     AnimationBoneLengthLookup[boneEntity] = boneLength;
                }
            }
        }
        
        private float Evaluate(ref AnimationPropertyCurveBlob curve, float time) {
            ref var keyframes = ref curve.Keyframes;
            var keyCount = keyframes.Length;
            if (keyCount == 0) return 0f;
            if (keyCount == 1) return keyframes[0].Value;

            if (time <= keyframes[0].Time) return keyframes[0].Value;
            if (time >= keyframes[keyCount - 1].Time) return keyframes[keyCount - 1].Value;

            // Binary search
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

            int index = low - 1; 
            if (index < 0) index = 0;
            if (index >= keyCount - 1) index = keyCount - 2;

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
