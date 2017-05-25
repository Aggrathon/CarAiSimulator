using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Speedometer : MonoBehaviour {

	public Text speedText;
	public Rigidbody car;

	private void Update()
	{
		float speed = Vector3.Dot(car.velocity, car.transform.forward);
		speedText.text = string.Format("{0 :0.0} km/h", speed*3.6f);
	}
}
