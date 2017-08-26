using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(TerrainGenerator))]
[RequireComponent(typeof(RoadGenerator))]
public class TrackGenerator : MonoBehaviour {

	TerrainGenerator terrainGenerator;
	RoadGenerator roadGenerator;
	TerrainData terrainData;
	Terrain terrain;
	float[,] heights;
	float[,,] alphas;

	public bool generateOnStart = false;
	public ReflectionProbe reflection;
	public Transform water;
	public event Action onGenerated;

	[Header("Terrain Data")]
	public Vector3 size = new Vector3(4000, 200, 4000);
	public int heightMapResolution = 4097;
	public int textureMapResolution = 2048;
	[Header("Textures")]
	public Texture2D waterTexture;
	public const int WATER_TEXTURE_INDEX = 0;
	public Texture2D sandTexture;
	public const int SAND_TEXTURE_INDEX = 1;
	public Texture2D grassTexture;
	public const int GRASS_TEXTURE_INDEX = 2;
	public Texture2D mountainTexture;
	public const int MOUNTAIN_TEXTURE_INDEX = 3;
	public Texture2D roadTexture;
	public const int ROAD_TEXTURE_INDEX = 4;
	public Texture2D paintTexture;
	public const int PAINT_TEXTURE_INDEX = 5;

	public List<Vector3> road { get { return roadGenerator.road; } }

	private void Awake()
	{
		terrainGenerator = GetComponent<TerrainGenerator>();
		roadGenerator = GetComponent<RoadGenerator>();
	}

	private void Start()
	{
		if (generateOnStart)
		{
			Generate();
		}
	}

	[ContextMenu("Generate")]
	public void Generate()
	{
		StopAllCoroutines();
		StartCoroutine(Coroutine());
	}

	private void CreateTerrain()
	{
		terrainData = new TerrainData();
		terrainData.alphamapResolution = textureMapResolution;
		terrainData.heightmapResolution = heightMapResolution;
		terrainData.size = size;
		terrainData.thickness = 10;
		Texture2D[] tex = new Texture2D[] { waterTexture, sandTexture, grassTexture, mountainTexture, roadTexture, paintTexture };
		terrainData.splatPrototypes = tex.Select(t => { SplatPrototype sp = new SplatPrototype(); sp.texture = t; return sp; }).ToArray<SplatPrototype>();
		terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
	}


	IEnumerator Coroutine()
	{
		Utils.ClearMemory();
		if (terrainData == null || terrain == null)
		{
			CreateTerrain();
			yield return null;
		}

		heights = terrainData.GetHeights(0, 0, terrainData.heightmapWidth, terrainData.heightmapHeight);
		alphas = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
		Vector3 position = terrain.transform.position;
		float waterRelativeHeight = water.position.y / size.y;

		//Generate heights
		terrainGenerator.SetupConfig();
		bool terrainHeights1 = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.GenerateHeights(heights, 0, 4);
			terrainHeights1 = false;
		});
		bool terrainHeights2 = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.GenerateHeights(heights, 1, 4);
			terrainHeights2 = false;
		});
		bool terrainHeights3 = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.GenerateHeights(heights, 2, 4);
			terrainHeights3 = false;
		});
		bool terrainHeights4 = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.GenerateHeights(heights, 3, 4);
			terrainHeights4 = false;
		});

		while (terrainHeights1 || terrainHeights2 || terrainHeights3 || terrainHeights4)
			yield return null;
		//create roads
		bool road = true;
		ThreadPool.QueueUserWorkItem((o) => {
			roadGenerator.Generate(heights, size, position, waterRelativeHeight);
			road = false;
		});

		//paint textures
		bool terrainTextures = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.PrepareTextures(heights, waterRelativeHeight);
			terrainTextures = false;
		});
		while (terrainTextures)
			yield return null;
		bool terrainTextures1 = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.GenerateTextures(heights, alphas, 0, 4);
			terrainTextures1 = false;
		});
		bool terrainTextures2 = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.GenerateTextures(heights, alphas, 1, 4);
			terrainTextures2 = false;
		});
		bool terrainTextures3 = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.GenerateTextures(heights, alphas, 2, 4);
			terrainTextures3 = false;
		});
		bool terrainTextures4 = true;
		ThreadPool.QueueUserWorkItem((o) => {
			terrainGenerator.GenerateTextures(heights, alphas, 3, 4);
			terrainTextures4 = false;
		});

		//level roads
		while (road)
			yield return null;
		bool roadHeights = true;
		ThreadPool.QueueUserWorkItem((o) => {
			roadGenerator.DrawRoadHeights(heights, size, position, alphas.GetLength(0));
			roadHeights = false;
		});

		//paint roads
		while (road || terrainTextures1 || terrainTextures2 || terrainTextures3 || terrainTextures4)
			yield return null;
		bool roadTextures = true;
		ThreadPool.QueueUserWorkItem((o) => {
			roadGenerator.DrawRoadTextures(alphas, size, position);
			roadTextures = false;
		});

		while (roadTextures || roadHeights)
			yield return null;
		terrainData.SetHeights(0, 0, heights);
		terrainData.SetAlphamaps(0, 0, alphas);
		terrain.Flush();

		yield return null;
		reflection.RenderProbe();
		Utils.ClearMemory();
		if (onGenerated != null)
			onGenerated();
	}



	public bool IsRoad(Vector3 pos)
	{
		if (alphas == null)
			return false;
		int y = (int)((pos.x - terrain.transform.position.x) / size.x * alphas.GetLength(0));
		int x = (int)((pos.z - terrain.transform.position.z) / size.z * alphas.GetLength(1));
		return alphas[x, y, ROAD_TEXTURE_INDEX] > 0.5f || alphas[x, y, PAINT_TEXTURE_INDEX] > 0.5f;
	}
}
