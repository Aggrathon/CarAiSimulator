import tensorflow as tf
import numpy as np
from collections import deque
import os
import datetime

DATA_DIRECTORY = os.path.join('data', 'records')
DATA2_DIRECTORY = os.path.join('data', 'records2')
IMAGE_WIDTH = 140
IMAGE_HEIGHT = 60
IMAGE_DEPTH = 4
VARIABLE_COUNT = 1

def write_data():
    os.makedirs(DATA_DIRECTORY, exist_ok=True)
    timestamp = '{:%Y%m%d%H%M%S}'.format(datetime.datetime.now())
    writers = []
    for i in range(8):
        writers.append(tf.python_io.TFRecordWriter(os.path.join(DATA_DIRECTORY,"training_%s_%i.tfrecords"%(timestamp, i))))
    index = 0
    img, var, steer, score = read_data2()
    img = tf.concat((img, [0,0]), 0)
    sess = tf.Session()
    sess.run(tf.global_variables_initializer())
    sess.run(tf.local_variables_initializer())
    coord = tf.train.Coordinator()
    tf.train.start_queue_runners(sess, coord)
    try:
        while True:
            data = sess.run((img, var, steer, score))
            record = tf.train.Example(features=tf.train.Features(feature={
                'image': tf.train.Feature(float_list=tf.train.FloatList(value=data[0])),
                'variables': tf.train.Feature(float_list=tf.train.FloatList(value=data[1])),
                'steering': tf.train.Feature(float_list=tf.train.FloatList(value=data[2])),
                'score': tf.train.Feature(float_list=tf.train.FloatList(value=data[3]))
            }))
            writers[index%8].write(record.SerializeToString())
            index = index+1
            print("Reformatted:", index, end='\r')
    finally:
        for w in writers:
            w.close()
        coord.request_stop()
        coord.join()
        sess.close()


def read_data2(image_width=IMAGE_WIDTH, image_height=IMAGE_HEIGHT, image_depth=IMAGE_DEPTH, variable_count=VARIABLE_COUNT):
    os.makedirs(DATA2_DIRECTORY, exist_ok=True)
    reader = tf.TFRecordReader()
    files = [os.path.join(DATA2_DIRECTORY, f) for f in os.listdir(DATA2_DIRECTORY) if '.tfrecord' in f]
    np.random.shuffle(files)
    filename_queue = tf.train.string_input_producer(files, num_epochs=1)
    _, serialized_example = reader.read(filename_queue)
    features = tf.parse_single_example(
        serialized_example,
        features={
            'image': tf.FixedLenFeature([image_width*image_height*image_depth-2], tf.float32),
            'variables': tf.FixedLenFeature([variable_count], tf.float32),
            'steering': tf.FixedLenFeature([2], tf.float32),
            'score': tf.FixedLenFeature([1], tf.float32)
        })
    return features['image'], features['variables'], features['steering'], features['score']


if __name__ == "__main__":
    write_data()