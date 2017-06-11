using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;

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
				List<PathFindNode> path = new List<PathFindNode>();
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
				PathFindNode start = GetNextNode(null, path, rnd, nodes, false);
				PathFindNode target = null;
				for (int i = 0; i < numCheckpoints; i++)
				{
					if (i == numCheckpoints - 1)
					{
						target = path[0];
					}
					else
					{
						target = GetNextNode(start, path, rnd, nodes, i%2==0);
					}
					LinkedList<PathFindNode> p = PathFind(start, target, nodes);
					if (p == null)
					{
						if (i == 0)
						{
							start = target;
						}
						i--;
						continue;
					}
					start = target;
					AddRoadSegment(path, p);
				}
				//Create roads
				status = "Painting roads";
				DrawRoads(path, heights, textures, (int)(roadWidth * (float)heightWidth / (float)textureWidth - 1) + 3, pathFindingGraphSize, pathFindingGraphMargin);
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
		//Draw Road

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
			Drawing.DrawFatLine(tx, ty, px, py, roadWidth, (x, y, p) => {
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
		//Flatten waypoints
		for (int i = 0; i < path.Count; i++)
		{
			int hx = path[i].GetHeightX(graphWidth, heightWidth, graphMargin);
			int hy = path[i].GetHeightY(graphWidth, heightWidth, graphMargin);
			Drawing.DrawCircle(hx, hy, roadWidth+1, (x, y) => {
				heights[x, y] = path[i].height;
			});
		}
		//Flatten roads
		prev = path.Count - 1;
		px = path[prev].GetHeightX(graphWidth, heightWidth, graphMargin);
		py = path[prev].GetHeightY(graphWidth, heightWidth, graphMargin);
		for (int i = 0; i < path.Count; i++)
		{
			int hx = path[i].GetHeightX(graphWidth, heightWidth, graphMargin);
			int hy = path[i].GetHeightY(graphWidth, heightWidth, graphMargin);
			Drawing.DrawFatLine(hx, hy, px, py, roadWidth, (x, y, p) => {
				p = (p < 0.5f ? 2 * p * p : -1 + (4 - 2 * p) * p) * 0.5f + p * 0.5f;
				heights[x, y] = path[prev].height * p + path[i].height * (1 - p);
			}, true);
			prev = i;
			px = hx;
			py = hy;
		}
		//Smooth borders
		prev = 0;
		px = path[prev].GetHeightX(graphWidth, heightWidth, graphMargin);
		py = path[prev].GetHeightY(graphWidth, heightWidth, graphMargin);
		for (int i = path.Count - 1; i >= 0; i--)
		{
			int hx = path[i].GetHeightX(graphWidth, heightWidth, graphMargin);
			int hy = path[i].GetHeightY(graphWidth, heightWidth, graphMargin);;
			Drawing.DrawSideLines(hx, hy, px, py, roadWidth+1, (x, y, p) => {
				heights[x, y] = (
					heights[x - 1, y + 2] + heights[x + 1, y + 2] + heights[x + 1, y + 2] +
					heights[x - 2, y + 1] + heights[x - 1, y + 1] + heights[x + 1, y + 1] * 2 + heights[x + 1, y + 1] + heights[x + 2, y + 1] +
					heights[x - 2, y] + heights[x - 1, y] * 2 + heights[x, y] * 4 + heights[x + 1, y] * 2 + heights[x + 2, y] +
					heights[x - 2, y - 1] + heights[x - 1, y - 1] + heights[x, y - 1] * 2 + heights[x + 1, y - 1] + heights[x + 2, y - 1] +
					heights[x - 1, y - 2] + heights[x, y - 2] + heights[x + 1, y - 2]
					) / 28;
			}, true);
			prev = i;
			px = hx;
			py = hy;
		}
		//Flatten roads
		prev = 0;
		px = path[prev].GetHeightX(graphWidth, heightWidth, graphMargin);
		py = path[prev].GetHeightY(graphWidth, heightWidth, graphMargin);
		for (int i = path.Count-1; i >= 0; i--)
		{
			int hx = path[i].GetHeightX(graphWidth, heightWidth, graphMargin);
			int hy = path[i].GetHeightY(graphWidth, heightWidth, graphMargin);;
			Drawing.DrawFatLine(hx, hy, px, py, roadWidth, (x, y, p) => {
				p = (p < 0.5f ? 2 * p * p : -1 + (4 - 2 * p) * p) * 0.7f + p * 0.3f;
				heights[x, y] = heights[x, y] * 0.6f + (path[prev].height * p + path[i].height * (1 - p)) * 0.4f;
			}, true);
			prev = i;
			px = hx;
			py = hy;
		}
		//Smooth waypoints
		for (int i = 0; i < path.Count; i++)
		{
			int hx = path[i].GetHeightX(graphWidth, heightWidth, graphMargin);
			int hy = path[i].GetHeightY(graphWidth, heightWidth, graphMargin);
			Drawing.DrawCircle(hx, hy, roadWidth-1, (x, y) => {
				heights[x, y] = (
					heights[x - 1, y + 2] + heights[x + 1, y + 2] + heights[x + 1, y + 2] +
					heights[x - 2, y + 1] + heights[x - 1, y + 1] + heights[x + 1, y + 1] * 2 + heights[x + 1, y + 1] + heights[x + 2, y + 1] +
					heights[x - 2, y] + heights[x - 1, y]*2 + heights[x, y]*4 + heights[x + 1, y]*2 + heights[x + 2, y] +
					heights[x - 2, y - 1] + heights[x - 1, y - 1] + heights[x, y - 1] * 2 + heights[x + 1, y - 1] + heights[x + 2, y - 1] +
					heights[x - 1, y - 2] + heights[x, y - 2] + heights[x + 1, y - 2]
					) / 28;
			});
		}
	}

	private void AddRoadSegment(List<PathFindNode> roads, LinkedList<PathFindNode> segment)
	{
		while(roads.Count < 2)
		{
			roads.Add(segment.First.Value);
			segment.RemoveFirst();
		}
		foreach (var node in segment)
		{
			if (node.SqrDistance(roads[roads.Count - 1]) <= 4)
				continue;
			if (node.SqrDistance(roads[roads.Count - 2]) <= 4)
			{
				roads.RemoveAt(roads.Count - 1);
				continue;
			}
			bool cont = true;
			for (int i = 0; i < roads.Count; i++)
			{
				if (node.SqrDistance(roads[i]) <= 9)
				{
					cont = false;
					roads.Add(roads[i]);
					break;
				}
			}
			if(cont)
			{
				Vector2 old = (Vector2)roads[roads.Count - 1] - (Vector2)roads[roads.Count - 2];
				Vector2 test = (Vector2)node - (Vector2)roads[roads.Count - 2];
				if (test.sqrMagnitude < 80 && Vector2.Angle(old, test) < 25f)
				{
					roads[roads.Count - 1] = node;
				}
				else
				{
					roads.Add(node);
				}
			}
		}
	}

	private PathFindNode GetNextNode(PathFindNode prev, List<PathFindNode> path, System.Random rnd, PathFindNode[,] nodes, bool close=false)
	{
		PathFindNode node = null;
		if (prev == null)
		{
			while (node == null)
				node = nodes[rnd.Next(1, nodes.GetLength(0) - 1), rnd.Next(1, nodes.GetLength(0) - 1)];
			return node;
		}
		bool prev10 = true;
		do
		{
			node = nodes[rnd.Next(1, nodes.GetLength(0) - 1), rnd.Next(1, nodes.GetLength(0) - 1)];
			if(node == null)
				continue;

			if (close && node.SqrDistance(prev) > nodes.Length / 16)
				continue;

			prev10 = false;
			for (int pn = Mathf.Max(0, path.Count - 10); pn < path.Count; pn++)
				if (node.SqrDistance(path[pn]) <= 9)
				{
					prev10 = true;
					break;
				}
		}
		while (prev10);
		return node;
	}

	private LinkedList<PathFindNode> PathFind(PathFindNode start, PathFindNode target, PathFindNode[,] nodes)
	{
		int width = nodes.GetLength(0);
		int height = nodes.GetLength(1);
		if (!Utils.InMargin(start.x, start.y, width, height, 1) || !Utils.InMargin(target.x, target.y, width, height, 1) || target == null || start == null)
			return null;
		for (int i = 0; i < width; i++)
			for (int j = 0; j < height; j++)
				if(nodes[i, j] != null)
					nodes[i, j].Reset();
		int jumpDistance = 1;
		LinkedList<PathFindNode> queue = new LinkedList<PathFindNode>();
		start.distance = 0;
		queue.AddFirst(start);
		
		while (queue.Count > 0)
		{
			PathFindNode current = queue.First.Value;
			queue.RemoveFirst();
			current.queueNode = null;
			current.visited = true;
			if (current == target)
			{
				target = target.prevNode;
				LinkedList<PathFindNode> path = new LinkedList<PathFindNode>();
				while (target != null)
				{
					path.AddFirst(target);
					target = target.prevNode;
				}
				return path;
			}
			PathFindTryPath(current, nodes[current.x + jumpDistance, current.y], target, queue);
			PathFindTryPath(current, nodes[current.x - jumpDistance, current.y], target, queue);
			PathFindTryPath(current, nodes[current.x, current.y + jumpDistance], target, queue);
			PathFindTryPath(current, nodes[current.x, current.y - jumpDistance], target, queue);
			PathFindTryPath(current, nodes[current.x + jumpDistance, current.y + jumpDistance], target, queue);
			PathFindTryPath(current, nodes[current.x + jumpDistance, current.y - jumpDistance], target, queue);
			PathFindTryPath(current, nodes[current.x - jumpDistance, current.y + jumpDistance], target, queue);
			PathFindTryPath(current, nodes[current.x - jumpDistance, current.y - jumpDistance], target, queue);
			if (queue.Count > nodes.Length / 8)
			{
				return null;
			}
		}
		return null;
	}

	private void PathFindTryPath(PathFindNode from, PathFindNode to, PathFindNode target, LinkedList<PathFindNode> queue)
	{
		if (to == null || to.visited)
			return;
		float distance = from.distance + ((from.height - to.height) * heightPenalty) * ((from.height - to.height) * heightPenalty) + from.Distance(to);
		if (distance + 0.1f < to.distance)
		{
			to.distance = distance;
			to.prevNode = from;
			to.estimation = to.distance + to.Distance(target);
			if (to.queueNode != null)
			{
				queue.Remove(to.queueNode);
			}
			if (queue.Count == 0)
				queue.AddFirst(to);
			var pos = queue.First;
			while (pos.Next != null && to.estimation > pos.Next.Value.estimation)
				pos = pos.Next;
			to.queueNode = queue.AddAfter(pos, to);
		}
	}

	public class PathFindNode
	{
		public int x;
		public int y;
		public float height;
		public float distance;
		public float estimation;
		public PathFindNode prevNode;
		public LinkedListNode<PathFindNode> queueNode;
		public bool visited;

		public PathFindNode(int x, int y, float h)
		{
			this.x = x;
			this.y = y;
			height = h;
		}

		public void Reset()
		{
			distance = 99999f;
			prevNode = null;
			queueNode = null;
			visited = false;
			estimation = float.PositiveInfinity;
		}

		public float Distance(PathFindNode other)
		{
			float dx = x - other.x;
			float dy = y - other.y;
			return Mathf.Sqrt(dx * dx + dy * dy);
		}

		public int SqrDistance(PathFindNode other)
		{
			int dx = x - other.x;
			int dy = y - other.y;
			return dx * dx + dy * dy;
		}

		public static explicit operator Vector2(PathFindNode node)
		{
			return new Vector2(node.x, node.y);
		}

		public Vector3 GetPosition(float horizontalScale, float verticalScale, Vector3 offset, int graphMargin)
		{
			return new Vector3(
				(y+ graphMargin) * horizontalScale, 
				height * verticalScale, 
				(x+ graphMargin) * horizontalScale) + offset;
		}

		public int GetTextureX(int graphWidth, int textureWidth, int graphMargin)
		{
			return (x + graphMargin) * textureWidth / (graphWidth + graphMargin + graphMargin);
		}

		public int GetTextureY(int graphHeight, int textureHeight, int graphMargin)
		{
			return (y + graphMargin) * textureHeight / (graphHeight + graphMargin + graphMargin);
		}

		public int GetHeightX(int graphWidth, int heightWidth, int graphMargin)
		{
			return (x + graphMargin) * heightWidth / (graphWidth + graphMargin + graphMargin);
		}

		public int GetHeightY(int graphHeight, int heightHeight, int graphMargin)
		{
			return (y + graphMargin) * heightHeight / (graphHeight + graphMargin + graphMargin);
		}
	}
}
