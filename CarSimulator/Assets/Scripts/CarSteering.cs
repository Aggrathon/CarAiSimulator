using System;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

[RequireComponent(typeof (CarController))]
public class CarSteering : MonoBehaviour
{
	[SerializeField]
	bool _userInput = true;
	public bool userInput {
		get { return _userInput; }
		set {
			_userInput = value;
			horizontalSteering = 0f;
			verticalSteering = 0f;
			handbrake = 0f;
		}
	}

    private CarController car;
	[NonSerialized]
	public float horizontalSteering;
	[NonSerialized]
	public float verticalSteering;
	[NonSerialized]
	public float handbrake;

    private void Awake()
    {
        car = GetComponent<CarController>();
    }

	private void Update()
	{
		if(userInput)
		{
			horizontalSteering = Input.GetAxis("Horizontal");
			verticalSteering = Input.GetAxis("Vertical");
			handbrake = Input.GetAxis("Jump");
		}
	}

	private void FixedUpdate()
    {
        car.Move(horizontalSteering, verticalSteering, verticalSteering, handbrake);
    }
}
