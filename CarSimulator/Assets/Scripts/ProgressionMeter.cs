using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProgressionMeter : MonoBehaviour {

	public TrackManager manager;
	public Slider slider;
	public Text text;

	string[] cache;
	float time = 0;
	int progression;

	void Start () {
		manager.onWaypoint += OnWayPoint;
		manager.track.onGenerated += OnGenerated;
		cache = new string[100];
		for (int i = 1; i < 99; i++)
		{
			cache[i] = i + " s";
		}
		cache[99] = "";
		cache[0] = "";
		time = Time.time;
	}

	void OnWayPoint()
	{
		slider.value = ++progression;
		text.text = cache[(int)Mathf.Min(Time.time-time+0.3f, 99f)];
		time = Time.time;
	}

	void OnGenerated()
	{
		progression = -1;
		slider.value = progression;
		slider.maxValue = manager.track.road.Count;
		time = Time.time;
		text.text = cache[0];
	}
}
