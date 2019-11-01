﻿using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ScaledRenderSystem : JobComponentSystem
{
    private EntityQuery gridQuery;
    private DynamicBuffer<GridTile> grid;
    private int gridSize;
    
    protected override void OnCreate()
    {
        // Cache the BeginInitializationEntityCommandBufferSystem in a field, so we don't have to create it every frame
        EntityQueryDesc queryDescription = new EntityQueryDesc();
        queryDescription.All = new[] {ComponentType.ReadOnly<GridTile>(), ComponentType.ReadOnly<GridComponent>()};
        gridQuery = GetEntityQuery(queryDescription);
    }
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        // Bug - dynamic buffers get blown away by the entity debugger - Daniel
        //if (!grid.IsCreated)
        {
            var gridEntity = gridQuery.GetSingletonEntity();
            grid = EntityManager.GetBuffer<GridTile>(gridEntity);
            
            var gridComponent = EntityManager.GetComponentData<GridComponent>(gridEntity);
            gridSize = gridComponent.Size;
        }

        var localGrid = grid;
        var localGridSize = gridSize;
        
        var jobHandle = Entities.WithReadOnly(localGrid).WithReadOnly(localGridSize)
            .ForEach((Entity entity, ref Translation translation, ref NonUniformScale scale, in TileRenderer tile, in ScaledRenderer scaling) =>
            {
                int gridLocation = tile.tile.x * localGridSize + tile.tile.y;
                if (gridLocation < localGrid.Length)
                {
                    var value = localGrid[gridLocation];
                    int scaleValue = 0;
                    if (value.IsPlant())
                    {
                        scaleValue = value.GetPlantHealth();
                    }
                    else
                    {
                        scaleValue = value.GetRockHealth();
                    }

                    float lerpValue = (float)scaleValue / (float)scaling.Max;
                    float scaleXZ = scaling.XZScaleAtZero + lerpValue * (scaling.XZScaleAtMax - scaling.XZScaleAtZero);
                    float scaleY = scaling.YScaleAtZero + lerpValue * (scaling.YScaleAtMax - scaling.YScaleAtZero);

                    scale.Value = new float3(scaleXZ, scaleY, scaleXZ);
                    
                    translation.Value = new float3(tile.tile.x, translation.Value.y, tile.tile.y) + new float3(scaleXZ * 0.5f, 0.0f, scaleXZ * 0.5f);

                    if (!value.IsPlant() && !value.IsRock())
                    {
                        scale.Value = new float3(0f,0f,0f);
                    }
                }
            }).Schedule(inputDependencies);

        return jobHandle;
    }
}

