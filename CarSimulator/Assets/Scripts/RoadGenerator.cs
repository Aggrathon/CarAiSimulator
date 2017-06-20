using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;
using static Pathfinding;

[RequireComponent(typeof(Terrain))]
[ExecuteInEditMode]
public class RoadGenerator : MonoBehaviour {

	public Transform water;
	public ReflectionProbe reflection;
	public int heightPenalty = 30;
	public int numCheckpoints = 8;
	public int roadWidth = 10;
	public int pathFindingSpacing = 10;
	public int roadTextureIndex = 4;
	public int roadMarkerTextureIndex = 6;

	Terrain terrain;
	float[,] tempHeights;
	float[,,] tempTextures;
	Thread thread;
	string status;

	[HideInInspector]
	public List<Vector3> road;
	[NonSerialized]
	public Action onGenerated;

	private void Awake()
	{
		terrain = GetComponent<Terrain>();
	}

	[ContextMenu("Generate")]
	public void Generate()
	{
		if (thread != null)
			thread.Abort();
		tempHeights = null;
		tempTextures = null;
		status = null;
		int heightWidth = terrain.terrainData.heightmapWidth;
		float heightScaleX = terrain.terrainData.heightmapScale.x;
		float heightScaleY = terrain.terrainData.heightmapScale.y;
		float[,] heights = terrain.terrainData.GetHeights(0, 0, heightWidth, heightWidth);
		int textureWidth = terrain.terrainData.alphamapWidth;
		float[,,] textures = terrain.terrainData.GetAlphamaps(0, 0, textureWidth, textureWidth);
		Vector3 offset = transform.position;
		float waterHeight = water.position.y / terrain.terrainData.heightmapScale.y + 0.03f;

		thread = new Thread(() => {
			try
			{
				System.Random rnd = new System.Random();
				//Create path
				int pathFindingGraphMargin = 5;
				int pathFindingGraphSize = heightWidth / pathFindingSpacing - 2 * pathFindingGraphMargin;
				PathFindNode[,] nodes = new PathFindNode[pathFindingGraphSize, pathFindingGraphSize];
				for (int i = 1; i < pathFindingGraphSize-1; i++)
				{
					for (int j = 1; j < pathFindingGraphSize-1; j++)
					{
						float nodeHeight = heights[(i + pathFindingGraphMargin) * pathFindingSpacing, (j + pathFindingGraphMargin) * pathFindingSpacing];
						if (nodeHeight > waterHeight)
							nodes[i, j] = new PathFindNode(i, j, nodeHeight);
					}
				}
				Pathfinding pf = new Pathfinding(nodes, heightPenalty);
				List<PathFindNode> path = pf.GetRoad(numCheckpoints, rnd);
				//Create roads
				status = "Painting roads";
				DrawRoads(path, heights, textures, roadWidth, pathFindingGraphSize, pathFindingGraphMargin);
				tempHeights = heights;
				tempTextures = textures;
				List<Vector3> road = new List<Vector3>();
				foreach (var n in path) {
					Vector3 pos = n.GetPosition(pathFindingSpacing * heightScaleX, heightScaleY, offset, pathFindingGraphMargin);
					if(road.Count < 1 || Vector3.SqrMagnitude(pos-road[road.Count-1]) > 1)
						road.Add(pos);
				}
				this.road = road;
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		});
		thread.Start();
		StartCoroutine(WaitForResult());
		Debug.Log("Generating Road");
	}


	IEnumerator WaitForResult()
	{
		while (tempHeights == null || tempTextures == null)
		{
#if UNITY_EDITOR
			if (status != null)
			{
				Debug.Log(status);
				status = null;
			}
			if(!thread.IsAlive)
			{
				Debug.Log("Road Generation Thread Has Died");
				Generate();
				yield break;
			}
#endif
			yield return null;
		}
		terrain.terrainData.SetHeights(0, 0, tempHeights);
		terrain.terrainData.SetAlphamaps(0, 0, tempTextures);
		terrain.Flush();

		thread = null;
		tempHeights = null;
		tempTextures = null;
		yield return null;
		reflection.RenderProbe();
		Debug.Log("Generated Road");
		if (onGenerated != null)
			onGenerated();
	}

	private void OnDisable()
	{
		if (thread != null)
			thread.Abort();
	}

	public bool IsRoad(Vector3 pos)
	{
		int x = (int)((pos.x - transform.position.x) / terrain.terrainData.size.x * terrain.terrainData.alphamapWidth);
		int y = (int)((pos.z - transform.position.z) / terrain.terrainData.size.z * terrain.terrainData.alphamapHeight);
		float[,,] alpha = terrain.terrainData.GetAlphamaps(x, y, 1, 1);
		return alpha[0, 0, roadTextureIndex] > 0.5f;
	}


	private void DrawRoads(List<PathFindNode> path, float[,] heights, float[,,] textures, int roadWidth, int graphWidth, int graphMargin)
	{
		int textureWidth = textures.GetLength(0);
		int heightWidth = heights.GetLength(0);
		//Draw Road Textures
		int prev = path.Count - 1;
		int px = path[prev].GetTextureX(graphWidth, textureWidth, graphMargin);
		int py = path[prev].GetTextureY(graphWidth, textureWidth, graphMargin);
		int len = textures.GetLength(2);
		for (int i = 0; i < path.Count; i++)
		{
			int tx = path[i].GetTextureX(graphWidth, textureWidth, graphMargin);
			int ty = path[i].GetTextureY(graphWidth, textureWidth, graphMargin);
			Drawing.DrawCircle(tx, ty, roadWidth, (x, y) => {
				for (int j = 0; j < len; j++)
				{
					textures[x, y, j] = 0f;
				}
				textures[x, y, roadTextureIndex] = 1f;
			});
			Drawing.DrawFatLine(tx, ty, px, py, roadWidth, (x, y, pa, pb) => {
				for (int j = 0; j < len; j++)
				{
					textures[x, y, j] = 0f;
				}
				textures[x, y, roadTextureIndex] = 1f;
			});
			prev = i;
			px = tx;
			py = ty;
		}
		prev = path.Count - 1;
		px = path[prev].GetTextureX(graphWidth, textureWidth, graphMargin);
		py = path[prev].GetTextureY(graphWidth, textureWidth, graphMargin);
		for (int i = 0; i < path.Count; i++)
		{
			int tx = path[i].GetTextureX(graphWidth, textureWidth, graphMargin);
			int ty = path[i].GetTextureY(graphWidth, textureWidth, graphMargin);
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
					textures[x, y, roadTextureIndex] = 0f;
					textures[x, y, roadMarkerTextureIndex] = 1f;
				}
			});
			Drawing.DrawCircle(px, py, 3, (x, y) => {
				textures[x, y, roadTextureIndex] = 1f;
				textures[x, y, roadMarkerTextureIndex] = 0f;
			});
			prev = i;
			px = tx;
			py = ty;
		}
		//Draw road Heights
		roadWidth = (int)((float)roadWidth * (float)heightWidth / (float)(textureWidth - 1)) + 3;
		//Flag waypoints
		RoadSmoothNode[,] smooths = new RoadSmoothNode[heightWidth, heightWidth];
		for (int i = 0; i < path.Count; i++)
		{
			int hx = path[i].GetHeightX(graphWidth, heightWidth, graphMargin);
			int hy = path[i].GetHeightY(graphWidth, heightWidth, graphMargin);
			Drawing.DrawCircle(hx, hy, roadWidth + 2, (x, y) => {
				smooths[x, y].AddNode(path[i], 1f);
			});
			Drawing.DrawCircle(hx, hy, roadWidth + 3, (x, y) => {
				smooths[x, y].smooth = true;
			});
		}
		//Flag roads
		prev = path.Count - 1;
		px = path[prev].GetHeightX(graphWidth, heightWidth, graphMargin);
		py = path[prev].GetHeightY(graphWidth, heightWidth, graphMargin);
		for (int i = 0; i < path.Count; i++)
		{
			int hx = path[i].GetHeightX(graphWidth, heightWidth, graphMargin);
			int hy = path[i].GetHeightY(graphWidth, heightWidth, graphMargin);
			Drawing.DrawFatLine(hx, hy, px, py, roadWidth + 2, (x, y, pa, pb) => {
				smooths[x, y].smooth = true;
			}, false);
			Drawing.DrawFatLine(hx, hy, px, py, roadWidth + 1, (x, y, pa, pb) => {
				smooths[x, y].AddNode(path[prev], pa);
				smooths[x, y].AddNode(path[i], pb);
			}, true);
			prev = i;
			px = hx;
			py = hy;
		}
		//Smooth and flatten
		for (int x = 0; x < heightWidth; x++)
			for (int y = 0; y < heightWidth; y++)
				smooths[x, y].CalculateRoads(heights, x, y);
		for (int x = 0; x < heightWidth; x++)
			for (int y = 0; y < heightWidth; y++)
				smooths[x, y].CalculateSmooth(heights, x, y);
		for (int x = 0; x < heightWidth; x++)
			for (int y = 0; y < heightWidth; y++)
				smooths[x, y].ApplySmooth(heights, x, y);
	}

	public struct RoadSmoothNode
	{
		public float idealHeight;
		public bool smooth;
		public List<PathFindNode> roadNodes;
		public List<float> strengths;

		public void AddNode(PathFindNode node, float strength)
		{
			if(roadNodes == null)
			{
				roadNodes = new List<PathFindNode>();
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

		public void CalculateRoads(float[,] heights, int x, int y)
		{
			if (roadNodes != null)
			{
				float sum = 0f;
				for (int i = 0; i < strengths.Count; i++)
				{
					strengths[i] = strengths[i] * strengths[i] + strengths[i]*0.5f;
					sum += strengths[i];
				}
				float h = 0;
				for (int i = 0; i < roadNodes.Count; i++)
				{
					h = h + roadNodes[i].height * strengths[i];
				}
				h = h / sum;
				heights[x, y] = h;
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
