
using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using System.Net.Sockets;

public class CommunicationManager : MonoBehaviour {

	const int PORT = 38698;
	const int BUFFER_SIZE = 1 << 18;
	const byte SIMULATOR_RECORD = 30;
	const byte SIMULATOR_DRIVE = 31;

	public CarSteering car;
	public GameObject reconnectButton;
	public Toggle fastForwardButton;
	public float connectionTimeout = 20;
	public RenderTexture cameraView;
	public TrackManager track;
	public Speedometer speedometer;
	[Range(0f,1f)]
	public float sendInterval = 0.1f;
	[Range(1f, 20f)]
	public float fastForwardSpeed = 5f;

	Thread thread;
	byte[] buffer;
	Texture2D texture;
	bool requireTexture;
	float lastSend;
	int imageSize;
	bool setupFastForward;
	int layer;

	void OnEnable () {
		reconnectButton.SetActive(false);
		fastForwardButton.isOn = false;
		fastForwardButton.gameObject.SetActive(false);
		if (buffer == null) buffer = new byte[BUFFER_SIZE];
		if (thread != null && thread.IsAlive) thread.Abort();
		if (texture == null) texture = new Texture2D(cameraView.width, cameraView.height);
		imageSize = texture.width * texture.height;
		layer = 0;
		requireTexture = false;
		setupFastForward = false;
		DisableFastForward();
		thread = new Thread(Thread);
		thread.Start();
	}

	private void OnDisable()
	{
		if (thread != null && thread.IsAlive)
			thread.Abort();
		DisableFastForward();
	}

	private void OnDestroy()
	{
		if (thread != null && thread.IsAlive)
			thread.Abort();
	}

	private void Update()
	{
		if(requireTexture && Time.timeScale > 0 && (layer != 0 ||  lastSend < Time.time))
		{
			switch (layer)
			{
				case 1:
					for (int i = 0; i < texture.width; i++)
					{
						for (int j = 0; j < texture.height; j++)
						{
							Color32 c = texture.GetPixel(i, j);
							int index = 4 * (i + texture.width * j);
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
							int index = 4 * (i + texture.width * j);
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
							int index = 4 * (i + texture.width * j);
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
							int index = 4 * (i + texture.width * j);
							buffer[index + 3] = c.a;
						}
					}
					layer = 0;
					requireTexture = false;
					Destroy(texture);
					break;
				default:
					RenderTexture.active = cameraView;
					texture = new Texture2D(cameraView.width, cameraView.height);
					texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
					texture.Apply();
					layer = 1;
					lastSend = Time.time + sendInterval;
					buffer[imageSize * 4 + 0] = (byte)((track.directionVector.x + 1) * 127.5f);
					buffer[imageSize * 4 + 1] = (byte)((track.directionVector.y + 1) * 127.5f);
					buffer[imageSize * 4 + 2] = (byte)(speedometer.speed * 3 + 100);
					buffer[imageSize * 4 + 3] = (byte)(car.horizontalSteering * 127.5f + 127.5f);
					buffer[imageSize * 4 + 4] = (byte)(car.verticalSteering * 127.5f + 127.5f);
					buffer[imageSize * 4 + 5] = (byte)(track.score * 255f);
					break;
			}
		}
		else if (setupFastForward)
		{
			fastForwardButton.gameObject.SetActive(true);
			fastForwardButton.onValueChanged.RemoveAllListeners();
			fastForwardButton.onValueChanged.AddListener((v) =>
			{
				if (v)
					EnableFastForward();
				else
					DisableFastForward();
			});
			setupFastForward = false;
			if (SystemInfo.graphicsDeviceID == 0)
				EnableFastForward();
			else
				DisableFastForward();
		}
		if (thread == null || !thread.IsAlive)
		{
			if (!car.userInput) car.userInput = true;
			reconnectButton.SetActive(true);
			enabled = false;
			thread = null;
			fastForwardButton.gameObject.SetActive(false);
			DisableFastForward();
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
						FillStatusBuffer();
						if (socket.Send(buffer, imageSize * 4 + 6, SocketFlags.None) == 0)
							break;
						if (socket.Receive(buffer) == 0)
							break;
					}
					break;
				case SIMULATOR_DRIVE:
					setupFastForward = true;
					car.userInput = false;
					while (true)
					{
						FillStatusBuffer();
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

	void FillStatusBuffer()
	{
		requireTexture = true;
		while (requireTexture);
	}


	void EnableFastForward()
	{
		if (!fastForwardButton.isOn)
			fastForwardButton.isOn = true;
		if (Time.timeScale > 0)
			Time.timeScale = fastForwardSpeed;
		QualitySettings.vSyncCount = 0;
		Time.fixedDeltaTime = 0.02f / fastForwardSpeed;
		Camera.main.rect = new Rect(0.4f, 0.4f, 0.2f, 0.2f);
		AudioListener.pause = true;
	}

	void DisableFastForward()
	{
		if(fastForwardButton.isOn)
			fastForwardButton.isOn = false;
		if (Time.timeScale > 0)
			Time.timeScale = 1;
		Time.fixedDeltaTime = 0.01f;
		Camera.main.rect = new Rect(0, 0, 1, 1);
		AudioListener.pause = false;
	}
}