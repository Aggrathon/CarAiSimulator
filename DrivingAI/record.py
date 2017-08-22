from communication import Recorder
from threading import Thread
from queue import Queue
from data import score_buffer, write_data


def record_data(data_queue):
    counter = 0
    buffer = score_buffer()
    with Recorder() as comm:
        print("Waiting for Data...               ", end='\r')
        try:
            data = comm.get_status()
            comm.send_heartbeat()
            while data is not None and len(data) > 0:
                buffer.add_item(*data[:-1], score=data[-1])
                for i in buffer.get_items():
                    i[-1] = i[-1]*0.6+0.4
                    data_queue.put(i)
                counter += 1
                print("Snapshots recieved:",counter, end='\r')
                data = comm.get_status()
                comm.send_heartbeat()
        except StopIteration:
            pass
        except KeyboardInterrupt:
            pass
    for i in buffer.clear_buffer():
        data_queue.put(i)
    print()


if __name__ == "__main__":
    q = Queue()
    for i in range(8):
        t = Thread(target=write_data, args=(q, i))
        t.daemon = True
        t.start()
    record_data(q)
    if q.qsize() > 0:
        print("Processing leftover snapshots (%i)"%q.qsize())
    q.join()