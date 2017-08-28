using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ImageEffect : MonoBehaviour {

	public Material effect;
	public DepthTextureMode cameraDepth = DepthTextureMode.Depth;

	private void Start()
	{
		GetComponent<Camera>().depthTextureMode = cameraDepth;
	}

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		Graphics.Blit(source, destination, effect);
	}

}
