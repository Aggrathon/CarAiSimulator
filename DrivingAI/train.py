import tensorflow as tf
from model import DoubleNetwork
from communication import Driver
from data import IMAGE_DEPTH, IMAGE_HEIGHT, IMAGE_WIDTH, VARIABLE_COUNT, get_middle_lane


def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    x = tf.placeholder(tf.float32, [None, IMAGE_DEPTH*IMAGE_HEIGHT*IMAGE_WIDTH], "image")
    v = tf.placeholder(tf.float32, [None, VARIABLE_COUNT], "variables")
    y = tf.placeholder(tf.float32, [None, 2], "steering")
    s = tf.placeholder(tf.float32, [None, 1], "score")
    training = tf.placeholder(tf.float32, name="training")
    with DoubleNetwork(get_middle_lane(tf.reshape(x, [-1, IMAGE_WIDTH, IMAGE_HEIGHT, IMAGE_DEPTH])), v, y, s, training) as nn:
        with Driver() as driver:
            def inp():
                x_, v_, y_, s_ = driver.get_status()
                return {x: [x_], v:[v_], training:False}, x_, v_, s_
            def out(val):
                driver.set_action(val[0][0], val[0][1])
            def feed(x_, v_, y_, s_):
                return {x: x_, v:v_, y:y_, s:s_, training:True}
            nn.train(inp, out, feed, 16, 50000)  


if __name__ == "__main__":
    main()