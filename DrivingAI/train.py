import tensorflow as tf
from network import Estimator
from record import read_data

def main():
    tf.logging.set_verbosity(tf.logging.INFO)
    nn = Estimator()
    nn.train(input_fn=read_data, steps=3000)


if __name__ == "__main__":
    main()
