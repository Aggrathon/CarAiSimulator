import tensorflow as tf
from model import Network
from record import read_data

def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    nn = Network()
    nn.train(*read_data(), 30000)


if __name__ == "__main__":
    main()
