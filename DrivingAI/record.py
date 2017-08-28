from communication import Driver
from threading import Thread
from queue import Queue
from data import score_buffer, write_data


def record_data(data_queue):
    counter = 0
    buffer = score_buffer()
    with Driver() as comm:
        print("Waiting for Data...               ", end='\r')
        try:
            data = comm.record()
            comm.heartbeat()
            while data is not None and len(data) > 0:
                buffer.add_item(*data[:-1], score=data[-1])
                score = 0.0
                for d in buffer.get_items():
                    score = d[-1]
                    d[-1] = [d[-1]*0.6+0.4]
                    data_queue.put(d)
                counter += 1
                if score != 0.0:
                    print("Snapshots recieved:", counter, "  (%.2f) "%score, end='\r')
                else:
                    print("Snapshots recieved:", counter, "           ", end='\r')
                data = comm.record()
                comm.heartbeat()
        except StopIteration:
            pass
        except KeyboardInterrupt:
            pass
    for i in buffer.clear_buffer():
        i[-1] = [i[-1]*0.6+0.4]
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