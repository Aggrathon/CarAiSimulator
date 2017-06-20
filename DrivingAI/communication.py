
import socket
import sys

PORT = 38698
BUFFER_SIZE = 1 << 18
SIMULATOR_RECORD = 30
SIMULATOR_DRIVE = 31
HEARTBEAT = bytes([1])

class Communicator():

    def __init__(self):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server_socket:
            server_socket.bind(("localhost", PORT))
            server_socket.listen(1)
            print("Waiting for Connection...")
            self.socket, _ = server_socket.accept()
            print("Connection established")

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


class Recorder(Communicator):
    mode = SIMULATOR_RECORD

    def __init__(self):
        super().__init__()
        self.socket.send(bytearray([self.mode]))
    
    @classmethod
    def bytes_to_tensor(cls, data):
        image = [float(i)/255.0 for i in data[:-5]]
        variables = [
            float(data[-5])/127.5 - 1.0, float(data[-4])/127.5 - 1.0, #direction
            (float(data[-3])-100)/3 #speed
        ]
        outdata = [float(data[-2])/127.5-1, float(data[-1])/127.5-1]
        return image, variables, outdata
        
    def get_status(self):
        data = self.recieve()
        if data is None or len(data) == 0:
            raise StopIteration
            return None
        return Recorder.bytes_to_tensor(data)
    
    def get_status_bytes(self, heartbeat=True):
        data = self.recieve()
        if data is None or len(data) == 0:
            raise StopIteration
            return None
        if heartbeat:
            self.send_heartbeat()
        return data
    
    def send_heartbeat(self):
        self.send(HEARTBEAT)
        

class Driver(Recorder):
    mode = SIMULATOR_DRIVE

    def set_action(self, h, v):
        data = bytes([int((h+1)*127.5), int((v+1)*127.5)])
        #print("Driving  |  h: %+.2f  v: %+.2f"%(h,v))
        self.send(data)
    
    def get_score(self):
        self.get_status()
        self.send_heartbeat()
        data = self.recieve()
        score = int.from_bytes(data, sys.byteorder, signed=True)
        return score
        

if __name__ == "__main__":
    with Communicator() as c:
        print(c.recieve())