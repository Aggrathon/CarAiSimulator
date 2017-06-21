import tensorflow as tf
from model import Network
from communication import Driver


def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    nn = Network()
    with Driver() as driver:
        def inp():
            x, v, y = driver.get_status()
            return [x], [v]
        def out(val):
            driver.set_action(val[0][0], val[0][1])
        def score():
            return [driver.get_score()]
        def cont():
            driver.send_heartbeat()
        nn.train(inp, out, score, cont, 64, 50000)


if __name__ == "__main__":
    main()