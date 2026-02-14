using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

internal class SkinnedMeshBaker : Baker<SkinnedMeshRenderer> {
    public override void Bake(SkinnedMeshRenderer skinnedMeshRenderer) {
        if (skinnedMeshRenderer.sharedMesh == null)
            return;

        var entity = GetEntity(TransformUsageFlags.Dynamic);

        // Only execute this if we have a valid skinning setup
        DependsOn(skinnedMeshRenderer.sharedMesh);
        var hasSkinning = skinnedMeshRenderer.bones.Length > 0 && skinnedMeshRenderer.sharedMesh.bindposes.Length > 0;
        if (hasSkinning) {
            // Setup reference to the root bone
            var rootTransform = skinnedMeshRenderer.rootBone
                ? skinnedMeshRenderer.rootBone
                : skinnedMeshRenderer.transform;
            var rootEntity = GetEntity(rootTransform, TransformUsageFlags.Dynamic);
            AddComponent(entity, new RootEntity { Value = rootEntity });
            AddComponent<InitBakingBone>(entity);

            // Setup reference to the other bones
            var boneEntityArray = AddBuffer<BoneEntity>(entity);
            boneEntityArray.ResizeUninitialized(skinnedMeshRenderer.bones.Length);

            for (int boneIndex = 0; boneIndex < skinnedMeshRenderer.bones.Length; ++boneIndex) {
                var bone = skinnedMeshRenderer.bones[boneIndex];
                var boneEntity = GetEntity(bone, TransformUsageFlags.Dynamic);
                boneEntityArray[boneIndex] = new BoneEntity { Value = boneEntity };
            }

            // Store the bindpose for each bone
            var bindPoseArray = AddBuffer<BindPose>(entity);
            bindPoseArray.ResizeUninitialized(skinnedMeshRenderer.bones.Length);

            for (int boneIndex = 0; boneIndex != skinnedMeshRenderer.bones.Length; ++boneIndex) {
                var bindPose = skinnedMeshRenderer.sharedMesh.bindposes[boneIndex];
                bindPoseArray[boneIndex] = new BindPose { Value = bindPose };
            }
        }
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
public partial class ComputeSkinMatricesBakingSystem : SystemBase {
    protected override void OnUpdate() {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        foreach (var (rootEntity, bones, initTrigger, entity) in SystemAPI
                     .Query<RefRO<RootEntity>, DynamicBuffer<BoneEntity>,
                         EnabledRefRW<InitBakingBone>>().WithEntityAccess()) {
            if (!initTrigger.ValueRO) continue;

            ecb.AddComponent<LocalToWorld>(rootEntity.ValueRO.Value);
            // ecb.AddComponent<LocalToWorld>(rootEntity.ValueRO.Value);
            // RootTag and BoneTag are no longer needed
            // ecb.AddComponent<RootTag>(rootEntity.ValueRO.Value);

            for (int boneIndex = 0; boneIndex < bones.Length; ++boneIndex) {
                // var boneEntity = bones[boneIndex].Value;
                // ecb.AddComponent(boneEntity, new BoneTag());
            }

            initTrigger.ValueRW = false;
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}