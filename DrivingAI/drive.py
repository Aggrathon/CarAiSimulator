import tensorflow as tf
from model import Session, get_network
from communication import Driver
from data import get_middle_lane


def drive_alternating(image_tensor, variable_tensor, input_fn, output_fn):
    """
        Drive a car, alternating between the networks
    """
    _, neta, netb = get_network(image_tensor, variable_tensor, training=False)
    try:
        with Session(False, False) as sess:
            while True:
                output_fn(sess.session.run(neta.output, feed_dict=input_fn()))
                output_fn(sess.session.run(netb.output, feed_dict=input_fn()))
    except (KeyboardInterrupt, StopIteration):
        pass

def drive_a(image_tensor, variable_tensor, input_fn, output_fn):
    """
        Drive a car using the first network
    """
    _, neta, _ = get_network(image_tensor, variable_tensor, training=False)
    try:
        with Session(False, False) as sess:
            while True:
                output_fn(sess.session.run(neta.output, feed_dict=input_fn()))
    except (KeyboardInterrupt, StopIteration):
        pass

def drive_b(image_tensor, variable_tensor, input_fn, output_fn):
    """
        Drive a car using the second network
    """
    _, _, netb = get_network(image_tensor, variable_tensor, training=False)
    try:
        with Session(False, False) as sess:
            while True:
                output_fn(sess.session.run(netb.output, feed_dict=input_fn()))
    except (KeyboardInterrupt, StopIteration):
        pass

def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    with Driver() as driver:
        def inp():
            x, v, y, s = driver.get_status()
            return {imgs: [x], vars: [v]}
        def out(val):
            h, v = val[0]
            print("Driving  |  h: %+.2f  v: %+.2f"%(h,v))
            driver.set_action(h, v)
        imgs = tf.placeholder(tf.float32, [None, 200*60*4])
        vars = tf.placeholder(tf.float32, [None, 3])
        drive_a(get_middle_lane(tf.reshape(imgs, [-1, 200, 60, 4])), vars, inp, out)

if __name__ == "__main__":
    main()