using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
	public DetailLayer[] detailLayers;


	float meanHeight;
	float sandHeight;
	float mountainHeight;

	public void SetupConfig()
	{
		for (int h = 0; h < detailLayers.Length; h++)
		{
			detailLayers[h].x = Random.Range(-1000, 1000);
			detailLayers[h].y = Random.Range(-1000, 1000);
		}
	}

	public void GenerateHeights(float[,] heights, int threadIndex, int numThreads)
	{
		int size = heights.GetLength(0);
		int width = size / numThreads;
		int start = width * threadIndex;
		int end = threadIndex == numThreads - 1 ? size : width * (threadIndex + 1);

		for (int i = start; i < end; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float px = (float)i / size * 2f - 1f;
				float py = (float)j / size * 2f - 1f;
				float height = 0.95f - Mathf.Sqrt(px * px * 6 + py * py * 6) * 0.38f;
				px = px * (float)size / 1000f;
				py = py * (float)size / 1000f;
				for (int h = 0; h < detailLayers.Length; h++)
				{
					float detail = Mathf.PerlinNoise(
						detailLayers[h].x + px * detailLayers[h].scale,
						detailLayers[h].y + py * detailLayers[h].scale
						) * 2 - 1;
					if (detailLayers[h].detail)
						height += Mathf.Clamp(detail * detail * detail, -0.75f, 0.75f) * detailLayers[h].height;
					else
						height += detail * detailLayers[h].height;
				}
				heights[i, j] = height;
			}
		}
	}

	public void PrepareTextures(float[,] heights, float waterRelativeHeight)
	{
		int sizeHeight = heights.GetLength(0);
		float sumHeight = 0;
		for (int i = 0; i < sizeHeight; i++)
		{
			for (int j = 0; j < sizeHeight; j++)
			{
				sumHeight += heights[i, j];
			}
		}

		meanHeight = sumHeight / (sizeHeight * sizeHeight);
		sandHeight = waterRelativeHeight + 0.02f;
		mountainHeight = (meanHeight * 1.5f + 0.9f) * 0.5f;
	}

	public void GenerateTextures(float[,] heights, float[,,] alphas, int threadIndex, int numThreads)
	{
		int sizeHeight = heights.GetLength(0);
		int sizeAlpha = alphas.GetLength(0);
		int width = sizeAlpha / numThreads;
		int start = width * threadIndex;
		int end = threadIndex == numThreads - 1 ? sizeAlpha : width * (threadIndex + 1);

		for (int i = start; i < end; i++)
		{
			for (int j = 0; j < sizeAlpha; j++)
			{
				for (int k = 0; k < alphas.GetLength(2); k++)
					alphas[i, j, k] = 0;

				int x = (int)((float)i * (float)sizeHeight / (float)sizeAlpha);
				int y = (int)((float)j * (float)sizeHeight / (float)sizeAlpha);

				float sand = Mathf.Clamp01(1 - Mathf.Abs((heights[x, y] - sandHeight) * 3));
				sand = sand * sand;
				float mountain = (heights[x, y] - mountainHeight) * 8;
				mountain =  Mathf.Clamp01(mountain*mountain*mountain);

				alphas[i, j, TrackGenerator.SAND_TEXTURE_INDEX] = sand;
				alphas[i, j, TrackGenerator.MOUNTAIN_TEXTURE_INDEX] = mountain;
				if (heights[x, y] < sandHeight)
					alphas[i, j, TrackGenerator.WATER_TEXTURE_INDEX] = 1 - sand;
				else
					alphas[i, j, TrackGenerator.GRASS_TEXTURE_INDEX] = 1 - sand - mountain;
			}
		}
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
