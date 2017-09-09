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
        self.saver = tf.train.Saver(tf.trainable_variables())
        self.global_step = global_step
        self.session = tf.Session()
        self.session.run(tf.global_variables_initializer())
        self.session.run(tf.local_variables_initializer())
        self.coord = tf.train.Coordinator()
        tf.train.start_queue_runners(self.session, self.coord)
        try:
            ckpt = tf.train.get_checkpoint_state(self.network_directory)
            if ckpt is None:
                print("\nCreated a new network\n")
            else:
                self.saver.restore(self.session, ckpt.model_checkpoint_path)
                print("\nLoaded an existing network\n")
        except Exception as e:
            print("\nCreated a new network (%s)\n"%repr(e))
        if summary:
            self.summary_ops = tf.summary.merge_all()
            self.summary_writer = tf.summary.FileWriter(self.log_directory, self.session.graph)
        else:
            self.summary_ops = None
            self.summary_writer = None
    
    def save_summary(self, global_step, fd=None):
        if self.summary_writer is not None:
            self.summary_writer.add_summary(self.session.run(self.summary_ops, feed_dict=fd), global_step)
    
    def save_network(self):
        if self._save:
            self.saver.save(self.session, self.model_file_name, self.global_step, write_meta_graph=False)

    def __enter__(self):
        return self

    def __exit__(self, type, value, traceback):
        self.save_network()
        self.coord.request_stop()
        self.coord.join()
        self.session.close()
        if self.summary_writer is not None:
            self.summary_writer.close()


def get_network(image, variables, example=None, score=1.0, training=True):
    """
        Creates the double networks
    """
    global_step = tf.Variable(0, name='global_step')
    network_a = Network(image, variables, example, score, training,  global_step=global_step, name="Network_A")
    network_b = Network(image, variables, example, score, training, name="Network_B")
    return global_step, network_a, network_b


class Network():
    def __init__(self, input_image, input_variables, example_output=None, score=None, training=False, global_step=None, name="Network"):
        self.name = name
        with tf.variable_scope(name):
            prev_layer = input_image
            # Convolutions
            for i, size in enumerate([32, 64, 96]):
                with tf.variable_scope('convolution_%d'%i):
                    prev_layer = tf.layers.conv2d(prev_layer, size, 7-i*2, 1, 'valid', activation=tf.nn.relu)
                    if i < 2:
                        prev_layer = tf.layers.max_pooling2d(prev_layer, 3, 2, 'valid')
                    tf.layers.batch_normalization(prev_layer, training=training)
            # Combine variables
            prev_layer = tf.contrib.layers.flatten(prev_layer)
            prev_layer = tf.concat([prev_layer, input_variables], 1)
            # Fully connected layers
            for i, size in enumerate([1024, 512]):
                with tf.variable_scope('fully_connected_%d'%i):
                    prev_layer = tf.layers.dense(prev_layer, size, tf.nn.relu, use_bias=True)
                    prev_layer = tf.layers.dropout(prev_layer, 0.3, training=training)
            # Output
            self.output = tf.layers.dense(prev_layer, 3, use_bias=True)
            self.horizontal, self.vertical, self.prediction = tf.split(self.output, [1,1,1], 1)
            tf.summary.histogram('Horizontal', self.horizontal)
            tf.summary.histogram('Vertical', self.vertical)
            # Trainers and losses here
            if example_output is not None and score is not None:
                self.vars = tf.get_collection(tf.GraphKeys.TRAINABLE_VARIABLES, scope=name)
                self.update_ops = tf.get_collection(tf.GraphKeys.UPDATE_OPS, scope=name)
                steer, _ = tf.split(self.output, [2,1], 1)
                loss_pos = tf.squared_difference(steer, example_output)*tf.minimum(tf.maximum(0.0, score), 1.0)
                loss_neg = (1.0-tf.abs(steer-example_output))*tf.minimum(tf.negative(tf.minimum(0.0, score))*0.1, 0.1)
                loss_pred = tf.squared_difference(self.prediction*0.1, score*0.1)
                self.loss = tf.losses.compute_weighted_loss((loss_pos + loss_neg + loss_pred), reduction=tf.losses.Reduction.MEAN)
                with tf.control_dependencies(self.update_ops):
                    self.trainer = tf.train.AdamOptimizer(1e-5).minimize(self.loss, global_step, self.vars)
                with tf.name_scope('Loss'):
                    tf.summary.scalar('Sum', self.loss)
                    tf.summary.scalar('Positive', tf.reduce_mean(loss_pos))
                    tf.summary.scalar('Negative', tf.reduce_mean(loss_neg))
                    tf.summary.scalar('Prediction', tf.reduce_mean(loss_pred))

