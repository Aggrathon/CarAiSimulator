from timeit import default_timer as timer
import tensorflow as tf
import numpy as np
from model import Session, get_network
from communication import Driver
from data import IMAGE_DEPTH, IMAGE_HEIGHT, IMAGE_WIDTH, VARIABLE_COUNT, get_middle_lane, score_buffer, get_shuffle_batch


def get_input(driver, session, neta, netb, input_img, input_vars, buffer=None, array=None):
    if buffer is None:
        buffer = score_buffer()
    if array is None:
        array = []
    def fill_buffer(output):
        print("Filling the reinforcement buffer...     ", end='\r')
        for _ in range(200):
            x, v, y, s = driver.get_status()
            y = session.run(output, feed_dict={ input_img: [x], input_vars: [v] })
            y1 = y[0][0]
            y2 = y[0][1]
            if np.random.uniform() < 0.2:
                y1 = np.clip(y1 + np.random.normal(0, 0.1) + np.random.normal(0, 0.1)+ np.random.normal(0, 0.1), -1, 1)
                y2 = np.clip(y2 + np.random.normal(0, 0.1) + np.random.normal(0, 0.1)+ np.random.normal(0, 0.1), -1, 1)
            driver.set_action(y1, y2)
            buffer.add_item([x, v, [y1, y2], s])
    for _ in range(5):
        fill_buffer(neta.output)
        fill_buffer(netb.output)
    for i in buffer.clear_buffer():
        array.append(i)
        array.append(i)
    np.random.shuffle(array)
    return array

def get_batch_feed(array, tensor_img, tensor_vars, tensor_output, tensor_weights, batch=32):
    x = []
    v = []
    y = []
    s = []
    for _ in range(batch):
        x_, v_, y_, s_ = array.pop()
        x.append(x_)
        v.append(v_)
        y.append(y_)
        s.append(s_)
    return { tensor_img: x, tensor_vars: v, tensor_output: y, tensor_weights: s }


def train(iterations=80000, summary_interval=100, batch=32):
    tf.logging.set_verbosity(tf.logging.INFO)
    with Driver() as driver:
        x = tf.placeholder(tf.float32, [None, IMAGE_DEPTH*IMAGE_HEIGHT*IMAGE_WIDTH], "image")
        v = tf.placeholder(tf.float32, [None, VARIABLE_COUNT], "variables")
        y = tf.placeholder(tf.float32, [None, 2], "steering")
        s = tf.placeholder(tf.float32, [None, 1], "weights")
        global_step, network_a, network_b = get_network(get_middle_lane(tf.reshape(x, [-1, IMAGE_WIDTH, IMAGE_HEIGHT, IMAGE_DEPTH])), v, y, s, True)
        with Session(True, True, global_step) as sess:
            try:
                last_save = timer()
                buffer = score_buffer()
                array = []
                step = 1
                time = 1
                for _ in range(iterations):
                    if len(array) < batch*2:
                        get_input(driver, sess.session, network_a, network_b, x, v, buffer, array)
                    pre = timer()
                    fd = get_batch_feed(array, x, v, y, s, batch)
                    _, aloss, step = sess.session.run([network_a.trainer, network_a.loss, global_step], feed_dict=fd)
                    fd = get_batch_feed(array, x, v, y, s, batch)
                    _, bloss = sess.session.run([network_b.trainer, network_b.loss], feed_dict=fd)
                    if step%summary_interval == 0:
                        sess.save_summary(step)
                        print()
                    time = 0.9*time + 0.1 *(timer()-pre)
                    if step%10 == 0:
                        print("Training step: %i, Loss A: %.3f, Loss B: %.3f (%.2f s)  "%(step, aloss, bloss, time), end='\r')
                    if timer() - last_save > 1800:
                        sess.save_network()
                        last_save = timer()
            except (KeyboardInterrupt, StopIteration):
                print("\nStopping the training")


if __name__ == "__main__":
    train()