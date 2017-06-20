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
	public Text scoreText;
	[Space]
	[Range(0f, 20f)]
	public float resetTimeout = 10f;
	[Range(0f,100f)]
	public float waypointDistance = 50f;
	public bool generateOnLoad = false;
	[Header("Score")]
	public float resetPenalty = 100f;
	public float scorePerDistance = 0.1f;

	float resetTime = 0f;
	int checkpoint = 0;

	Vector3 scorePos;
	float scoreRaw;
	
	public float directionAngle { get; protected set; }
	public Vector2 directionVector { get; protected set; }
	public float score { get { return scoreRaw - resetTime / resetTimeout * resetPenalty; } }

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
			scorePos = road.road[checkpoint] + new Vector3(0, 0.5f, 0);
			car.MovePosition(road.road[checkpoint] + new Vector3(0, 1, 0));
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
			CalculateScore();
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
					scoreRaw -= resetPenalty;
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
			if (Vector3.SqrMagnitude(direction) < 0.3f * waypointDistance * waypointDistance
				|| Vector3.Angle(direction, road.road[(checkpoint+1) % road.road.Count] - car.position) < 30)
				NextCheckpoint();
		}
		direction = car.transform.InverseTransformDirection(direction);
		if (direction.sqrMagnitude == 0)
			directionVector = direction;
		else
			directionVector = direction.normalized;
	}

	void CalculateScore()
	{
		Vector3 curPos = car.position;
		float impr = Vector3.Distance(scorePos, road.road[checkpoint]) - Vector3.Distance(curPos, road.road[checkpoint]);
		if (impr >= 1)
		{
			scoreRaw += impr * scorePerDistance;
			scorePos = curPos;
		}
		scoreText.text = ((int)score).ToString();
	}
}
