
using UnityEngine;
using System.Threading;
using System.Net.Sockets;

public class CommunicationManager : MonoBehaviour {

	const int PORT = 38698;
	const int BUFFER_SIZE = 1 << 16;
	const byte RECORD = 30;
	const byte DRIVE = 31;
	const byte DISCONNECT = 20;
	const byte PAUSE = 21;
	const byte PLAY = 22;
	const byte HEARTBEAT = 1;

	public CarSteering car;
	public GameObject connectButton;
	public GameObject disconnectButton;
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
	bool setFastForward;
	bool unsetFastForward;
	bool setPause;
	bool setPlay;
	bool endThread;
	bool hasReset;
	bool hasScored;

	float lastSend;
	int imageSize;
	int statusSize;
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
		setFastForward = false;
		unsetFastForward = false;
		setPause = false;
		setPlay = false;
		TimeManager.SetFastForwardPossible(false);
		endThread = false;

		track.onReset += OnReset;
		track.onWaypoint += OnScore;
		hasReset = true;
		hasScored = false;

		thread = new Thread(Thread);
		thread.Start();

		lastSend = 0;
	}

	private void OnDisable()
	{
		endThread = true;
		track.onReset -= OnReset;
		track.onWaypoint -= OnScore;
		car.userInput = true;
		if (connectButton)
			connectButton.SetActive(true);
		if (disconnectButton)
			disconnectButton.SetActive(false);
		TimeManager.SetFastForwardPossible(false);
		TimeManager.Play();
	}

	private void OnReset()
	{
		hasReset = true;
	}

	void OnScore()
	{
		hasScored = true;
	}

	private void Update()
	{
		if (thread == null || !thread.IsAlive)
		{
			car.userInput = true;
			enabled = false;
			thread = null;
			return;
		}
		if (requireTexture)
		{
			FillStatusBuffer();
			if (TimeManager.IsFastForwarding)
			{
				FillStatusBuffer();
				FillStatusBuffer();
			}
		}
		if (setFastForward)
		{
			TimeManager.SetFastForwardPossible(true);
			setFastForward = false;
		}
		else if (unsetFastForward)
		{
			TimeManager.SetFastForwardPossible(false);
			unsetFastForward = false;
		}
		else if (setPause)
		{
			setPause = false;
			TimeManager.Pause();
		}
		else if (setPlay)
		{
			setPlay = false;
			TimeManager.Play();
		}
	}

	void Thread()
	{
		Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		try
		{
			socket.Connect("localhost", PORT);
			while (!endThread)
			{
				if (socket.Receive(buffer) == 0)
					break;
				switch (buffer[0])
				{
					case RECORD:
						car.userInput = true;
						unsetFastForward = true;
						requireTexture = true;
						while (requireTexture) ;
						if (socket.Send(buffer, statusSize, SocketFlags.None) == 0)
							endThread = true;
						break;
					case DRIVE:
						car.userInput = false;
						car.horizontalSteering = ((float)buffer[1]) / 127.5f - 1f;
						car.verticalSteering = ((float)buffer[2]) / 127.5f - 1f;
						setFastForward = true;
						requireTexture = true;
						while (requireTexture) ;
						if (socket.Send(buffer, statusSize, SocketFlags.None) == 0)
							endThread = true;
						break;
					case HEARTBEAT:
						break;
					case DISCONNECT:
						endThread = true;
						break;
					case PLAY:
						setPlay = true;
						break;
					case PAUSE:
						setPause = true;
						break;
				}
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
			car.userInput = true;
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
			case 5:
				return;
			default:
				lastSend = Time.time + sendInterval;
				if (car.userInput)
					lastSend += sendInterval;
				RenderTexture.active = cameraView;
				texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
				texture.Apply();
				layer = 1;
				index = imageSize * 4;
				buffer[index++] = (byte)Mathf.Clamp(speedometer.speed * 4 + 100, 0, 255);
				buffer[index++] = (byte)(car.horizontalSteering * 127.5f + 127.5f);
				buffer[index++] = (byte)(car.verticalSteering * 127.5f + 127.5f);
				if (hasReset)
				{
					buffer[index++] = (byte)0;
					hasReset = false;
					hasScored = false;
				}
				else if (hasScored)
				{
					buffer[index++] = (byte)255;
					hasScored = false;
				}
				else
					buffer[index++] = (byte)(score.currentScore * 126 + 128);
				statusSize = index;
				break;
		}
	}
}