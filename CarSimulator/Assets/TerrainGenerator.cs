using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

[RequireComponent(typeof(Terrain))]
[ExecuteInEditMode]
public class TerrainGenerator : MonoBehaviour
{

	public bool generateOnStart = false;
	public DetailLayer[] detailLayers;
	public Transform water;
	public ReflectionProbe reflection;

	Terrain terrain;
	Thread thread;
	float[,] tempHeights;
	float[,,] tempTextures;
	float waterHeight;

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
		if (thread != null)
			thread.Abort();
		tempHeights = null;
		tempTextures = null;
		int size = terrain.terrainData.heightmapResolution;
		float[,] heights = terrain.terrainData.GetHeights(0, 0, size, size);
		float mapHeight = terrain.terrainData.heightmapScale.y;
		float mapWidth = terrain.terrainData.heightmapWidth;
		int textureSize = terrain.terrainData.alphamapWidth;
		float[,,] textures = terrain.terrainData.GetAlphamaps(0, 0, textureSize, textureSize);
		for (int h = 0; h < detailLayers.Length; h++)
		{
			detailLayers[h].x = Random.Range(-1000f, 1000f);
			detailLayers[h].y = Random.Range(-1000f, 1000f);
		}
		thread = new Thread(() => {
			float sumHeight = 0f;
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					float px = (float)i / size * 2f - 1f;
					float py = (float)j / size * 2f - 1f;
					float height = Mathf.Sqrt(px * px + py * py)-0.3f;
					px = px * (float)mapWidth / 1000;
					py = py * (float)mapWidth / 1000;
					height = height*height * 0.7f + 0.25f;
					for (int h = 0; h < detailLayers.Length; h++)
					{
						float detail = Mathf.PerlinNoise(
							detailLayers[h].x + px * detailLayers[h].scale,
							detailLayers[h].y + py * detailLayers[h].scale
							) * 2 - 1;
						if(detailLayers[h].detail)
							height += Mathf.Clamp(detail * detail * detail, -0.75f, 0.75f) * detailLayers[h].height;
						else
							height += detail * detailLayers[h].height;
					}
					heights[i, j] = height;
					sumHeight += height;
				}
			}
			float meanHeight = sumHeight / (size * size);
			waterHeight = meanHeight / 1.8f * mapHeight;
			float sandHeight = meanHeight / 1.7f;
			float mountainHeight = meanHeight * 1.5f;
			for (int i = 0; i < textureSize; i++)
			{
				for (int j = 0; j < textureSize; j++)
				{
					int x = (int)((float)i / (float)textureSize * size);
					int y = (int)((float)j / (float)textureSize * size);
					textures[i, j, 3] = Mathf.Clamp01(1 - Mathf.Abs((heights[x, y] - sandHeight) * 10));
					textures[i, j, 2] = Mathf.Clamp01(Mathf.Pow((heights[x, y] - mountainHeight) * 8, 3));
					textures[i, j, 4] = 0;
					textures[i, j, 0] = 1 - textures[i, j, 3] - textures[i, j, 2];
				}
			}
			tempTextures = textures;
			tempHeights = heights;
		});
		thread.Start();
		StartCoroutine(WaitForResult());
		Debug.Log("Generating Terrain");
	}

	IEnumerator WaitForResult()
	{
		while (tempHeights == null || tempTextures == null)
			yield return null;
		terrain.terrainData.SetHeights(0, 0, tempHeights);
		terrain.terrainData.SetAlphamaps(0, 0, tempTextures);
		terrain.Flush();
		water.position = new Vector3(water.position.x, waterHeight, water.position.z);
		tempHeights = null;
		tempTextures = null;
		thread = null;
		Debug.Log("Generated Terrain");
		yield return null;
		RoadGenerator rgen = GetComponent<RoadGenerator>();
		if (rgen != null)
			rgen.Generate();
		else
		{
			reflection.RenderProbe();
		}
	}

	private void OnDestroy()
	{
		if (thread != null)
			thread.Abort();
	}

	[System.Serializable]
	public struct DetailLayer
	{
		[Range(0f, 40f)]
		public float scale;
		[Range(0f, 0.5f)]
		public float height;
		public bool detail;
		[System.NonSerialized]
		public float x;
		[System.NonSerialized]
		public float y;
	}
}
