import tensorflow as tf
from model import DoubleNetwork
from communication import Driver
from data import get_middle_lane


def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    imgs = tf.placeholder(tf.float32, [None, 200*60*4])
    vars = tf.placeholder(tf.float32, [None, 3])
    nn = DoubleNetwork(get_middle_lane(tf.reshape(imgs, [-1, 200, 60, 4])), vars, None, None, False)
    with Driver() as driver:
        def inp():
            x, v, y, s = driver.get_status()
            return {imgs: [x], vars: [v]}
        def out(val):
            h, v = val[0]
            print("Driving  |  h: %+.2f  v: %+.2f"%(h,v))
            driver.set_action(h, v)
        nn.drive(inp, out)

if __name__ == "__main__":
    main()