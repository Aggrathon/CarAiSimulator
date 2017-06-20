import tensorflow as tf
from record import read_data


def get_shuffle_batch(batch=64):
    return tf.train.shuffle_batch([*read_data()], batch, 8000, 1000)

def get_batch(batch=64):
    return tf.train.batch([*read_data()], batch, capacity=2000)

def create_lane_holder(image, variables, steering, pixel_shift=20, steering_shift=0.2):
    new_width = int(image.get_shape()[0])-2*pixel_shift
    image = tf.stack([
        tf.slice(image, [pixel_shift,0,0], [new_width, -1, -1]),
        tf.slice(image, [2*pixel_shift,0,0], [new_width, -1, -1]),
        tf.slice(image, [0,0,0], [new_width, -1, -1])
    ])
    variables = tf.stack([variables, variables, variables])
    st_const = tf.constant([steering_shift, 0.0], tf.float32)
    steering = tf.stack([steering, steering+st_const, steering-st_const])
    return image, variables, steering

def get_lane_shuffle_batch(batch=64, pixel_shift=20, steering_shift=0.2):
    return tf.train.shuffle_batch(
        tensors=[*create_lane_holder(*read_data(), pixel_shift, steering_shift)],
        batch_size=batch, capacity=8000, min_after_dequeue=1000, enqueue_many=True)

def get_middle_lane(image, variables, steering, pixel_shift=20):
    new_width = int(image.get_shape()[1])-2*pixel_shift
    return tf.slice(image, [0, pixel_shift, 0, 0], [-1, new_width, -1, -1]), variables, steering



