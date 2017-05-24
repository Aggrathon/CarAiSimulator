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
}
