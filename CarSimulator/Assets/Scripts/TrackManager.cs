using System;
using UnityEngine;
using UnityEngine.UI;

public class TrackManager : MonoBehaviour {

	public TrackGenerator track;
	public Rigidbody car;
	[Space]
	public GameObject waitForGenerationScreen;
	public Text resetText;
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
	public event Action onWaypoint;
	public Vector3 waypointPosition { get; protected set; }
	public Vector3 waypointNext { get; protected set; }
	public bool isResetting { get; protected set; }

	float resetTime = 0f;
	int checkpoint = 0;
	Vector3[] gpsPoints;
	Vector3[] gpsSmoothPoints;

	void Start ()
	{
		track.onGenerated += OnTrackGenerated;
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
		car.velocity = Vector3.zero;
		track.Generate();
	}

	private void OnTrackGenerated()
	{
		waitForGenerationScreen.SetActive(false);
		checkpoint = -1;
		waypointPosition = track.road[track.road.Count-1];
		waypointNext = track.road[0];
		ResetCar();
		TimeManager.Play();
	}

	[ContextMenu("Reset Car")]
	public void ResetCar()
	{
		if (checkpoint == track.road.Count)
			GenerateTrack();
		else
		{
			RaycastHit hit;
			if (Physics.Raycast(waypointPosition + new Vector3(0, 1, 0), Vector3.down, out hit, 10))
				car.MovePosition(hit.point + new Vector3(0, 0.1f, 0));
			else
				car.MovePosition(waypointPosition + new Vector3(0, 0.1f, 0));
			car.MoveRotation(Quaternion.LookRotation(waypointNext - waypointPosition, Vector3.up));
			NextCheckpoint();
			car.angularVelocity = Vector3.zero;
			car.velocity = Vector3.zero;
			resetTime = 0f;
			resetText.gameObject.SetActive(false);
			CalculateGpsPoints();
			if (onReset != null)
				onReset();
		}
	}

	public void NextCheckpoint()
	{
		if (checkpoint == track.road.Count)
		{
			GenerateTrack();
			checkpoint = 0;
			if (onWaypoint != null)
				onWaypoint();
			return;
		}
		else
		{
			checkpoint++;
			waypointPosition = track.road[checkpoint % track.road.Count];
			waypointNext = track.road[(checkpoint + 1) % track.road.Count];
			if (onWaypoint != null)
				onWaypoint();
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
		if (track.IsRoad(car.position) && car.velocity.sqrMagnitude > 1f)
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
				|| Vector3.Angle(direction, waypointNext - car.position) < 30)
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
