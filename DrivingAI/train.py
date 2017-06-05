import tensorflow as tf
from network import Estimator
from data_recorder import read_data

def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    nn = Estimator()
    nn.train(input_fn=read_data, steps=30000)


if __name__ == "__main__":
    main()
