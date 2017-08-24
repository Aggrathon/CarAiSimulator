using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class GlLineRenderer : MonoBehaviour {

	public LineRenderer lr;
	public Color[] colors;
	public Material mat;

	void OnPostRender()
	{
		if (lr == null || mat == null)
			return;
		mat.SetPass(0);
		GL.Begin(GL.LINES);
		if (colors.Length > 0)
			GL.Color(colors[0]);
		for (int i = 1; i < lr.positionCount; i++)
		{
			GL.Vertex(lr.GetPosition(i-1));
			if (colors.Length > i)
				GL.Color(colors[i]);
			GL.Vertex(lr.GetPosition(i));
		}
		GL.End();
	}
	

}
