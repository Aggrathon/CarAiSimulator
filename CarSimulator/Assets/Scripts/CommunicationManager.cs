using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Text;

public class CommunicationManager : MonoBehaviour {

	const int PORT = 38698;
	const int BUFFER_SIZE = 1 << 18;
	const byte SIMULATOR_RECORD = 30;
	const byte SIMULATOR_DRIVE = 31;

	public CarSteering car;
	public GameObject reconnectButton;
	public float connectionTimeout = 20;
	public RenderTexture cameraView;
	public TrackManager track;
	public Speedometer speedometer;
	[Range(0f,1f)]
	public float sendInterval = 0.1f;

	Thread thread;
	byte[] buffer;
	Texture2D texture;
	bool requireTexture;
	float lastSend;
	int imageSize;

	void OnEnable () {
		reconnectButton.SetActive(false);
		if (buffer == null) buffer = new byte[BUFFER_SIZE];
		if (thread != null && thread.IsAlive) thread.Abort();
		if (texture == null) texture = new Texture2D(cameraView.width, cameraView.height);
		imageSize = texture.width * texture.height;
		thread = new Thread(Thread);
		thread.Start();
		requireTexture = false;
	}

	private void OnDestroy()
	{
		if (thread != null && thread.IsAlive)
			thread.Abort();
	}

	private void Update()
	{
		if(requireTexture && Time.timeScale > 0 && lastSend < Time.time)
		{
			lastSend = Time.time + sendInterval;
			RenderTexture.active = cameraView;
			texture = new Texture2D(cameraView.width, cameraView.height);
			texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
			texture.Apply();
			for (int i = 0; i < texture.width; i++)
			{
				for (int j = 0; j < texture.height; j++)
				{
					Color32 c = texture.GetPixel(i, j);
					int index = 4 * (i + texture.width * j);
					buffer[index] = c.r;
					buffer[index+1] = c.g;
					buffer[index+2] = c.b;
					buffer[index+3] = c.a;
				}
			}
			requireTexture = false;
		}
		else if (thread == null || !thread.IsAlive)
		{
			if (!car.userInput) car.userInput = true;
			reconnectButton.SetActive(true);
			enabled = false;
			thread = null;
		}
	}

	void Thread()
	{
		Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		try
		{
			socket.Connect("localhost", PORT);
			socket.Receive(buffer);
			switch (buffer[0])
			{
				case SIMULATOR_RECORD:
					car.userInput = true;
					while (car.verticalSteering == 0f) ;
					while (true)
					{
						if (socket.Send(buffer, FillStatusBuffer(), SocketFlags.None) == 0)
							break;
					}
					break;
				case SIMULATOR_DRIVE:
					car.userInput = false;
					while (true)
					{
						if (socket.Send(buffer, FillStatusBuffer(), SocketFlags.None) == 0)
							break;
						if (socket.Receive(buffer) < 2)
							break;
						car.horizontalSteering = ((float)buffer[0]) / 127.5f - 1f;
						car.verticalSteering = ((float)buffer[1]) / 127.5f - 1f;
					}
					break;
			}
		}
		catch (ThreadAbortException)
		{
			thread = null;
		}
		catch (SocketException)
		{
			thread = null;
		}
		catch (System.Exception e)
		{
			thread = null;
			Debug.LogException(e);
		}
		finally
		{
			socket.Shutdown(SocketShutdown.Both);
			socket.Close();
			thread = null;
		}
	}

	int FillStatusBuffer()
	{
		requireTexture = true;
		while (requireTexture) ;
		buffer[imageSize*4 + 0] = (byte)((track.directionAngle / 180f + 1) * 127.5f);
		buffer[imageSize*4 + 1] = (byte)(speedometer.speed*3+100);
		buffer[imageSize*4 + 2] = (byte)(car.horizontalSteering * 127.5f + 127.5f);
		buffer[imageSize*4 + 3] = (byte)(car.verticalSteering * 127.5f + 127.5f);
		return imageSize*4 + 4;
	}

}