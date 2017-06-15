
using System.Collections.Generic;
using UnityEngine;

public class Pathfinding
{
	PathFindNode[,] nodes;
	float heightPenalty;


	public Pathfinding(PathFindNode[,] nodes, float heightCost)
	{
		this.nodes = nodes;
		heightPenalty = heightCost;
	}


	public LinkedList<PathFindNode> PathFind(PathFindNode start, PathFindNode target)
	{
		int width = nodes.GetLength(0);
		int height = nodes.GetLength(1);
		if (!Utils.InMargin(start.x, start.y, width, height, 1) || !Utils.InMargin(target.x, target.y, width, height, 1) || target == null || start == null)
			return null;
		for (int i = 0; i < width; i++)
			for (int j = 0; j < height; j++)
				if (nodes[i, j] != null)
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

	public List<PathFindNode> GetRoad(int numCheckpoints, System.Random rnd)
	{
		List<PathFindNode> path = new List<PathFindNode>();
		PathFindNode start = GetNewRoadGoal(null, path, rnd, false);
		PathFindNode target = null;
		for (int i = 0; i < numCheckpoints; i++)
		{
			if (i == numCheckpoints - 1)
			{
				target = path[0];
			}
			else
			{
				target = GetNewRoadGoal(start, path, rnd, i % 2 == 0);
			}
			LinkedList<PathFindNode> p = PathFind(start, target);
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
		return path;
	}

	private void AddRoadSegment(List<PathFindNode> road, LinkedList<PathFindNode> segment)
	{
		foreach (var node in segment)
		{
			if (road.Count < 2)
			{
				road.Add(node);
				continue;
			}

			if (node.SqrDistance(road[road.Count - 1]) <= 9)
			{
				continue;
			}
			if (node.SqrDistance(road[road.Count-2]) <= 9)
			{
				road.RemoveAt(road.Count - 1);
				continue;
			}
			bool cont = true;
			for (int i = 0; i < road.Count-2; i++)
			{
				if (node.SqrDistance(road[i]) <= 9)
				{
					cont = false;
					road.Add(road[i]);
					break;
				}
			}
			if (cont)
			{
				Vector2 old = (Vector2)road[road.Count - 1] - (Vector2)road[road.Count - 2];
				Vector2 test = (Vector2)node - (Vector2)road[road.Count - 2];
				if (test.sqrMagnitude < 80 && Vector2.Angle(old, test) < 25f)
				{
					road[road.Count - 1] = node;
				}
				else
				{
					road.Add(node);
				}
			}
		}
	}

	private PathFindNode GetNewRoadGoal(PathFindNode start, List<PathFindNode> road, System.Random rnd, bool goalClose = false)
	{
		PathFindNode node = null;
		if (start == null)
		{
			while (node == null)
				node = nodes[rnd.Next(1, nodes.GetLength(0) - 1), rnd.Next(1, nodes.GetLength(0) - 1)];
			return node;
		}
		bool prev10 = true;
		do
		{
			node = nodes[rnd.Next(1, nodes.GetLength(0) - 1), rnd.Next(1, nodes.GetLength(0) - 1)];
			if (node == null)
				continue;

			if (goalClose && node.SqrDistance(start) > nodes.Length / 16)
				continue;

			prev10 = false;
			for (int pn = Mathf.Max(0, road.Count - 10); pn < road.Count; pn++)
				if (node.SqrDistance(road[pn]) <= 9)
				{
					prev10 = true;
					break;
				}
		}
		while (prev10);
		return node;
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
				(y + graphMargin) * horizontalScale,
				height * verticalScale,
				(x + graphMargin) * horizontalScale) + offset;
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