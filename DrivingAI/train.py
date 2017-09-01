from timeit import default_timer as timer
import tensorflow as tf
import numpy as np
from model import Session, get_network
from communication import Driver
from data import IMAGE_DEPTH, IMAGE_HEIGHT, IMAGE_WIDTH, VARIABLE_COUNT, score_buffer, get_shuffle_batch


def create_placeholders():
    #placeholders
    xp = tf.placeholder(tf.float32, None, "image")
    vp = tf.placeholder(tf.float32, None, "variables")
    yp = tf.placeholder(tf.float32, None, "steering")
    sp = tf.placeholder(tf.float32, None, "score")
    batch = tf.placeholder(tf.int32, None, "example_size")
    training = tf.placeholder(tf.bool, None, "training")
    #reshapes
    xs = tf.reshape(xp, [-1, IMAGE_WIDTH, IMAGE_HEIGHT, IMAGE_DEPTH])
    vs = tf.reshape(vp, [-1, VARIABLE_COUNT])
    ys = tf.reshape(yp, [-1, 2])
    ss = tf.reshape(sp, [-1, 1])
    #examples
    xe, ve, ye, se = get_shuffle_batch(batch, capacity=2000)
    #combines
    xc = tf.concat((xs, xe), 0)
    vc = tf.concat((vs, ve), 0)
    yc = tf.concat((ys, ye), 0)
    sc = tf.concat((ss, se), 0)
    return xp, vp, yp, sp, batch, training, xc, vc, yc, sc


def get_input(driver, session, neta, netb, tensor_placeholders, buffer=None, array=None):
    if buffer is None:
        buffer = score_buffer()
    if array is None:
        array = []
    h = 0
    v = 1
    per_network_size = 500
    def add_item(i):
        array.append(i)
        if i[-1] > 0:
            array.append(i)
            array.append(i)
            if i[-1] > i[-2][-1]+0.3:
                array.append(i)
                array.append(i)
    def fill_buffer(output, h,v):
        while buffer.get_num_scored() < per_network_size:
            x, v, y, s = driver.drive(h, v)
            y = session.run(output, feed_dict={ 
                tensor_placeholders[4]: 0,
                tensor_placeholders[0]: [x],
                tensor_placeholders[1]: [v],
                tensor_placeholders[5]: False
            })
            h = y[0][0]
            v = y[0][1]
            p = y[0][2]
            if np.random.uniform() < 0.1:
                h = np.clip(h + np.random.normal(0, 0.3) + np.random.normal(0, 0.2), -1, 1)
                v = np.clip(v + np.random.normal(0, 0.5) + np.random.normal(0, 0.5), -1, 1)
            buffer.add_item(x, v, [h, v, p], score=s)
        sum = 0
        for i in buffer.get_items():
            add_item(i)
            sum += i[-1]
        return h, v, sum
    driver.play()
    sum = 0
    for i in range(2):
        print("Filling the reinforcement buffer...     (A, Average: %.2f)  "%(sum/(i*per_network_size*2+0.1)), end='\r')
        h, v, s = fill_buffer(neta.output, h, v)
        sum += s
        print("Filling the reinforcement buffer...     (B, Average: %.2f)  "%(sum/(i*per_network_size*2+per_network_size)), end='\r')
        h, v, s = fill_buffer(netb.output, h, v)
        sum += s
    driver.pause()
    print("Filled the experience replay buffer (Average score: %.2f)          "%(sum/(2*per_network_size*2)))
    for i in buffer.clear_buffer():
        add_item(i)
    np.random.shuffle(array)
    return array


def get_batch_feed(array, tensor_placeholders, batch=32, example_count=12):
    x = []
    v = []
    y = []
    s = []
    for _ in range(batch-example_count):
        x_, v_, y_, s_ = array.pop()
        x.append(x_)
        v.append(v_)
        y.append(y_[:2])
        s.append(s_)
    return {
        tensor_placeholders[0]: x,
        tensor_placeholders[1]: v,
        tensor_placeholders[2]: y,
        tensor_placeholders[3]: s,
        tensor_placeholders[4]: example_count,
        tensor_placeholders[5]: True
    }


def train(iterations=80000, summary_interval=100, batch=32):
    tf.logging.set_verbosity(tf.logging.INFO)
    placeholders = create_placeholders()
    global_step, network_a, network_b = get_network(*placeholders[-4:], placeholders[5])
    with Session(True, True, global_step) as sess:
        with Driver() as driver:
            try:
                last_save = timer()
                array = []
                buffer = score_buffer()
                step = 0
                time = 1
                for _ in range(iterations):
                    if len(array) < batch*10:
                        get_input(driver, sess.session, network_a, network_b, placeholders, buffer, array)
                    pre = timer()
                    _, aloss, step = sess.session.run([network_a.trainer, network_a.loss, global_step], feed_dict=get_batch_feed(array, placeholders, batch, batch//2))
                    _, bloss = sess.session.run([network_b.trainer, network_b.loss], feed_dict=get_batch_feed(array, placeholders, batch, batch//2))
                    time = 0.9*time + 0.11 *(timer()-pre)
                    if step%10 == 0:
                        print("Training step: %i, Loss A: %.3f, Loss B: %.3f (%.2f s)  "%(step, aloss, bloss, time), end='\r')
                    if step%summary_interval == 0:
                        sess.save_summary(step, get_batch_feed(array, placeholders, batch, batch//2))
                        print()
                    if timer() - last_save > 1800:
                        sess.save_network()
                        last_save = timer()
            except (KeyboardInterrupt, StopIteration):
                print("\nStopping the training")


if __name__ == "__main__":
    train()