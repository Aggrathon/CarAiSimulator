import tensorflow as tf
from communication import Communicator, SIMULATOR_RECORD
from threading import Thread
import os
import datetime


def record_data():
    counter = 0
    records = []
    with Communicator(SIMULATOR_RECORD) as comm:
        print("Waiting for Data...               ", end='\r')
        data = comm.recieve()
        while data is not None and len(data) > 0:
            records.append(data)
            counter += 1
            print("Snapshots recieved:",counter, end='\r')
            data = comm.recieve()
    print()
    return records

def save_data(records, start, step):
    counter = start
    os.makedirs("data", exist_ok=True)
    timestamp = '{:%Y%m%d%H%M%S}'.format(datetime.datetime.now())
    with tf.python_io.TFRecordWriter("data/training_%s_%i.tfrecords"%(timestamp, start)) as writer:
        while counter < len(records):
            data = records[counter]
            record = tf.train.Example(features=tf.train.Features(feature={
                'camera': tf.train.Feature(int64_list=tf.train.Int64List(value=data[:-4])),
                'direction': tf.train.Feature(float_list=tf.train.FloatList(value=[float(data[-4])/127.5*180-180])),
                'speed': tf.train.Feature(float_list=tf.train.FloatList(value=[(float(data[-3])-100)*0.5])),
                'horizontal': tf.train.Feature(float_list=tf.train.FloatList(value=[float(data[-2])/127.5-1])),
                'vertical': tf.train.Feature(float_list=tf.train.FloatList(value=[float(data[-1])/127.5-1]))
            }))
            writer.write(record.SerializeToString())
            counter += step
            if start == 0:
                print("Snapshots saved:", counter, end='\r')
    if start == 0:
        print()


def read_data(batch_size=64):
    reader = tf.TFRecordReader()
    filename_queue = tf.train.string_input_producer([os.path.join('data', f) for f in os.listdir('data') if '.tfrecord' in f])
    _, serialized_example = reader.read(filename_queue)
    features = tf.parse_single_example(
        serialized_example,
        features={
            'camera': tf.FixedLenFeature([256*128], tf.int64),
            'direction': tf.FixedLenFeature([], tf.float32),
            'speed': tf.FixedLenFeature([], tf.float32),
            'horizontal': tf.FixedLenFeature([], tf.float32),
            'vertical': tf.FixedLenFeature([], tf.float32)
        })
    
    camera = features['camera']
    speed = features['speed']
    direction = features['direction']
    horizontal = features['horizontal']
    vertical = features['vertical']
    
    camera = tf.cast(camera, tf.float32) / 255.
    x = tf.concat([camera, tf.reshape(speed, [1]), tf.reshape(direction, [1])], 0)
    y = tf.stack([horizontal, vertical], 0)
    return tf.train.shuffle_batch([x, y], batch_size, 2000, 1000)


if __name__ == "__main__":
    data = record_data()
    num_thr = 8
    threads = [Thread(target=save_data, args=(data, i, num_thr)) for i in range(num_thr)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()