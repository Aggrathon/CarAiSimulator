using System;
using System.Collections.Generic;
using UnityEngine;

public static class Utils {

	public static void Swap(ref int a, ref int b)
	{
		int tmp = a;
		a = b;
		b = tmp;
	}

	public static float GetWideLineProgress(int length2, int ax, int ay, int bx, int by)
	{
		return ((float)(ax * ax + ay*ay - bx * bx - by*by) / (float)length2 + 1f) / 2f;
	}

	public static bool InMargin(int x, int y, int width, int height, int margin)
	{
		return x - margin > 0 && x + margin < width && y - margin > 0 && y + margin < height;
	}

	public static void ClearMemory()
	{
		System.GC.Collect();
		Resources.UnloadUnusedAssets();
	}
}
