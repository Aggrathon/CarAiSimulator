
using UnityEngine;
using UnityEngine.UI;

public class Speedometer : MonoBehaviour {

	static string[] cache;

	public Text speedText;
	public Rigidbody car;

	[System.NonSerialized]
	public float speed;

	private void Start()
	{
		if (cache == null)
		{
			cache = new string[251];
			for (int i = 0; i < 251; i++)
			{
				cache[i] = (i - 100) + " km/h";
			}
		}
	}

	private void Update()
	{
		speed = Vector3.Dot(car.velocity, car.transform.forward);
		speedText.text = cache[Mathf.Clamp((int)(speed*3.6f+100f), 0, 250)];
		if (car.IsSleeping())
			car.WakeUp();
	}
}
