using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TrackManager))]
public class ScoreManager : MonoBehaviour {

	TrackManager manager;

	public float totalScore { get; protected set; }
	public float currentScore { get; protected set; }

	public float targetSpeed = 100f;
	public float resetPenalty = 10;


	void Start () {
		manager = GetComponent<TrackManager>();
		manager.onReset += OnReset;
	}
	

	void Update () {
		if (Time.timeScale > 0)
		{
			currentScore = manager.isResetting ? -1 : 0;
			float velocity = manager.car.velocity.magnitude;
			velocity = Mathf.Max(1 - velocity / targetSpeed, 0);
			float direction = Vector3.Dot(manager.car.velocity, manager.waypointPosition - manager.car.position);
			direction = Mathf.Max(1 - direction / targetSpeed, 0);
			currentScore += 0.5f - 0.5f * velocity * velocity;
			currentScore += 0.5f - 0.5f * direction * direction;
			totalScore += Time.deltaTime * currentScore;
		}
	}

	void OnReset()
	{
		totalScore -= resetPenalty;
	}
	
}
