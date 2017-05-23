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
}
