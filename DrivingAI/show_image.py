from matplotlib import pyplot as plt
import numpy as np
from data import get_batch
from communication import Recorder
import tensorflow as tf

def show_image(x, input_fn=None):
    image1, _ = tf.split(x, [-1, 2], 1)
    image2 = tf.reshape(image1, [-1, 60, 200, 4])
    image3, _ = tf.split(image2, [3, 1], 3)
    image4 = image3
    with tf.Session() as sess:
        sess.run(tf.global_variables_initializer())
        tf.train.start_queue_runners(sess)
        if input_fn is None:
            img = sess.run(image4)
            plt.imshow(img[0])
            plt.show()
        else:
            try:
                while True:
                    img = sess.run(image4, input_fn())
                    plt.imshow(img[0])
                    plt.show()
            except StopIteration:
                pass


def show_record():
    x, _ = get_batch(1)
    show_image(x)

def show_connected():
    inp = tf.placeholder(tf.float32, [200*60*4+2])
    batch = tf.reshape(inp, [1, -1])
    with Recorder() as d:
        def inp_fn():
            x, _ = d.bytes_to_tensor(d.get_status_bytes())
            return {inp: x}
        show_image(batch, inp_fn)



if __name__ == "__main__":
    show_record()
    #show_connected()
