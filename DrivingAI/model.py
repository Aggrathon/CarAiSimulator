import tensorflow as tf
import os

class Session():
    """
        Creates a session and loads the saved network
        use with for automatic closing
    """
    network_directory = os.path.join('data', 'network')
    model_file_name = os.path.join(network_directory, 'model')
    log_directory = os.path.join('data', 'logs')

    def __init__(self, save=False, summary=False, global_step=None):
        os.makedirs(self.log_directory, exist_ok=True)
        os.makedirs(self.network_directory, exist_ok=True)
        self._save = save
        self.saver = tf.train.Saver()
        self.global_step = global_step
        self.session = tf.Session()
        self.coord = tf.train.Coordinator()
        tf.train.start_queue_runners(self.session, self.coord)
        try:
            ckpt = tf.train.get_checkpoint_state(self.network_directory)
            if ckpt is None:
                self.session.run(tf.global_variables_initializer())
                print("\nCreated a new network\n")
            else:
                self.saver.restore(self.session, ckpt.model_checkpoint_path)
                print("\nLoaded an existing network\n")
        except Exception as e:
            self.session.run(tf.global_variables_initializer())
            print("\nCreated a new network (%s)\n"%repr(e))
        if summary:
            self.summary_ops = tf.summary.merge_all()
            self.summary_writer = tf.summary.FileWriter(self.log_directory, self.session.graph)
        else:
            self.summary_ops = None
            self.summary_writer = None
    
    def save_summary(self, global_step):
        if self.summary_writer is not None:
            self.summary_writer.add_summary(self.session.run(self.summary_ops), global_step)
    
    def save_network(self):
        if self._save:
            self.saver.save(self.session, self.model_file_name, self.global_step)

    def __enter__(self):
        return self

    def __exit__(self, type, value, traceback):
        self.save_network()
        self.coord.request_stop()
        self.coord.join()
        self.session.close()
        if self.summary_writer is not None:
            self.summary_writer.close()


def get_network(image, variables, example=None, weights=1.0, training=True):
    """
        Creates the double networks
    """
    global_step = tf.Variable(0, name='global_step')
    network_a = Network(image, variables, example, weights, training,  global_step=global_step, name="Network_A")
    network_b = Network(image, variables, example, weights, training, name="Network_B")
    return global_step, network_a, network_b


class Network():
    def __init__(self, input_image, input_variables, example_output=None, weights=1.0, training=True, global_step=None, name="Network"):
        self.name = name
        with tf.variable_scope(name):
            prev_layer = input_image
            # Convolutions
            for i, size in enumerate([32, 64, 128]):
                with tf.variable_scope('convolution_%d'%i):
                    prev_layer = tf.layers.conv2d(prev_layer, size, 5, 1, 'same', activation=tf.nn.relu)
                    prev_layer = tf.layers.max_pooling2d(prev_layer, 3, 2, 'valid')
                    prev_layer = tf.nn.local_response_normalization(prev_layer, 4, 1.0, 1e-4, 0.75)
            # Combine variables
            prev_layer = tf.contrib.layers.flatten(prev_layer)
            prev_layer = tf.concat([prev_layer, input_variables], 1)
            # Fully connected layers
            for i, size in enumerate([1024, 256, 64]):
                with tf.variable_scope('fully_connected_%d'%i):
                    prev_layer = tf.layers.dense(prev_layer, size, tf.nn.relu, use_bias=True)
                    if training:
                        prev_layer = tf.layers.dropout(prev_layer, 0.3)
            prev_layer = tf.contrib.layers.flatten(prev_layer)
            # Output
            self.output = tf.layers.dense(prev_layer, 2, activation=tf.nn.tanh, use_bias=True)
            self.horizontal, self.vertical = tf.split(self.output, [1,1], 1)
            tf.summary.histogram('Horizontal', self.horizontal)
            tf.summary.histogram('Vertical', self.vertical)
            # Trainers and losses here
            if example_output is not None:
                self.vars = tf.get_collection(tf.GraphKeys.TRAINABLE_VARIABLES, scope=name)
                if weights is 1.0:
                    self.loss = tf.losses.mean_squared_error(self.output, example_output)
                else:
                    loss_pos = tf.squared_difference(self.output, example_output)*tf.minimum(tf.maximum(0.0, weights), 1.0)
                    loss_neg = (1.0-tf.abs(self.output-example_output))*tf.minimum(tf.negative(tf.minimum(0.0, weights))*0.1, 0.05)
                    self.loss = tf.losses.compute_weighted_loss((loss_pos + loss_neg), reduction=tf.losses.Reduction.MEAN)
                adam = tf.train.AdamOptimizer(1e-5, 0.85)
                self.trainer = adam.minimize(self.loss, global_step, self.vars)
                tf.summary.scalar('Loss', self.loss)

