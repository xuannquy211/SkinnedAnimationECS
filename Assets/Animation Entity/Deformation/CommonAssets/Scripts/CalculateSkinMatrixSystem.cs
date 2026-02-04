using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[RequireMatchingQueriesForUpdate]
[WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(DeformationsInPresentation))]
partial class CalculateSkinMatrixSystemBase : SystemBase
{
    EntityQuery m_BoneEntityQuery;
    EntityQuery m_RootEntityQuery;

    protected override void OnCreate()
    {
        m_BoneEntityQuery = GetEntityQuery(
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<BoneTag>()
            );

        m_RootEntityQuery = GetEntityQuery(
                ComponentType.ReadOnly<LocalToWorld>(),
                ComponentType.ReadOnly<RootTag>()
            );
    }

    protected override void OnUpdate()
    {
        var boneCount = m_BoneEntityQuery.CalculateEntityCount();
        var bonesLocalToWorld = new NativeParallelHashMap<Entity, float4x4>(boneCount, Allocator.TempJob);
        var bonesLocalToWorldParallel = bonesLocalToWorld.AsParallelWriter();

        var dependency = Dependency;

        var bone = Entities
            .WithName("GatherBoneTransforms")
            .WithAll<BoneTag>()
            .ForEach((Entity entity, in LocalToWorld localToWorld) =>
        {
            bonesLocalToWorldParallel.TryAdd(entity, localToWorld.Value);
        }).ScheduleParallel(dependency);

        var rootCount = m_RootEntityQuery.CalculateEntityCount();
        var rootWorldToLocal = new NativeParallelHashMap<Entity, float4x4>(rootCount, Allocator.TempJob);
        var rootWorldToLocalParallel = rootWorldToLocal.AsParallelWriter();

        var root = Entities
            .WithName("GatherRootTransforms")
            .WithAll<RootTag>()
            .ForEach((Entity entity, in LocalToWorld localToWorld) =>
        {
            rootWorldToLocalParallel.TryAdd(entity, math.inverse(localToWorld.Value));
        }).ScheduleParallel(dependency);

        dependency = JobHandle.CombineDependencies(bone, root);

        dependency = Entities
            .WithName("CalculateSkinMatrices")
            .WithReadOnly(bonesLocalToWorld)
            .WithReadOnly(rootWorldToLocal)
            .ForEach((ref DynamicBuffer<SkinMatrix> skinMatrices, in DynamicBuffer<BindPose> bindPoses, in DynamicBuffer<BoneEntity> bones, in RootEntity root) =>
        {
            if (!bonesLocalToWorld.ContainsKey(root.Value) || bones.Length != skinMatrices.Length)
                return;

            var rootMatrixInv = rootWorldToLocal[root.Value];
            for (int i = 0; i < skinMatrices.Length; i++)
            {
                var boneEntity = bones[i].Value;
                if (!bonesLocalToWorld.ContainsKey(boneEntity))
                    continue;

                var matrix = math.mul(rootMatrixInv, bonesLocalToWorld[boneEntity]);
                matrix = math.mul(matrix, bindPoses[i].Value);

                skinMatrices[i] = new SkinMatrix
                {
                    Value = new float3x4(matrix.c0.xyz, matrix.c1.xyz, matrix.c2.xyz, matrix.c3.xyz)
                };
            }
        }).ScheduleParallel(dependency);

        Dependency = JobHandle.CombineDependencies(
            bonesLocalToWorld.Dispose(dependency),
            rootWorldToLocal.Dispose(dependency)
        );
    }
}

