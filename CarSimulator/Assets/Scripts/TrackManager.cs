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
	[Range(0f,120f)]
	public float waypointDistance = 50f;
	public bool generateOnLoad = false;
	[Header("Score")]
	[Range(0f, 1f)]
	public float scoreBalanceResetDistance = 0.4f;
	public float targetSpeed = 100f;

	float resetTime = 0f;
	int checkpoint = 0;

	bool hasReset;
	
	public float directionAngle { get; protected set; }
	public Vector2 directionVector { get; protected set; }
	public float score {
		get {
			if (hasReset)
			{
				hasReset = false;
				return 0;
			}
			float forward = Vector3.Dot(car.velocity, (road.road[(checkpoint + 1) % road.road.Count] - road.road[checkpoint % road.road.Count]).normalized);
			float score = resetTime <= 0 ? 1 - scoreBalanceResetDistance : 0;
			float speed = Mathf.Min(targetSpeed, forward * 3.6f) / targetSpeed;
			score += (1-(1-speed)*(1-speed)) * scoreBalanceResetDistance;
			return score * 0.9f + 0.1f;
		}
	}

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
					hasReset = true;
				}
			}
		}
	}

	void CheckTrackProgression()
	{
		Vector3 direction = road.road[checkpoint%road.road.Count] - car.position;
		directionAngle = Vector3.SignedAngle(car.transform.forward, direction, Vector3.up);
		directionArrow.rotation = Quaternion.Euler(0, 0, -directionAngle);
		if (Vector3.SqrMagnitude(direction) < waypointDistance * waypointDistance)
		{
			if (Vector3.SqrMagnitude(direction) < 0.35f * waypointDistance * waypointDistance
				|| Vector3.Angle(direction, road.road[(checkpoint+1) % road.road.Count] - car.position) < 30)
				NextCheckpoint();
		}
		direction = car.transform.InverseTransformDirection(direction);
		if (direction.sqrMagnitude == 0)
			directionVector = direction;
		else
			directionVector = direction.normalized;
	}
}
