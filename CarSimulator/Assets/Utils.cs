using System;
using System.Collections.Generic;
using UnityEngine;

public static class Utils {

	public static void LineDraw(int x1, int y1, int x2, int y2, Action<int,int> onPoint)
	{
		float dx = x2 - x1;
		float dy = y2 - y1;
		float dxa = Mathf.Abs(dx);
		float dya = Mathf.Abs(dy);

		if(dxa > dya)
		{
			if(x2 < x1)
			{
				Swap(ref x1, ref x2);
				Swap(ref y1, ref y2);
				dy = -dy;
			}

			float derr = dya / dxa;
			float error = derr - 0.5f;

			int y = y1;
			int step = (int)Mathf.Sign(dy);
			for (int x = x1; x < x2; x++)
			{
				onPoint(x, y);
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
				onPoint(x1, y1);
				return;
			}
			if (y2 < y1)
			{
				Swap(ref x1, ref x2);
				Swap(ref y1, ref y2);
				dx = -dx;
			}
			float derr = dxa/dya;
			float error = derr - 0.5f;

			int x = x1;
			int step = (int)Mathf.Sign(dx);
			for (int y = y1; y < y2; y++)
			{
				onPoint(x, y);
				error += derr;
				if (error >= 0.5f)
				{
					x += step;
					error -= 1f;
				}
			}
		}
	}

	public static void DrawCircle(int x, int y, int radius, Action<int,int> onPoint)
	{
		int r2 = radius * radius;
		for (int i = -radius+1; i < radius; i++)
		{
			for (int j = -radius + 1; j < radius; j++)
			{
				if (i * i + j * j < r2)
					onPoint(x + i, y + j);
			}
		}
	}

	public static void Swap(ref int a, ref int b)
	{
		int tmp = a;
		a = b;
		b = tmp;
	}
}
