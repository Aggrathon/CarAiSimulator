using System;
using UnityEngine;

public static class Drawing
{

	public static void DrawLine(int x1, int y1, int x2, int y2, Action<int, int, float> onPoint)
	{
		float dx = x2 - x1;
		float dy = y2 - y1;
		float dxa = Mathf.Abs(dx);
		float dya = Mathf.Abs(dy);
		bool swap = false;

		if (dxa > dya)
		{
			if (x2 < x1)
			{
				Utils.Swap(ref x1, ref x2);
				Utils.Swap(ref y1, ref y2);
				dy = -dy;
				swap = true;
			}

			float derr = dya / dxa;
			float error = derr - 0.5f;

			int y = y1;
			int step = (int)Mathf.Sign(dy);
			for (int x = x1; x < x2; x++)
			{
				float perc = (float)(x - x1) / (float)(x2 - x1);
				if (swap) perc = 1 - perc;
				onPoint(x, y, perc);
				error += derr;
				if (error >= 0.5f)
				{
					y += step;
					error -= 1f;
				}
			}
		}
		else
		{
			if (dya == 0)
			{
				onPoint(x1, y1, 0.5f);
				return;
			}
			if (y2 < y1)
			{
				Utils.Swap(ref x1, ref x2);
				Utils.Swap(ref y1, ref y2);
				dx = -dx;
				swap = true;
			}
			float derr = dxa / dya;
			float error = derr - 0.5f;

			int x = x1;
			int step = (int)Mathf.Sign(dx);
			for (int y = y1; y < y2; y++)
			{
				float perc = (float)(y - y1) / (float)(y2 - y1);
				if (swap) perc = 1 - perc;
				onPoint(x, y, perc);
				error += derr;
				if (error >= 0.5f)
				{
					x += step;
					error -= 1f;
				}
			}
		}
	}

	public static void DrawFatLine(int x1, int y1, int x2, int y2, int width, Action<int, int, float, float> onPoint, bool shortEnd=false)
	{
		Vector2 along = new Vector2(x2 - x1, y2 - y1).normalized;
		if (shortEnd)
		{
			Vector2 end = along * (width * 0.8f);
			x1 += (int)Mathf.Round(end.x);
			x2 -= (int)Mathf.Round(end.x);
			y1 += (int)Mathf.Round(end.y);
			y2 -= (int)Mathf.Round(end.y);
		}
		along *= width;
		int dx = -(int)Mathf.Round(along.y);
		int dy = (int)Mathf.Round(along.x);
		if (Mathf.Abs(Mathf.Abs(dx)-Mathf.Abs(dy)) < width)
		{
			DrawLine(x1, y1, x2, y2, (x, y, p) => {
				DrawLine(x+dx, y+dy, x-dx, y-dy, (rx, ry, rp) => {
					rp = Mathf.Abs(rp - 0.5f);
					rp = 1 - rp * rp * 3;
					float pa = p * rp;
					float pb = (1-p) * rp;
					onPoint(rx + 1, ry + 1, pa, pb);
					onPoint(rx + 1, ry, pa, pb);
					onPoint(rx + 1, ry - 1, pa, pb);
					onPoint(rx, ry + 1, pa, pb);
					onPoint(rx, ry, pa, pb);
					onPoint(rx, ry - 1, pa, pb);
					onPoint(rx - 1, ry + 1, pa, pb);
					onPoint(rx - 1, ry, pa, pb);
					onPoint(rx - 1, ry - 1, pa, pb);
				});
			});
		}
		else
		{
			DrawLine(x1, y1, x2, y2, (x, y, p) => {
				DrawLine(x + dx, y + dy, x - dx, y - dy, (rx, ry, rp) => {
					onPoint(rx, ry, p, 1-p);
				});
			});
		}
	}

	public static void DrawPolygon(Action<int, int> onPoint, params Pixel[] points)
	{
		int minX = points[0].x;
		int maxX = points[0].x;
		for (int i = 1; i < points.Length; i++)
		{
			if (points[i].x > maxX)
				maxX = points[i].x;
			else if (points[i].x < minX)
				minX = points[i].x;
		}
		int[] minY = new int[maxX - minX];
		int[] maxY = new int[maxX - minX];
		for (int i = 0; i < minY.Length; i++)
		{
			minY[i] = int.MaxValue;
			maxY[i] = int.MinValue;
		}
		Pixel prev = points[points.Length - 1];
		for (int i = 0; i < points.Length; i++)
		{
			DrawLine(prev.x, prev.y, points[i].x, points[i].y, (x,y,p) => {
				x -= minX;
				if (minY[x] > y)
					minY[x] = y;
				if (maxY[x] < y)
					maxY[x] = y;
			});
			prev = points[i];
		}
		for (int i = 0; i < minY.Length; i++)
		{
			for (int j = minY[i]; j < maxY[i] + 1; j++)
				onPoint(i, j);
		}
	}

	public static void DrawCircle(int x, int y, int radius, Action<int, int> onPoint)
	{
		int r2 = radius * radius;
		for (int i = -radius + 1; i < radius; i++)
		{
			for (int j = -radius + 1; j < radius; j++)
			{
				if (i * i + j * j < r2)
					onPoint(x + i, y + j);
			}
		}
	}

	public static void DrawSideLines(int x1, int y1, int x2, int y2, int width, Action<int, int, float> onPoint, bool shortEnd=true)
	{
		Vector2 perp = new Vector2(x2 - x1, y2 - y1).normalized * width;
		if (shortEnd)
		{
			x1 += (int)Mathf.Round(perp.x);
			x2 -= (int)Mathf.Round(perp.x);
			y1 += (int)Mathf.Round(perp.y);
			y2 -= (int)Mathf.Round(perp.y);
		}
		int dx = -(int)Mathf.Round(perp.y);
		int dy = (int)Mathf.Round(perp.x);
		DrawLine(x1 + dx, y1 + dy, x2 + dx, y2 + dy, (x, y, p) => {
			onPoint(x, y, p);
		});
		DrawLine(x1 - dx, y1 - dy, x2 - dx, y2 - dy, (x, y, p) => {
			onPoint(x, y, p);
		});
	}

	public struct Pixel
	{
		public int x;
		public int y;

		public Pixel(int x, int y)
		{
			this.x = x;
			this.y = y;
		}
	}
}
