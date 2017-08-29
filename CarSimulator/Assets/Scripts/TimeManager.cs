using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
	static TimeManager instance;

	public float fastSpeed = 5f;
	public Toggle fastForwardButton;
	bool fastForwarding;
	bool fastForwardPossible;

	public static bool IsFastForwarding { get { return instance != null && instance.fastForwarding; } }

	public static void Pause()
	{
		Time.timeScale = 0;
		QualitySettings.vSyncCount = 1;
		AudioListener.pause = true;
		AudioListener.volume = 0.0f;
	}

	public static void Play()
	{
		if (instance != null && instance.fastForwarding)
		{
			Time.timeScale = instance.fastSpeed;
			QualitySettings.vSyncCount = 0;
			AudioListener.pause = true;
			AudioListener.volume = 0.0f;
		}
		else
		{
			Time.timeScale = 1;
			QualitySettings.vSyncCount = 1;
			AudioListener.pause = false;
			AudioListener.volume = 1.0f;
		}
	}

	public static void SetFastForwardPossible(bool state)
	{
		if (instance != null && state != instance.fastForwardPossible)
		{
			instance.fastForwardPossible = state;
			instance.fastForwarding = instance.fastForwarding && state;
			if (instance.fastForwardButton != null)
			{
				instance.fastForwardButton.gameObject.SetActive(state);
				if (!state && Time.timeScale > 0)
					Play();
				instance.fastForwardButton.isOn = instance.fastForwarding;
			}
		}
	}

	private void Awake()
	{
		instance = this;
		if(SystemInfo.graphicsDeviceID == 0)
		{
			fastSpeed *= 2;
			fastForwarding = true;
			fastForwardPossible = true;
		}
		else
		{
			fastForwardPossible = false;
			fastForwarding = false;
		}
		fastForwardButton.onValueChanged.AddListener(ToggleFastForward);
	}

	private void OnDestroy()
	{
		if (instance == this)
			instance = null;
	}

	public void ToggleFastForward(bool state)
	{
		fastForwarding = state;
		if (Time.timeScale > 0)
			Play();
	}
}
