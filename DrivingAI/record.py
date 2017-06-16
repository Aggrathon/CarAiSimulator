import tensorflow as tf
from communication import Recorder
from threading import Thread
from queue import Queue
import os
import datetime
import random

DATA_DIRECTORY = os.path.join('data', 'records')


def record_data(data_queue):
    counter = 0
    with Recorder() as comm:
        print("Waiting for Data...               ", end='\r')
        try:
            data = comm.get_status_bytes()
            while data is not None and len(data) > 0:
                data_queue.put(data)
                counter += 1
                print("Snapshots recieved:",counter, end='\r')
                data = comm.get_status_bytes()
        except StopIteration:
            pass
    print()

def write_data(data_queue, id):
    os.makedirs(DATA_DIRECTORY, exist_ok=True)
    timestamp = '{:%Y%m%d%H%M%S}'.format(datetime.datetime.now())
    with tf.python_io.TFRecordWriter(os.path.join(DATA_DIRECTORY,"training_%s_%i.tfrecords"%(timestamp, id))) as writer:
        while True:
            data_img, data_var, data_out = Recorder.bytes_to_tensor(data_queue.get())
            record = tf.train.Example(features=tf.train.Features(feature={
                'image': tf.train.Feature(float_list=tf.train.FloatList(value=data_img)),
                'variables': tf.train.Feature(float_list=tf.train.FloatList(value=data_var)),
                'steering': tf.train.Feature(float_list=tf.train.FloatList(value=data_out))
            }))
            writer.write(record.SerializeToString())
            data_queue.task_done()


def read_data(image_width=200, image_height=60, image_depth=4, variable_count=3):
    reader = tf.TFRecordReader()
    files = [os.path.join(DATA_DIRECTORY, f) for f in os.listdir(DATA_DIRECTORY) if '.tfrecord' in f]
    random.shuffle(files)
    filename_queue = tf.train.string_input_producer(files)
    _, serialized_example = reader.read(filename_queue)
    features = tf.parse_single_example(
        serialized_example,
        features={
            'image': tf.FixedLenFeature([image_width*image_height*image_depth], tf.float32),
            'variables': tf.FixedLenFeature([variable_count], tf.float32),
            'steering': tf.FixedLenFeature([2], tf.float32)
        })
    return tf.reshape(features['image'], [image_width, image_height, image_depth]), features['variables'], features['steering']


if __name__ == "__main__":
    q = Queue()
    num_thr = 8
    for i in range(num_thr):
        t = Thread(target=write_data, args=(q, i))
        t.daemon = True
        t.start()
    record_data(q)
    if q.qsize() > 0:
        print("Processing leftover snapshots (%i)"%q.qsize())
    q.join()