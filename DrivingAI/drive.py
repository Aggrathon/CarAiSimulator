import tensorflow as tf
from model import Network
from communication import Driver
from record import read_data


def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    nn = Network()
    with Driver() as driver:
        def inp():
            x, y = driver.get_status()
            return [x]
        def out(val):
            driver.set_action(val[0], val[1])
        nn.predict(inp, out)

def test_tensor():
    tf.logging.set_verbosity(tf.logging.INFO)
    nn = Network()
    with Driver() as driver:
        def out(val):
            driver.set_action(val[0], val[1])
            driver.get_status()
        x, _ = read_data(1, False)
        driver.get_status()
        nn.predict_tensor(x, out)


if __name__ == "__main__":
    test_tensor()