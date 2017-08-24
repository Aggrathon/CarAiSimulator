using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MotionBlur : MonoBehaviour {

	public Material effect;
	public UnityStandardAssets.Vehicles.Car.CarController car;

	int blurID;
	Material mat;
	float maxBlur;

	void Start()
	{
		blurID = Shader.PropertyToID("_BlurStrength");
		maxBlur = effect.GetFloat(blurID);
		mat = new Material(effect);
	}

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		float blur = Mathf.Abs(car.CurrentSpeed / car.MaxSpeed);
		mat.SetFloat(blurID, (blur*blur+blur)*maxBlur);
		Graphics.Blit(source, destination, mat);
	}

}
