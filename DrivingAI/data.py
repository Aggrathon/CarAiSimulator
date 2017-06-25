import tensorflow as tf
import numpy as np
from collections import deque
import os
import datetime

DATA_DIRECTORY = os.path.join('data', 'records')

def write_data(data_queue, id):
    os.makedirs(DATA_DIRECTORY, exist_ok=True)
    timestamp = '{:%Y%m%d%H%M%S}'.format(datetime.datetime.now())
    with tf.python_io.TFRecordWriter(os.path.join(DATA_DIRECTORY,"training_%s_%i.tfrecords"%(timestamp, id))) as writer:
        while True:
            data = data_queue.get()
            record = tf.train.Example(features=tf.train.Features(feature={
                'image': tf.train.Feature(float_list=tf.train.FloatList(value=data[0])),
                'variables': tf.train.Feature(float_list=tf.train.FloatList(value=data[1])),
                'steering': tf.train.Feature(float_list=tf.train.FloatList(value=data[2])),
                'score': tf.train.Feature(float_list=tf.train.FloatList(value=data[3]))
            })) 
            writer.write(record.SerializeToString())
            data_queue.task_done()


def read_data(image_width=200, image_height=60, image_depth=4, variable_count=3):
    reader = tf.TFRecordReader()
    files = [os.path.join(DATA_DIRECTORY, f) for f in os.listdir(DATA_DIRECTORY) if '.tfrecord' in f]
    np.random.shuffle(files)
    filename_queue = tf.train.string_input_producer(files)
    _, serialized_example = reader.read(filename_queue)
    features = tf.parse_single_example(
        serialized_example,
        features={
            'image': tf.FixedLenFeature([image_width*image_height*image_depth], tf.float32),
            'variables': tf.FixedLenFeature([variable_count], tf.float32),
            'steering': tf.FixedLenFeature([2], tf.float32),
            'score': tf.FixedLenFeature([1], tf.float32)
        })
    return tf.reshape(features['image'], [image_width, image_height, image_depth]), \
        features['variables'], features['steering'], features['score']


def create_lane_variations(image, variables, steering, score, pixel_shift=20, steering_shift=0.2):
    new_width = int(image.get_shape()[0])-2*pixel_shift
    image = tf.stack([
        tf.slice(image, [pixel_shift,0,0], [new_width, -1, -1]),
        tf.slice(image, [2*pixel_shift,0,0], [new_width, -1, -1]),
        tf.slice(image, [0,0,0], [new_width, -1, -1])
    ])
    variables = tf.stack([variables, variables, variables])
    st_const = tf.constant([steering_shift, 0.0], tf.float32)
    steering = tf.stack([steering, steering+st_const, steering-st_const])
    score = tf.stack([score, score, score])
    return image, variables, steering, score


def get_shuffle_batch(batch=64, pixel_shift=20, steering_shift=0.2):
    with tf.variable_scope("input"):
        return tf.train.shuffle_batch(
            tensors=[*create_lane_variations(*read_data(), pixel_shift, steering_shift)],
            batch_size=batch, capacity=8000, min_after_dequeue=1000, enqueue_many=True)

def get_middle_lane(image, variables, steering, score, pixel_shift=20):
    new_width = int(image.get_shape()[1])-2*pixel_shift
    return tf.slice(image, [0, pixel_shift, 0, 0], [-1, new_width, -1, -1]), variables, steering, score


class score_buffer():

    def __init__(self, length=64, falloff=0.95):
        self.length = length
        self.falloff = falloff
        self.buffer = deque()
        self.to_score = 0
        self.sum = 0
        for i in range(length):
            self.sum = self.sum + falloff**i
    
    def add_item(self, item):
        falloff = 1
        for i, ls in enumerate(self.buffer):
            if i > self.length or i > self.to_score:
                break
            ls[-1][0] = ls[-1][0]+falloff*item[-1][0]
            falloff = falloff*self.falloff
        if item[-1][0] == 0:
            for _ in range(min(16, self.to_score)):
                self.buffer.popleft()
            self.buffer.appendleft(item)
            self.to_score = 1
        else:
            item[-1][0] = 0
            self.buffer.appendleft(item)
            self.to_score = self.to_score + 1
    
    def get_items(self):
        while len(self.buffer) > self.to_score or len(self.buffer) > self.length:
            yield self.buffer.pop()
    
    def clear_buffer(self):
        self.to_score = min(self.to_score, 16)
        ret = [i for i in self.get_items()]
        self.buffer.clear()
        self.to_score = 0
        return ret
