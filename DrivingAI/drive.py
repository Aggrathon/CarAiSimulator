import tensorflow as tf
from model import Session, get_network
from communication import Driver
from data import VARIABLE_COUNT, IMAGE_DEPTH, IMAGE_HEIGHT, IMAGE_WIDTH


def drive():
    """
        Drive a car, alternating between the networks
    """
    tf.logging.set_verbosity(tf.logging.INFO)
    imgs = tf.placeholder(tf.float32, [None, IMAGE_WIDTH*IMAGE_HEIGHT*IMAGE_DEPTH])
    vars = tf.placeholder(tf.float32, [None, VARIABLE_COUNT])
    _, neta, netb = get_network(tf.reshape(imgs, [-1, IMAGE_WIDTH, IMAGE_HEIGHT, IMAGE_DEPTH]), vars, training=False)
    with Session(False, False) as sess:
        with Driver() as driver:
            def inout(h, v):
                print("Driving  |  h: %+.2f  v: %+.2f"%(h,v), end='\r')
                x, v, y, s = driver.drive(h, v)
                return { imgs: [x], vars: [v] }
            try:
                h = 0
                v = 1
                while True:
                    h, v, _ = sess.session.run(neta.output, feed_dict=inout(h, v))[0]
                    h, v, _ = sess.session.run(netb.output, feed_dict=inout(h, v))[0]
            except (KeyboardInterrupt, StopIteration):
                pass

if __name__ == "__main__":
    drive()