
using UnityEngine;
using System.Threading;
using System.Net.Sockets;

public class CommunicationManager : MonoBehaviour {

	const int PORT = 38698;
	const int BUFFER_SIZE = 1 << 18;
	const byte SIMULATOR_RECORD = 30;
	const byte SIMULATOR_DRIVE = 31;

	public CarSteering car;
	public GameObject connectButton;
	public GameObject disconnectButton;
	public float connectionTimeout = 30;
	public RenderTexture cameraView;
	public TrackManager track;
	public ScoreManager score;
	public Speedometer speedometer;
	[Range(0f,1f)]
	public float sendInterval = 0.1f;

	Thread thread;
	byte[] buffer;
	Texture2D texture;

	bool requireTexture;
	bool setupFastForward;
	bool toggleTimePause;
	bool endThread;
	bool hasReset;

	float lastSend;
	int imageSize;
	int layer;

	void OnEnable () {
		connectButton.SetActive(false);
		disconnectButton.SetActive(true);
		if (buffer == null) buffer = new byte[BUFFER_SIZE];
		if (thread != null && thread.IsAlive) thread.Abort();
		if (texture == null) texture = new Texture2D(cameraView.width, cameraView.height);
		imageSize = texture.width * texture.height;
		layer = 0;
		requireTexture = false;
		setupFastForward = false;
		TimeManager.SetFastForwardPossible(false);
		endThread = false;

		track.onReset += OnReset;
		hasReset = false;

		thread = new Thread(Thread);
		thread.Start();

		lastSend = 0;
	}

	private void OnDisable()
	{
		track.onReset -= OnReset;
		connectButton.SetActive(true);
		disconnectButton.SetActive(false);
		TimeManager.SetFastForwardPossible(false);
		if (thread != null)
		{
			endThread = true;
		}
	}

	private void OnReset()
	{
		hasReset = true;
	}

	private void Update()
	{
		if (thread == null || !thread.IsAlive)
		{
			if (!car.userInput) car.userInput = true;
			enabled = false;
			thread = null;
			return;
		}
		if (requireTexture)
		{
			FillStatusBuffer();
		}
		else if (setupFastForward)
		{
			TimeManager.SetFastForwardPossible(true);
			setupFastForward = false;
		}
		else if (toggleTimePause)
		{
			toggleTimePause = false;
			if (Time.timeScale > 0)
				TimeManager.Pause();
			else
				TimeManager.Play();
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
					while (!endThread)
					{
						requireTexture = true;
						while (requireTexture) ;
						if (socket.Send(buffer, imageSize * 4 + 6, SocketFlags.None) == 0)
							break;
						if (socket.Receive(buffer) == 0)
							break;
					}
					break;
				case SIMULATOR_DRIVE:
					setupFastForward = true;
					car.userInput = false;
					while (!endThread)
					{
						requireTexture = true;
						while (requireTexture) ;
						if (socket.Send(buffer, imageSize * 4 + 6, SocketFlags.None) == 0)
							break;
						int size = socket.Receive(buffer);
						if (size == 2)
						{
							car.horizontalSteering = ((float)buffer[0]) / 127.5f - 1f;
							car.verticalSteering = ((float)buffer[1]) / 127.5f - 1f;
						}
						else
							break;
					}
					break;
			}
		}
		catch (ThreadAbortException)
		{
		}
		catch (SocketException)
		{
		}
		catch (System.Exception e)
		{
			Debug.LogException(e);
		}
		finally
		{
			socket.Shutdown(SocketShutdown.Both);
			socket.Close();
			thread = null;
		}
	}

	void FillStatusBuffer()
	{
		if (Time.timeScale == 0 || (layer == 0 && lastSend >= Time.time))
			return;
		int index;
		switch (layer)
		{
			case 1:
				for (int i = 0; i < texture.width; i++)
				{
					for (int j = 0; j < texture.height; j++)
					{
						Color32 c = texture.GetPixel(i, j);
						index = 4 * (i + texture.width * j);
						buffer[index] = c.r;
					}
				}
				layer++;
				break;
			case 2:
				for (int i = 0; i < texture.width; i++)
				{
					for (int j = 0; j < texture.height; j++)
					{
						Color32 c = texture.GetPixel(i, j);
						index = 4 * (i + texture.width * j);
						buffer[index + 1] = c.g;
						buffer[index + 3] = c.a;
					}
				}
				layer++;
				break;
			case 3:
				for (int i = 0; i < texture.width; i++)
				{
					for (int j = 0; j < texture.height; j++)
					{
						Color32 c = texture.GetPixel(i, j);
						index = 4 * (i + texture.width * j);
						buffer[index + 2] = c.b;
					}
				}
				layer++;
				break;
			case 4:
				for (int i = 0; i < texture.width; i++)
				{
					for (int j = 0; j < texture.height; j++)
					{
						Color32 c = texture.GetPixel(i, j);
						index = 4 * (i + texture.width * j);
						buffer[index + 3] = c.a;
					}
				}
				layer = 0;
				requireTexture = false;
				break;
			default:
				RenderTexture.active = cameraView;
				texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
				texture.Apply();
				layer = 1;
				lastSend = Time.time + sendInterval;
				index = imageSize * 4;
				buffer[index++] = (byte)(speedometer.speed * 3 + 100);
				buffer[index++] = (byte)(car.horizontalSteering * 127.5f + 127.5f);
				buffer[index++] = (byte)(car.verticalSteering * 127.5f + 127.5f);
				if (hasReset)
				{
					buffer[imageSize * 4 + 5] = (byte)0;
					hasReset = false;
				}
				else
					buffer[imageSize * 4 + 5] = (byte)(score.currentScore * 127 + 128);
				break;
		}
	}
}