
import socket
import sys

PORT = 38698
BUFFER_SIZE = 1 << 18
DISCONNECT = 20
HEARTBEAT = bytes([1])
PAUSE = bytes([21])
PLAY = bytes([22])
DRIVE = 31
RECORD = bytes([30])

class Communicator():

    def __init__(self):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server_socket:
            server_socket.bind(("localhost", PORT))
            server_socket.listen(1)
            print("Waiting for Connection...")
            self.socket, _ = server_socket.accept()
            print("Connection established")

    def recieve(self):
        try:
            data = self.socket.recv(BUFFER_SIZE)
            if data is None or len(data) == 0 or (len(data) == 1 and data[0] == DISCONNECT):
                return None
            return data
        except Exception as e:
            print(e)
            return None
    
    def send(self, data):
        try:
            self.socket.send(data)
        except Exception as e:
            print(e)
    
    def close(self):
        self.send(bytearray([DISCONNECT]))
        self.socket.close()
    
    def __enter__(self):
        return self
    
    def __exit__(self, a, b, c):
        self.close()


class Driver(Communicator):

    def __init__(self):
        super().__init__()
        self.send(HEARTBEAT)
    
    @classmethod
    def bytes_to_tensor(cls, data):
        image = [float(i)/255.0 for i in data[:-4]]
        variables = [
            (float(data[-4])-100)/3 #speed
        ]
        steer = [float(data[-3])/127.5-1, float(data[-2])/127.5-1]
        score = (float(data[-1])-128)/126
        return image, variables, steer, score
        
    def _get_status(self):
        data = self.recieve()
        if data is None:
            raise StopIteration
            return None
        return Driver.bytes_to_tensor(data)

    def record(self):
        self.send(RECORD)
        return self._get_status()
    
    def heartbeat(self):
        self.send(HEARTBEAT)

    def drive(self, h, v):
        h = max(min(1, h), -1)
        v = max(min(1, v), -1)
        data = bytes([DRIVE, int((h+1)*127.5), int((v+1)*127.5)])
        self.send(data)
        return self._get_status()
    
    def pause(self):
        self.send(PAUSE)
    
    def play(self):
        self.send(PLAY)


if __name__ == "__main__":
    with Communicator() as c:
        print(c.recieve())