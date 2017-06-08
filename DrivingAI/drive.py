import tensorflow as tf
from network import Estimator
from communication import Driver


def main():
    tf.logging.set_verbosity(tf.logging.WARN)
    nn = Estimator()
    with Driver() as driver:
        def inp():
            res = driver.get_status()
            return tf.train.batch([res[0], res[1]], 1, 1, 1, False)
        while True:
            for p in nn.predict(input_fn=inp):
                driver.set_action(p['horizontal'], p['vertical'])
                break


if __name__ == "__main__":
    main()