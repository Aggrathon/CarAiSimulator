using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Text;

public class CommunicationManager : MonoBehaviour {

	const int PORT = 38698;
	const byte SIMULATOR_RECORD = 30;
	const byte SIMULATOR_DRIVE = 31;

	Thread thread;
	byte[] buffer;

	void Start () {
		buffer = new byte[256];
		thread = new Thread(Thread);
		thread.Start();
	}

	private void OnDestroy()
	{
		if (thread != null)
			thread.Abort();
	}

	void Thread()
	{
		Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		socket.Connect("localhost", PORT);
		socket.Receive(buffer);
		switch (buffer[0])
		{
			case SIMULATOR_RECORD:
				break;
			case SIMULATOR_DRIVE:
				break;
		}
		socket.Send(Encoding.UTF8.GetBytes("Sent From Unity"));
		socket.Shutdown(SocketShutdown.Both);
		socket.Close();
		thread = null;
	}

}