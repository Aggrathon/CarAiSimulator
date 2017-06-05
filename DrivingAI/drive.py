import tensorflow as tf
from network import Estimator
from communication import Driver


def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    nn = Estimator()
    with Driver() as driver:
        for p in nn.predict(input_fn=driver.get_status):
            driver.set_action(p['output'][0], p['output'][1])


if __name__ == "__main__":
    main()