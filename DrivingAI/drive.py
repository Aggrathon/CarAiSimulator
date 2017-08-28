import tensorflow as tf
from model import Session, get_network
from communication import Driver
from data import VARIABLE_COUNT, IMAGE_DEPTH, IMAGE_HEIGHT, IMAGE_WIDTH


def drive_alternating(image_tensor, variable_tensor, inout_fn):
    """
        Drive a car, alternating between the networks
    """
    _, neta, netb = get_network(image_tensor, variable_tensor, training=False)
    try:
        h = 0
        v = 1
        with Session(False, False) as sess:
            while True:
                h, v = sess.session.run(neta.output, feed_dict=inout_fn(h, v))[0]
                h, v = sess.session.run(netb.output, feed_dict=inout_fn(h, v))[0]
    except (KeyboardInterrupt, StopIteration):
        pass

def drive_a(image_tensor, variable_tensor, inout_fn):
    """
        Drive a car using the first network
    """
    _, neta, _ = get_network(image_tensor, variable_tensor, training=False)
    try:
        h = 0
        v = 1
        with Session(False, False) as sess:
            while True:
                h, v = sess.session.run(neta.output, feed_dict=inout_fn(h, v))[0]
    except (KeyboardInterrupt, StopIteration):
        pass

def drive_b(image_tensor, variable_tensor, inout_fn):
    """
        Drive a car using the second network
    """
    _, _, netb = get_network(image_tensor, variable_tensor, training=False)
    try:
        h = 0
        v = 1
        with Session(False, False) as sess:
            while True:
                h, v = sess.session.run(netb.output, feed_dict=inout_fn(h, v))[0]
    except (KeyboardInterrupt, StopIteration):
        pass

def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    with Driver() as driver:
        imgs = tf.placeholder(tf.float32, [None, IMAGE_WIDTH*IMAGE_HEIGHT*IMAGE_DEPTH])
        vars = tf.placeholder(tf.float32, [None, VARIABLE_COUNT])
        def inout(h, v):
            print("Driving  |  h: %+.2f  v: %+.2f"%(h,v), end='\r')
            x, v, y, s = driver.drive(h, v)
            return { imgs: [x], vars: [v] }
        drive_alternating(tf.reshape(imgs, [-1, IMAGE_WIDTH, IMAGE_HEIGHT, IMAGE_DEPTH]), vars, inout)

if __name__ == "__main__":
    main()