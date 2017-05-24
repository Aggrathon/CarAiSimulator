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

	public static void DrawFatLine(int x1, int y1, int x2, int y2, int width, Action<int, int, float> onPoint)
	{
		Vector2 perp = new Vector2(x2 - x1, y2 - y1).normalized * width;
		int dx = -(int)Mathf.Round(perp.y);
		int dy = (int)Mathf.Round(perp.x);
		int z2 = (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2)+width/4;
		if (Mathf.Abs(Mathf.Abs(dx)-Mathf.Abs(dy)) < width)
		{
			DrawLine(x1, y1, x2, y2, (x, y, p) => {
				DrawLine(x+dx, y+dy, x-dx, y-dy, (rx, ry, rp) => {
					int ax = rx - x1, ay = ry - y1;
					int bx = x2 - rx, by = y2 - ry;
					onPoint(rx + 1, ry + 1, Utils.GetWideLineProgress(z2, ax + 1, ay + 1, bx + 1, by - 1));
					onPoint(rx + 1, ry, Utils.GetWideLineProgress(z2, ax + 1, ay, bx + 1, by));
					onPoint(rx + 1, ry - 1, Utils.GetWideLineProgress(z2, ax + 1, ay - 1, bx + 1, by + 1));
					onPoint(rx, ry + 1, Utils.GetWideLineProgress(z2, ax, ay + 1, bx, by - 1));
					onPoint(rx, ry, p);
					onPoint(rx, ry - 1, Utils.GetWideLineProgress(z2, ax, ay - 1, bx, by + 1));
					onPoint(rx - 1, ry + 1, Utils.GetWideLineProgress(z2, ax - 1, ay + 1, bx - 1, by - 1));
					onPoint(rx - 1, ry, Utils.GetWideLineProgress(z2, ax - 1, ay, bx - 1, by));
					onPoint(rx - 1, ry - 1, Utils.GetWideLineProgress(z2, ax - 1, ay - 1, bx - 1, by + 1));
				});
			});
		}
		else
		{
			DrawLine(x1, y1, x2, y2, (x, y, p) => {
				DrawLine(x + dx, y + dy, x - dx, y - dy, (rx, ry, rp) => {
					onPoint(rx, ry, p);
				});
			});
		}
	}

	public static void DrawFatLine2(int x1, int y1, int x2, int y2, int width, Action<int, int, float> onPoint)
	{
		Vector2 perp = new Vector2(x2 - x1, y2 - y1).normalized * width;
		int dx = -(int)Mathf.Round(perp.y);
		int dy = (int)Mathf.Round(perp.x);

		float z2 = (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
		DrawPolygon((x, y) => {
			int ax = x - x1;
			int ay = y - y1;
			int bx = x - x2;
			int by = y - y2;
			float a2 = ax * ax + ay * ay;
			float b2 = bx * bx + by * by;
			onPoint(x, y, ((a2 - b2) / z2 + 1) / 2);
		},
		new Pixel(x1 + dx, y1 + dy),
		new Pixel(x1 - dx, y1 - dy),
		new Pixel(x2 + dx, y2 + dy),
		new Pixel(x2 - dx, y2 - dy)
		);
	}

	public static void DrawFatLine3(int x0, int y0, int x1, int y1, int width, Action<int, int, float> onPoint)
	{
		int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
		int dy = Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
		int err = dx - dy, e2, x2, y2;                          /* error value e_xy */
		float ed = dx + dy == 0 ? 1 : Mathf.Sqrt((float)dx * dx + (float)dy * dy);
		
		for (width = (width+1) / 2; ;)
		{                                   /* pixel loop */
			onPoint(x0, y0, 1);
			e2 = err; x2 = x0;
			if (2 * e2 >= -dx)
			{                                           /* x step */
				for (e2 += dy, y2 = y0; e2 < ed * width && (y1 != y2 || dx > dy); e2 += dx)
					onPoint(x0, y2 += sy, 1);
				if (x0 == x1) break;
				e2 = err; err -= dy; x0 += sx;
			}
			if (2 * e2 <= dy)
			{                                            /* y step */
				for (e2 = dx - e2; e2 < ed * width && (x1 != x2 || dx < dy); e2 += dy)
					onPoint(x2 += sx, y0, 1);
				if (y0 == y1) break;
				err += dx; y0 += sy;
			}
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

	public static void DrawSideLines(int x1, int y1, int x2, int y2, int width, Action<int, int, float> onPoint)
	{
		Vector2 perp = new Vector2(x2 - x1, y2 - y1).normalized * width;
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
