using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

[RequireComponent(typeof(Terrain))]
[ExecuteInEditMode]
public class TerrainGenerator : MonoBehaviour
{

	public bool generateOnStart = false;
	[Space]
	[Range(0f, 0.1f)]
	public float largeScale = 0.01f;
	[Range(0f, 0.2f)]
	public float detailScale = 0.05f;

	Terrain terrain;
	Thread thread;
	float[,] tempHeights;

	private void Awake()
	{
		terrain = GetComponent<Terrain>();
	}

	void Start()
	{
		if (generateOnStart)
			Generate();
	}

	[ContextMenu("Generate")]
	public void Generate()
	{
		tempHeights = null;
		int size = terrain.terrainData.heightmapResolution;
		float[,] heights = terrain.terrainData.GetHeights(0, 0, size, size);
		float x1 = Random.Range(0f, 1000f);
		float y1 = Random.Range(0f, 1000f);
		float x2 = Random.Range(-1000f, 0f);
		float y2 = Random.Range(-1000f, 0f);

		thread = new Thread(() => {
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					float h = Mathf.PerlinNoise(x1 + i * largeScale, y1 + j * largeScale);
					float px = (float)i / size * 2f - 1f;
					float py = (float)j / size * 2f - 1f;
					h += Mathf.Sqrt(px * px + py * py);
					h += Mathf.PerlinNoise(x2 + i * detailScale, y2 + j * detailScale) * 0.2f;
					h /= 2.2f; 
					heights[i, j] = h;
				}
			}
			tempHeights = heights;
		});
		thread.Start();
		StartCoroutine(WaitForResult());
	}

	IEnumerator WaitForResult()
	{
		while (tempHeights == null)
			yield return null;
		terrain.terrainData.SetHeights(0, 0, tempHeights);
		terrain.Flush();
		tempHeights = null;
		thread = null;
	}

	private void OnDestroy()
	{
		if (thread != null)
			thread.Abort();
	}
}
