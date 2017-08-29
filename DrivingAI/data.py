import tensorflow as tf
import numpy as np
from collections import deque
import os
import datetime

DATA_DIRECTORY = os.path.join('data', 'records')
IMAGE_WIDTH = 140
IMAGE_HEIGHT = 60
IMAGE_DEPTH = 4
VARIABLE_COUNT = 1

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


def read_data(image_width=IMAGE_WIDTH, image_height=IMAGE_HEIGHT, image_depth=IMAGE_DEPTH, variable_count=VARIABLE_COUNT):
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


def get_shuffle_batch(batch=16, capacity=8000, fixed_score=True):
    with tf.variable_scope("input"):
        tensors = read_data()
        if fixed_score:
            tensors = (*tensors[:-1], [1.0])
        return tf.train.shuffle_batch([*tensors], batch_size=batch, capacity=capacity, min_after_dequeue=capacity//8)


class score_buffer():

    def __init__(self, length=400, falloff=0.992, peak=40):
        self.length = length
        self.falloff = falloff
        self.peak = peak
        self.buffer = deque()
        self.to_score = 0
        self.sum = 0
        self.weights = []
        for i in range(length):
            self.weights.append(falloff**(i-self.peak if i > 0 else 2*self.peak-2*i))
            self.sum = self.sum + self.weights[i]
        self.scale = 0.2/length

    def add_item(self, *values, score):
        if score < -1:
            # The car has been reset
            rem = min(self.to_score, self.peak)
            for _ in range(rem):
                self.buffer.popleft()
            for i, ls in enumerate(self.buffer):
                if i > self.to_score - rem:
                    break
                ls[-2] = ls[-2] - self.weights[i//3]*0.5
            self.to_score = 0
            item = list((*values, 0, False))
        elif score > 1:
            # the car has reached a waypoint
            for i, ls in enumerate(self.buffer):
                if i > self.to_score:
                    break
                ls[-2] = ls[-2] + self.weights[i]
            item = list((*values, 0, False))
        else:
            danger = score < 0
            if not danger:
                score = score*0.5 -0.5
            item = list((*values, 0, danger))
            for i, ls in enumerate(self.buffer):
                if i > self.to_score:
                    break
                if ls[-1] and not danger:
                    ls[-2] = ls[-2] + self.weights[i] * 0.2
                    ls[-1] = False
                elif danger and not ls[-1]:
                    ls[-2] = ls[-2] - self.weights[i] * 0.2
                ls[-2] = ls[-2] + self.weights[i] * score * self.scale
        self.buffer.appendleft(item)
        self.to_score = min(self.to_score + 1, self.length-1)

    def get_items(self):
        while len(self.buffer) > self.to_score:
            item = self.buffer.pop()
            item.pop()
            yield item

    def clear_buffer(self):
        self.to_score = min(self.to_score, self.peak)
        for i in self.get_items():
            yield i
        self.buffer.clear()
        self.to_score = 0

    def get_num_scored(self):
        return len(self.buffer) - self.to_score
