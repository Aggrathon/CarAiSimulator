using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
	static TimeManager instance;

	public float fastSpeed = 5f;
	public Toggle fastForwardButton;
	bool fastForwarding;

	public static void Pause()
	{
		Time.timeScale = 0;
	}

	public static void Play()
	{
		if (instance != null && instance.fastForwarding)
		{
			Time.timeScale = instance.fastSpeed;
			QualitySettings.vSyncCount = 0;
			AudioListener.pause = true;
		}
		else
		{
			Time.timeScale = 1;
			QualitySettings.vSyncCount = 1;
			AudioListener.pause = false;
		}
	}

	public static void SetFastForwardPossible(bool state)
	{
		if (instance != null)
		{
			instance.fastForwardButton.gameObject.SetActive(state);
			if (!state)
			{
				instance.fastForwarding = false;
				if (Time.timeScale > 0)
					Play();
			}
			instance.fastForwardButton.isOn = instance.fastForwarding;
		}
	}

	private void Awake()
	{
		instance = this;
		if(SystemInfo.graphicsDeviceID == 0)
		{
			fastSpeed *= 2;
			fastForwarding = true;
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
