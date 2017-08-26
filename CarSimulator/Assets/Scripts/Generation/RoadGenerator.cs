using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;


public class RoadGenerator : MonoBehaviour {
	
	public int heightPenalty = 30;
	public int numCheckpoints = 8;
	public int roadWidth = 10;
	public int pathFindingSpacing = 10;

	int pathFindingGraphMargin = 5;

	[HideInInspector]
	public List<Vector3> road;

	private void OnDrawGizmosSelected()
	{
		if (road != null || road.Count > 1)
		{
			Gizmos.color = Color.magenta;
			for (int i = 1; i < road.Count; i++)
			{
				Gizmos.DrawLine(road[i - 1], road[i]);
			}
			Gizmos.DrawLine(road[road.Count-1], road[0]);
		}

	}

	public void Generate(float[,] heights, Vector3 terrainSize, Vector3 terrainPosition, float waterRelativeHeight)
	{
		int heightWidth = heights.GetLength(0);
		System.Random rnd = new System.Random();
		int pathFindingGraphSize = heightWidth / pathFindingSpacing - 2 * pathFindingGraphMargin;

		Pathfinding.PathFindNode[,] nodes = new Pathfinding.PathFindNode[pathFindingGraphSize, pathFindingGraphSize];
		for (int i = 1; i < pathFindingGraphSize - 1; i++)
		{
			for (int j = 1; j < pathFindingGraphSize - 1; j++)
			{
				float nodeHeight = heights[(i + pathFindingGraphMargin) * pathFindingSpacing, (j + pathFindingGraphMargin) * pathFindingSpacing];
				if (nodeHeight > waterRelativeHeight + 0.03f)
					nodes[i, j] = new Pathfinding.PathFindNode(i, j, nodeHeight);
			}
		}
		Pathfinding pf = new Pathfinding(nodes, heightPenalty);
		List<Pathfinding.PathFindNode> path = pf.GetRoad(numCheckpoints, rnd);
		road.Clear();
		for (int i = 0; i < path.Count; i++)
		{
			Vector3 pos = Vector3.Scale(terrainSize, path[i].GetLocalPosition(pathFindingGraphSize, pathFindingGraphSize, pathFindingGraphMargin)) + terrainPosition;
			if (road.Count < 1 || Vector3.SqrMagnitude(pos - road[road.Count - 1]) > 1)
				road.Add(pos);
		}
	}

	private int GetScalePosition(float world, float terrainSize, float terrainPosition, int scaledSize)
	{
		return (int)((float)(world - terrainPosition) * (float)scaledSize / (float)terrainSize);
	}

	public void DrawRoadHeights(float[,] heights, Vector3 terrainSize, Vector3 terrainPosition, int textureWidth)
	{
		int heightWidth = heights.GetLength(0);
		int roadWidth = (int)((float)this.roadWidth * (float)heightWidth / (float)(textureWidth - 1)) + 3;
		int px = 0;
		int py = 0;
		//Flag waypoints
		RoadSmoothNode[,] smooths = new RoadSmoothNode[heightWidth, heightWidth];
		for (int i = 0; i < road.Count; i++)
		{
			py = GetScalePosition(road[i].x, terrainSize.x, terrainPosition.x, heightWidth);
			px = GetScalePosition(road[i].z, terrainSize.z, terrainPosition.z, heightWidth);
			Drawing.DrawCircle(px, py, roadWidth + 2, (x, y) => {
				smooths[x, y].AddNode(road[i], 1f);
			});
			Drawing.DrawCircle(px, py, roadWidth + 3, (x, y) => {
				smooths[x, y].smooth = true;
			});
		}
		//Flag roads
		int prev = road.Count - 1;
		for (int i = 0; i < road.Count; i++)
		{
			int hy = GetScalePosition(road[i].x, terrainSize.x, terrainPosition.x, heightWidth);
			int hx = GetScalePosition(road[i].z, terrainSize.z, terrainPosition.z, heightWidth);
			Drawing.DrawFatLine(hx, hy, px, py, roadWidth + 3, (x, y, pa, pb) => {
				smooths[x, y].smooth = true;
			}, false);
			Drawing.DrawFatLine(hx, hy, px, py, roadWidth + 2, (x, y, pa, pb) => {
				smooths[x, y].AddNode(road[prev], pa);
				smooths[x, y].AddNode(road[i], pb);
			}, true);
			prev = i;
			px = hx;
			py = hy;
		}
		//Smooth and flatten
		float scale = 1f / terrainSize.y;
		for (int x = 0; x < heightWidth; x++)
			for (int y = 0; y < heightWidth; y++)
				smooths[x, y].CalculateRoads(heights, x, y, scale);
		for (int x = 0; x < heightWidth; x++)
			for (int y = 0; y < heightWidth; y++)
				smooths[x, y].CalculateSmooth(heights, x, y);
		for (int x = 0; x < heightWidth; x++)
			for (int y = 0; y < heightWidth; y++)
				smooths[x, y].ApplySmooth(heights, x, y);
	}

	public void DrawRoadTextures(float[,,] textures, Vector3 terrainSize, Vector3 terrainPosition)
	{
		int textureWidth = textures.GetLength(0);
		//Draw Road Textures
		int prev = road.Count - 1;
		int py = GetScalePosition(road[prev].x, terrainSize.x, terrainPosition.x, textureWidth);
		int px = GetScalePosition(road[prev].z, terrainSize.z, terrainPosition.z, textureWidth);
		int len = textures.GetLength(2);
		for (int i = 0; i < road.Count; i++)
		{
			int ty = GetScalePosition(road[i].x, terrainSize.x, terrainPosition.x, textureWidth);
			int tx = GetScalePosition(road[i].z, terrainSize.z, terrainPosition.z, textureWidth);
			Drawing.DrawCircle(tx, ty, roadWidth, (x, y) => {
				for (int j = 0; j < len; j++)
				{
					textures[x, y, j] = 0f;
				}
				textures[x, y, TrackGenerator.ROAD_TEXTURE_INDEX] = 1f;
			});
			Drawing.DrawFatLine(tx, ty, px, py, roadWidth, (x, y, pa, pb) => {
				for (int j = 0; j < len; j++)
				{
					textures[x, y, j] = 0f;
				}
				textures[x, y, TrackGenerator.ROAD_TEXTURE_INDEX] = 1f;
			});
			prev = i;
			px = tx;
			py = ty;
		}
		prev = road.Count - 1;
		py = GetScalePosition(road[prev].x, terrainSize.x, terrainPosition.x, textureWidth);
		px = GetScalePosition(road[prev].z, terrainSize.z, terrainPosition.z, textureWidth);
		for (int i = 0; i < road.Count; i++)
		{
			int ty = GetScalePosition(road[i].x, terrainSize.x, terrainPosition.x, textureWidth);
			int tx = GetScalePosition(road[i].z, terrainSize.z, terrainPosition.z, textureWidth);
			int counter = 2;
			Drawing.DrawLine(tx, ty, px, py, (x, y, f) => {
				counter++;
				if (counter > 3)
				{
					if (counter == 7)
						counter = 0;
				}
				else
				{
					textures[x, y, TrackGenerator.ROAD_TEXTURE_INDEX] = 0f;
					textures[x, y, TrackGenerator.PAINT_TEXTURE_INDEX] = 1f;
				}
			});
			Drawing.DrawCircle(px, py, 3, (x, y) => {
				textures[x, y, TrackGenerator.ROAD_TEXTURE_INDEX] = 1f;
				textures[x, y, TrackGenerator.PAINT_TEXTURE_INDEX] = 0f;
			});
			prev = i;
			px = tx;
			py = ty;
		}
	}

	public struct RoadSmoothNode
	{
		public float idealHeight;
		public bool smooth;
		public List<Vector3> roadNodes;
		public List<float> strengths;

		public void AddNode(Vector3 node, float strength)
		{
			if(roadNodes == null)
			{
				roadNodes = new List<Vector3>();
				roadNodes.Add(node);
				strengths = new List<float>();
				strengths.Add(strength);
			}
			else
			{
				for (int i = 0; i < roadNodes.Count; i++)
				{
					if(roadNodes[i] == node)
					{
						if (strengths[i] < strength)
							strengths[i] = strength;
						return;
					}
				}
				roadNodes.Add(node);
				strengths.Add(strength);
			}
		}

		public void CalculateRoads(float[,] heights, int x, int y, float heightScale)
		{
			if (roadNodes != null)
			{
				float sum = 0f;
				for (int i = 0; i < strengths.Count; i++)
				{
					strengths[i] = strengths[i] * strengths[i] + strengths[i]*0.3f;
					sum += strengths[i];
				}
				float h = 0;
				for (int i = 0; i < roadNodes.Count; i++)
				{
					h = h + roadNodes[i].y * strengths[i];
				}
				h = h / sum;
				heights[x, y] = h * heightScale - 0.002f;
			}
		}

		public void CalculateSmooth(float[,] heights, int x, int y)
		{
			if (smooth)
			{
				idealHeight = (
					                        heights[x - 1, y + 2] +     heights[x, y + 2] +     heights[x + 1, y + 2] +
					heights[x - 2, y + 1] + heights[x - 1, y + 1] * 2 + heights[x, y + 1] * 4 + heights[x + 1, y + 1] * 2 + heights[x + 2, y + 1] +
					heights[x - 2, y] +     heights[x - 1, y] * 4 +     heights[x, y] * 3 +     heights[x + 1, y] * 4 +     heights[x + 2, y] +
					heights[x - 2, y - 1] + heights[x - 1, y - 1] * 2 + heights[x, y - 1] * 4 + heights[x + 1, y - 1] * 2 + heights[x + 2, y - 1] +
					                        heights[x - 1, y - 2] +     heights[x, y - 2] +     heights[x + 1, y - 2]
					) / 39;
			}
		}

		public void ApplySmooth(float[,] heights, int x, int y)
		{
			if(smooth)
			{
				heights[x, y] = idealHeight;
			}
		}
	}
}
