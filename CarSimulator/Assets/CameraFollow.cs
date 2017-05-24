using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour {

	public Transform target;
	[Range(0f, 1f)]
	public float horizontalDampening = 0.1f;
	[Range(1,40)]
	public float directionDampening = 10f;

	Quaternion origRotation;

	private void Start()
	{
		origRotation = transform.rotation;
	}

	void Update () {
		if(target != null && target.gameObject.activeSelf)
		{
			transform.position = target.position;
			Vector3 ownRot = transform.eulerAngles;
			Vector3 targetRot = target.eulerAngles;
			Quaternion targetQuat = target.rotation * origRotation;
			transform.rotation = Quaternion.Euler(
				Quaternion.Lerp(targetQuat, origRotation, horizontalDampening).eulerAngles.x,
				Mathf.LerpAngle(ownRot.y, targetRot.y, Time.deltaTime*directionDampening),
				0);
		}
	}
}
