using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MotionBlur : MonoBehaviour {

	public Material effect;
	public Speedometer speedometer;

	int blurID;

	void Start()
	{
		blurID = Shader.PropertyToID("_BlurStrength");
	}

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		float blur = Mathf.Abs(speedometer.speed * 3.6f / 150);
		effect.SetFloat(blurID, blur);
		Graphics.Blit(source, destination, effect);
	}

}
