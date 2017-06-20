import tensorflow as tf
from model import Network

if __name__ == "__main__":
    tf.logging.set_verbosity(tf.logging.INFO)
    Network().learn(16, 50000)
