
import socket

PORT = 38698
BUFFER_SIZE = 1 << 18
SIMULATOR_RECORD = 30
SIMULATOR_DRIVE = 31

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
        indata = [float(i)/.255 for i in data[:-4]]
        indata.append((float(data[-3])-100)*0.5) #speed
        indata.append(float(data[-4])/127.5*180-180) #direction
        out = [float(data[-2])/127.5-1, float(data[-1])/127.5-1]
        return indata, out
        
    def get_status(self):
        data = self.recieve()
        if data is None or len(data) == 0:
            raise StopIteration
            return None
        return Recorder.bytes_to_tensor(data)
    
    def get_status_bytes(self):
        data = self.recieve()
        if data is None or len(data) == 0:
            raise StopIteration
            return None
        return data


class Driver(Recorder):
    mode = SIMULATOR_DRIVE

    def set_action(self, h, v):
        print("Driving  |  h: %.2f  v: %.2f"%(h,v))
        self.send(bytes([int((h+1)*127.5), int((v+1)*127.5)]))



if __name__ == "__main__":
    with Communicator() as c:
        print(c.recieve())