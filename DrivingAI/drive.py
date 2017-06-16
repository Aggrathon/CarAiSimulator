import tensorflow as tf
from model import Network
from communication import Driver
from record import read_data


def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    nn = Network()
    with Driver() as driver:
        def inp():
            x, v, y = driver.get_status()
            return [x], [v]
        def out(val):
            driver.set_action(val[0][0], val[0][1])
        nn.predict(inp, out)


if __name__ == "__main__":
    main()