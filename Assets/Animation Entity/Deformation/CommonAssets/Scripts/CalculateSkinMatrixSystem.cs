using Unity.Burst;
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
[BurstCompile]
public partial struct CalculateSkinMatrixSystem : ISystem
{
    [ReadOnly] ComponentLookup<LocalToWorld> m_GlobalTransformLookup;

    public void OnCreate(ref SystemState state)
    {
        m_GlobalTransformLookup = state.GetComponentLookup<LocalToWorld>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        m_GlobalTransformLookup.Update(ref state);
        var parallelWriter = m_GlobalTransformLookup;

        var job = new CalculateSkinMatricesJob
        {
            GlobalTransformLookup = parallelWriter
        };
        
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    partial struct CalculateSkinMatricesJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<LocalToWorld> GlobalTransformLookup;

        public void Execute(ref DynamicBuffer<SkinMatrix> skinMatrices, in DynamicBuffer<BindPose> bindPoses, in DynamicBuffer<BoneEntity> bones, in RootEntity root)
        {
            if (!GlobalTransformLookup.HasComponent(root.Value) || bones.Length != skinMatrices.Length)
                return;

            var rootMatrix = GlobalTransformLookup[root.Value].Value;
            var rootMatrixInv = math.inverse(rootMatrix);

            for (int i = 0; i < skinMatrices.Length; i++)
            {
                var boneEntity = bones[i].Value;
                if (!GlobalTransformLookup.HasComponent(boneEntity))
                    continue;

                var boneMatrix = GlobalTransformLookup[boneEntity].Value;
                var matrix = math.mul(rootMatrixInv, boneMatrix);
                matrix = math.mul(matrix, bindPoses[i].Value);

                skinMatrices[i] = new SkinMatrix
                {
                    Value = new float3x4(matrix.c0.xyz, matrix.c1.xyz, matrix.c2.xyz, matrix.c3.xyz)
                };
            }
        }
    }
}
