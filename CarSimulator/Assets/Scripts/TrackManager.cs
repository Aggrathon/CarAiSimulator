using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrackManager : MonoBehaviour {

	public TerrainGenerator terrain;
	public RoadGenerator road;
	public Rigidbody car;
	[Space]
	public GameObject waitForGenerationScreen;
	public Text resetText;
	public RectTransform directionArrow;
	public Slider progressionBar;
	[Space]
	[Range(0f, 20f)]
	public float resetTimeout = 10f;
	[Range(0f,100f)]
	public float waypointDistance = 50f;
	public bool generateOnLoad = false;

	float resetTime = 0f;
	int checkpoint = 0;

	void Start () {
		if (generateOnLoad)
			GenerateTrack();
		else
			ResetCar();
	}

	[ContextMenu("Generate Track")]
	public void GenerateTrack()
	{
		Time.timeScale = 0f;
		waitForGenerationScreen.SetActive(true);
		terrain.Generate();
		road.onGenerated = () =>
		{
			Time.timeScale = 1f;
			waitForGenerationScreen.SetActive(false);
			checkpoint = 0;
			progressionBar.maxValue = road.road.Count;
			ResetCar();
		};
	}

	[ContextMenu("Reset Time")]
	public void ResetCar()
	{
		if (checkpoint == road.road.Count)
			GenerateTrack();
		else
		{
			car.MovePosition(road.road[checkpoint] + new Vector3(0, 2, 0));
			car.MoveRotation(Quaternion.LookRotation(road.road[(checkpoint + 1) % road.road.Count] - road.road[checkpoint]));
			car.angularVelocity = Vector3.zero;
			car.velocity = Vector3.zero;
			resetTime = 0f;
			resetText.gameObject.SetActive(false);
			NextCheckpoint();
		}
	}

	public void NextCheckpoint()
	{
		if (checkpoint == road.road.Count)
		{
			progressionBar.value = checkpoint;
			GenerateTrack();
			checkpoint = 0;
		}
		else
		{
			checkpoint++;
			progressionBar.value = checkpoint-1;
		}
	}

	void Update () {
		if (Time.timeScale > 0f)
		{
			CheckNeedReset();
			CheckTrackProgression();
		}
	}

	void CheckNeedReset()
	{
		if (road.IsRoad(car.position) && car.velocity.sqrMagnitude > 1f)
		{
			resetText.gameObject.SetActive(false);
			resetTime = 0f;
		}
		else
		{
			resetTime += Time.deltaTime;
			if(resetTime > 4)
			{
				resetText.gameObject.SetActive(true);
				resetText.text = "Resetting car in "+(int)(resetTimeout-resetTime)+" seconds";
				if(resetTime > resetTimeout)
				{
					ResetCar();
				}
			}
		}
	}

	void CheckTrackProgression()
	{
		Vector3 direction = road.road[checkpoint%road.road.Count] - car.position;
		directionArrow.rotation = Quaternion.Euler(0, 0, -Vector3.SignedAngle(car.transform.forward, direction, Vector3.up));
		if (Vector3.SqrMagnitude(direction) < waypointDistance * waypointDistance)
			NextCheckpoint();
	}
}
