using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct SkeletonInitializationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (buffer, entity) in SystemAPI.Query<DynamicBuffer<BoneBuffer>>().WithAll<AnimationClipBlobReference>().WithNone<SkeletonInitialized>().WithEntityAccess())
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                var boneEntity = buffer[i].Bone;
                if (boneEntity == Entity.Null) continue;

                ecb.AddComponent<PostTransformMatrix>(boneEntity);
                ecb.SetComponent(boneEntity, new PostTransformMatrix { Value = float4x4.identity });

                ecb.AddComponent<AnimationBoneLength>(boneEntity);
                ecb.SetComponent(boneEntity, new AnimationBoneLength { Value = 0f });
            }
            ecb.AddComponent<SkeletonInitialized>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
