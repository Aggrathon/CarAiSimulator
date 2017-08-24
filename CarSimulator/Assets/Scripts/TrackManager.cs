using System;
using UnityEngine;
using UnityEngine.UI;

public class TrackManager : MonoBehaviour {

	public TerrainGenerator terrain;
	public RoadGenerator road;
	public Rigidbody car;
	[Space]
	public GameObject waitForGenerationScreen;
	public Text resetText;
	public Slider progressionBar;
	[Space]
	[Range(0f, 20f)]
	public float resetTimeout = 10f;
	[Range(0f,120f)]
	public float waypointDistance = 50f;
	public bool generateOnLoad = false;
	[Header("GPS")]
	public LineRenderer gpsLine;
	public Vector3 gpsOffset;
	[Range(1f, 15f)]
	public float gpsCornerCutting = 8f;

	public event Action onReset;
	public Vector3 waypointPosition { get; protected set; }
	public Vector3 waypointNext { get; protected set; }
	public bool isResetting { get; protected set; }

	float resetTime = 0f;
	int checkpoint = 0;
	Vector3[] gpsPoints;
	Vector3[] gpsSmoothPoints;

	void Start () {
		if (generateOnLoad)
			GenerateTrack();
		else
			ResetCar();
		gpsLine.positionCount = 4;
		gpsPoints = new Vector3[4];
		gpsSmoothPoints = new Vector3[4];
	}

	[ContextMenu("Generate Track")]
	public void GenerateTrack()
	{
		TimeManager.Pause();
		waitForGenerationScreen.SetActive(true);
		terrain.Generate();
		road.onGenerated = () =>
		{
			waitForGenerationScreen.SetActive(false);
			checkpoint = 0;
			progressionBar.maxValue = road.road.Count;
			ResetCar();
			TimeManager.Play();
		};
	}

	[ContextMenu("Reset Car")]
	public void ResetCar()
	{
		if (checkpoint == road.road.Count)
			GenerateTrack();
		else
		{
			RaycastHit hit;
			if (Physics.Raycast(road.road[checkpoint] + new Vector3(0, 3, 0), Vector3.down, out hit, 10))
				car.MovePosition(hit.point + new Vector3(0, 0.5f, 0));
			else
				car.MovePosition(road.road[checkpoint] + new Vector3(0, 1, 0));
			car.MoveRotation(Quaternion.LookRotation(road.road[(checkpoint + 1) % road.road.Count] - road.road[checkpoint], Vector3.up));
			car.angularVelocity = Vector3.zero;
			car.velocity = car.transform.forward;
			resetTime = 0f;
			resetText.gameObject.SetActive(false);
			NextCheckpoint();
			CalculateGpsPoints();
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
		waypointPosition = road.road[checkpoint % road.road.Count];
		waypointNext = road.road[(checkpoint+1) % road.road.Count];
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
			isResetting = false;
		}
		else
		{
			resetTime += Time.deltaTime;
			if(resetTime > 4)
			{
				isResetting = true;
				resetText.gameObject.SetActive(true);
				resetText.text = "Resetting car in "+(int)(resetTimeout-resetTime)+" seconds";
				if(resetTime > resetTimeout)
				{
					ResetCar();
					if (onReset != null)
						onReset();
					isResetting = false;
				}
			}
		}
	}

	void CheckTrackProgression()
	{
		Vector3 direction = waypointPosition - car.position;
		//Check arrival
		if (Vector3.SqrMagnitude(direction) < waypointDistance * waypointDistance)
		{
			if (Vector3.SqrMagnitude(direction) < 0.35f * waypointDistance * waypointDistance
				|| Vector3.Angle(direction, road.road[(checkpoint + 1) % road.road.Count] - car.position) < 30)
				NextCheckpoint();
			direction = waypointPosition - car.position;
		}
		CalculateGpsPoints(100*Time.deltaTime);
	}

	void CalculateGpsPoints(float smoothing=100000)
	{
		gpsPoints[0] = car.transform.TransformPoint(gpsOffset);
		Vector3 prev = Vector3.ClampMagnitude(gpsPoints[0] - waypointPosition, gpsCornerCutting);
		Vector3 next = Vector3.ClampMagnitude(waypointNext - waypointPosition, gpsCornerCutting);
		prev.y *= 0.25f;
		next.y *= 0.25f;
		gpsPoints[1] = waypointPosition + prev + next * 0.25f;
		gpsPoints[2] = waypointPosition + next + prev * 0.25f;
		gpsPoints[3] = waypointNext;

		gpsSmoothPoints[0] = gpsPoints[0];
		for (int i = 1; i < gpsPoints.Length; i++)
			gpsSmoothPoints[i] = Vector3.MoveTowards(gpsSmoothPoints[i], gpsPoints[i], smoothing);
		gpsLine.SetPositions(gpsSmoothPoints);

	}
}
