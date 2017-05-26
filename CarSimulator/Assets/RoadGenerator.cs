using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;

[RequireComponent(typeof(Terrain))]
[ExecuteInEditMode]
public class RoadGenerator : MonoBehaviour {

	public ReflectionProbe reflection;
	public int heightPenalty = 30;
	public int numCheckpoints = 8;
	public int roadWidth = 10;
	public int pathFindingSpacing = 10;

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

		thread = new Thread(() => {
			//Create path
			List<PathFindNode> path = new List<PathFindNode>();
			System.Random rnd = new System.Random();
			int[] order;
			bool random = true;
			if (random)
			{
				order = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
				for (int i = 0; i < order.Length-1; i++)
				{
					int j = rnd.Next(i, order.Length);
					int tmp = order[i];
					order[i] = order[j];
					order[j] = tmp;
				}
			}
			else
			{
				order = new int[] { 0, 1, 2, 5, 8, 7, 6, 3 };
			}
			float waterHeight = 0;
			foreach (float h in heights)
				waterHeight += h ;
			waterHeight /= heights.Length * 1.78f;
			int pathFindingGraphSize = heightWidth / pathFindingSpacing;
			PathFindNode[,] nodes = new PathFindNode[pathFindingGraphSize, pathFindingGraphSize];
			for (int i = 0; i < pathFindingGraphSize; i++)
			{
				for (int j = 0; j < pathFindingGraphSize; j++)
				{
					nodes[i,j] = new PathFindNode(i, j, heights[i*heightWidth/ pathFindingGraphSize, j*heightWidth/ pathFindingGraphSize], pathFindingGraphSize, pathFindingGraphSize, waterHeight);
				}
			}
			int x2, y2, number = 0;
			int x1 = rnd.Next(pathFindingGraphSize / 3 * (order[number % order.Length] % 3)+10, pathFindingGraphSize / 3 * (order[number % order.Length] % 3 + 1)-10);
			int y1 = rnd.Next(pathFindingGraphSize / 3 * (order[number % order.Length] / 3)+10, pathFindingGraphSize / 3 * (order[number % order.Length] / 3 + 1)-10);
			for (int i = 0; i < numCheckpoints; i++)
			{
				if (i == numCheckpoints-1)
				{
					x2 = path[0].x;
					y2 = path[0].y;
				}
				else
				{
					x2 = rnd.Next(pathFindingGraphSize / 3 * (order[number % order.Length] % 3) + 10, pathFindingGraphSize / 3 * (order[number % order.Length] % 3 + 1) - 10);
					y2 = rnd.Next(pathFindingGraphSize / 3 * (order[number % order.Length] / 3) + 10, pathFindingGraphSize / 3 * (order[number % order.Length] / 3 + 1) - 10);
				}
				LinkedList<PathFindNode> p = PathFind(x1, y1, x2, y2, nodes);
				if(p == null)
				{
					if(i==0)
					{
						x1 = x2;
						y1 = y2;
					}
					i--;
					number++;
					continue;
				}
				x1 = x2;
				y1 = y2;
				AddRoadSegment(path, p);
				number++;
			}
			//Create roads
			PathFindNode prev = path[path.Count - 1];
			float textureScaling = (float)textureWidth / (float)pathFindingGraphSize;
			int len = textures.GetLength(2) - 1;
			for (int i = 0; i < path.Count; i++)
			{
				PathFindNode node = path[i];
				int tx = (int)((float)node.x *textureScaling);
				int ty = (int)((float)node.y *textureScaling);
				Drawing.DrawCircle(tx, ty, roadWidth, (x, y) => {
					for (int j = 0; j < len; j++)
					{
						textures[x, y, j] = 0f;
					}
					textures[x, y, len] = 1f;
				});
				Drawing.DrawFatLine(tx, ty, (int)((float)prev.x*textureScaling) , (int)((float)prev.y * textureScaling), roadWidth, (x, y, p) => {
					for (int j = 0; j < len; j++)
					{
						textures[x, y, j] = 0f;
					}
					textures[x, y, len] = 1f;
				});
				prev = node;
			}
			SmoothRoads(path, heights, (int)(roadWidth * (float)heightWidth / (float)textureWidth - 1) + 3, (float)heightWidth / pathFindingGraphSize);
			tempHeights = heights;
			tempTextures = textures;
			road = new List<Vector3>();
			foreach (var n in path)
				road.Add(n.GetPosition(pathFindingSpacing * heightScaleX, heightScaleY, offset));
		});
		thread.Start();
		StartCoroutine(WaitForResult());
		Debug.Log("Generating Road");
	}


	IEnumerator WaitForResult()
	{
		while (tempHeights == null || tempTextures == null)
		{
			if (status != null)
			{
				Debug.Log(status);
				status = null;
			}
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
		return alpha[0, 0, alpha.GetUpperBound(2)] > 0.5f;
	}


	private void SmoothRoads(List<PathFindNode> path, float[,] heights, int roadWidth, float graphScale)
	{
		//Flatten waypoints
		for (int i = 0; i < path.Count; i++)
		{
			int hx = (int)((float)path[i].x * graphScale);
			int hy = (int)((float)path[i].y * graphScale);
			Drawing.DrawCircle(hx, hy, roadWidth+1, (x, y) => {
				heights[x, y] = path[i].height;
			});
		}
		//Flatten roads
		PathFindNode prev = path[path.Count - 1];
		int px = (int)((float)prev.x * graphScale);
		int py = (int)((float)prev.y * graphScale);
		for (int i = 0; i < path.Count; i++)
		{
			PathFindNode node = path[i];
			int hx = (int)((float)node.x * graphScale);
			int hy = (int)((float)node.y * graphScale);
			Drawing.DrawFatLine(hx, hy, px, py, roadWidth, (x, y, p) => {
				p = (p < 0.5f ? 2 * p * p : -1 + (4 - 2 * p) * p) * 0.5f + p * 0.5f;
				heights[x, y] = prev.height * p + node.height * (1 - p);
			}, true);
			prev = node;
			px = hx;
			py = hy;
		}
		//Smooth borders
		prev = path[0];
		px = (int)((float)prev.x * graphScale);
		py = (int)((float)prev.y * graphScale);
		for (int i = path.Count - 1; i >= 0; i--)
		{
			PathFindNode node = path[i];
			int hx = (int)((float)node.x * graphScale);
			int hy = (int)((float)node.y * graphScale);
			Drawing.DrawSideLines(hx, hy, px, py, roadWidth+1, (x, y, p) => {
				heights[x, y] = (
					heights[x - 1, y + 2] + heights[x + 1, y + 2] + heights[x + 1, y + 2] +
					heights[x - 2, y + 1] + heights[x - 1, y + 1] + heights[x + 1, y + 1] * 2 + heights[x + 1, y + 1] + heights[x + 2, y + 1] +
					heights[x - 2, y] + heights[x - 1, y] * 2 + heights[x, y] * 4 + heights[x + 1, y] * 2 + heights[x + 2, y] +
					heights[x - 2, y - 1] + heights[x - 1, y - 1] + heights[x, y - 1] * 2 + heights[x + 1, y - 1] + heights[x + 2, y - 1] +
					heights[x - 1, y - 2] + heights[x, y - 2] + heights[x + 1, y - 2]
					) / 28;
			}, true);
			prev = node;
			px = hx;
			py = hy;
		}
		//Flatten roads
		prev = path[0];
		px = (int)((float)prev.x * graphScale);
		py = (int)((float)prev.y * graphScale);
		for (int i = path.Count-1; i >= 0; i--)
		{
			PathFindNode node = path[i];
			int hx = (int)((float)node.x * graphScale);
			int hy = (int)((float)node.y * graphScale);
			Drawing.DrawFatLine(hx, hy, px, py, roadWidth, (x, y, p) => {
				p = (p < 0.5f ? 2 * p * p : -1 + (4 - 2 * p) * p) * 0.7f + p * 0.3f;
				heights[x, y] = heights[x, y] * 0.6f + (prev.height * p + node.height * (1 - p)) * 0.4f;
			}, true);
			prev = node;
			px = hx;
			py = hy;
		}
		//Smooth waypoints
		for (int i = 0; i < path.Count; i++)
		{
			int hx = (int)((float)path[i].x * graphScale);
			int hy = (int)((float)path[i].y * graphScale);
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
			bool cont = true;
			for (int i = 0; i < roads.Count; i++)
			{
				if (node.SqrDistance(roads[i]) < 10)
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

	/*void OnDrawGizmosSelected()
	{
		if(tempHeights == null && road != null && road.Count > 0)
		{
			Gizmos.color = Color.black;
			Vector3 pos = road[road.Count - 1] + Vector3.up;
			for (int i = 0; i < road.Count; i++)
			{
				Gizmos.DrawSphere(road[i], 10);
				Gizmos.DrawLine(pos, road[i]+Vector3.up);
				pos = road[i] + Vector3.up;
			}
		}
	}*/


	private LinkedList<PathFindNode> PathFind(int x1, int y1, int x2, int y2, PathFindNode[,] nodes)
	{
		foreach (var n in nodes)
			n.Reset();
		int jumpDistance = 1;
		LinkedList<PathFindNode> queue = new LinkedList<PathFindNode>();
		PathFindNode target = nodes[x2, y2];
		PathFindNode start = nodes[x1, y1];
		start.distance = 0;
		PathFindTryPath(start, nodes[x1 + jumpDistance, y1], target, queue);
		PathFindTryPath(start, nodes[x1 - jumpDistance, y1], target, queue);
		PathFindTryPath(start, nodes[x1, y1 - jumpDistance], target, queue);
		PathFindTryPath(start, nodes[x1, y1 + jumpDistance], target, queue);
		PathFindTryPath(start, nodes[x1 - jumpDistance, y1 - jumpDistance], target, queue);
		PathFindTryPath(start, nodes[x1 - jumpDistance, y1 + jumpDistance], target, queue);
		PathFindTryPath(start, nodes[x1 + jumpDistance, y1 - jumpDistance], target, queue);
		PathFindTryPath(start, nodes[x1 + jumpDistance, y1 + jumpDistance], target, queue);

		while (queue.Count > 0)
		{
			PathFindNode current = queue.First.Value;
			queue.RemoveFirst();
			current.queueNode = null;
			current.visited = true;
			if (current == target)
			{
				LinkedList<PathFindNode> path = new LinkedList<PathFindNode>();
				while (target != null)
				{
					path.AddFirst(target);
					target = target.prevNode;
				}
				return path;
			}
			int dx = current.x - current.prevNode.x;
			int dy = current.y - current.prevNode.y;
			if (dx > 0)
				PathFindTryPath(current, nodes[current.x + jumpDistance, current.y], target, queue);
			if (dx < 0)
				PathFindTryPath(current, nodes[current.x - jumpDistance, current.y], target, queue);
			if (dy > 0)
				PathFindTryPath(current, nodes[current.x, current.y + jumpDistance], target, queue);
			if (dy < 0)
				PathFindTryPath(current, nodes[current.x, current.y - jumpDistance], target, queue);
			if (dy >= 0 && dx >= 0)
				PathFindTryPath(current, nodes[current.x + jumpDistance, current.y + jumpDistance], target, queue);
			if (dy <= 0 && dx >= 0)
				PathFindTryPath(current, nodes[current.x + jumpDistance, current.y - jumpDistance], target, queue);
			if (dy >= 0 && dx <= 0)
				PathFindTryPath(current, nodes[current.x - jumpDistance, current.y + jumpDistance], target, queue);
			if (dy <= 0 && dx <= 0)
				PathFindTryPath(current, nodes[current.x - jumpDistance, current.y - jumpDistance], target, queue);
			if (queue.Count > 800)
				return null;
		}
		return null;
	}

	private void PathFindTryPath(PathFindNode from, PathFindNode to, PathFindNode target, LinkedList<PathFindNode> queue)
	{
		if (to.visited)
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

		public PathFindNode(int x, int y, float h, int maxX, int maxY, float waterHeight = 0)
		{
			this.x = x;
			this.y = y;
			if (x < 10 || x > maxX - 11 || y < 10 || y > maxY - 11 || h < waterHeight)
				height = 99999f;
			else
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

		public Vector3 GetPosition(float horizontalScale, float verticalScale, Vector3 offset)
		{
			return new Vector3(
				y * horizontalScale, 
				height * verticalScale, 
				x * horizontalScale) + offset;
		}
	}
}
