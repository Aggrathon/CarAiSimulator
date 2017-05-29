
import socket

PORT = 38698
BUFFER_SIZE = 1 << 18
SIMULATOR_RECORD = 30
SIMULATOR_DRIVE = 31

class Communicator():

    def __init__(self, mode=SIMULATOR_RECORD):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server_socket:
            server_socket.bind(("localhost", PORT))
            server_socket.listen(1)
            print("Waiting for Connection...", end='\r')
            self.socket, _ = server_socket.accept()
            self.socket.send(bytearray([mode]))

    def recieve(self):
        return self.socket.recv(BUFFER_SIZE)
    
    def send(self, data):
        self.socket.send(data)
    
    def close(self):
        self.socket.close()
    
    def __enter__(self):
        return self
    
    def __exit__(self, a, b, c):
        self.close()

if __name__ == "__main__":
    with Communicator() as c:
        print(c.recieve())