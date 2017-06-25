import tensorflow as tf
from timeit import default_timer as timer
import os
from data import get_lane_shuffle_batch, get_middle_lane


class DoubleNetwork():
    network_directory = os.path.join('data', 'network')
    log_directory = os.path.join('data', 'logs')
    model_file_name = os.path.join(network_directory, 'model')
    
    def __init__(self, images, variables, outputs=None, weights=1, training=True):
        os.makedirs(self.log_directory, exist_ok=True)
        os.makedirs(self.network_directory, exist_ok=True)
        self.global_step = tf.Variable(0, name='global_step')
        self.network_a = Network(images, variables, outputs, training, global_step=self.global_step, name="Network_A")
        self.network_b = Network(images, variables, outputs, training, name="Network_B")
        if training:
            self.saver = tf.train.Saver()
        else:
            self.saver = None
        self.session = tf.Session()
        tf.train.start_queue_runners(self.session)
        self.session.run(tf.global_variables_initializer())
        try:
            ckpt = tf.train.get_checkpoint_state(self.network_directory)
            if ckpt is None:
                print("\nCreated a new network\n")
            else:
                self.saver.restore(self.session, ckpt.model_checkpoint_path)
                print("\nLoaded an existing network\n")
        except Exception as e:
            print("\nCreated a new network (%s)\n"%repr(e))

    def __enter__(self):
        return self

    def __exit__(self, type, value, traceback):
        if self.saver is not None:
            self.saver.save(self.session, self.model_file_name, self.global_step)
        self.session.close()
    
    def learn(self, iterations=50000, summary_interval=100):
        summary_ops = tf.summary.merge_all()
        summary_writer = tf.summary.FileWriter(self.log_directory, self.session.graph)
        try:
            last_save = timer()
            step = 1
            for _ in range(iterations):
                pre = timer()
                _, aloss, step = self.session.run([self.network_a.trainer, self.network_a.loss, self.global_step])
                _, bloss = self.session.run([self.network_b.trainer, self.network_b.loss])
                if step%summary_interval == 0:
                    summary_writer.add_summary(self.session.run(summary_ops), step)
                if step%10 == 0:
                    print("Training step: %i, Loss A: %.3f, Loss B: %.3f (%.2f s)"%(step, aloss, bloss, (timer()-pre)))
                if timer() - last_save > 1800:
                    self.saver.save(self.session, self.model_file_name, self.global_step)
                    last_save = timer()
        except KeyboardInterrupt:
            print("Stopping the training")
        finally:
            summary_writer.close()

    def drive(self, input_fn, output_fn, use_network_a=True):
        net = self.network_a if use_network_a else self.network_b
        try:
            while True:
                output_fn(self.session.run(net.output, feed_dict=input_fn()))
        except (KeyboardInterrupt, StopIteration):
            pass


class Network():

    def __init__(self, input_image, input_variables, example_output=None, weights=1, training=True, global_step=None, name="Network"):
        self.name = name
        with tf.variable_scope(name):
            # Convolutional layers here
            prev_layer = tf.layers.batch_normalization(input_image, training=training)
            for i in [24, 32, 48]:
                prev_layer = tf.layers.conv2d(prev_layer, i, [5,5], [2,2], 'valid', activation=tf.nn.relu)
            prev_layer = tf.layers.batch_normalization(prev_layer, training=training)
            prev_layer = tf.layers.conv2d(prev_layer, 64, [3,3], [1,1], 'valid', activation=tf.nn.relu)
            # Combine variables
            prev_layer = tf.contrib.layers.flatten(prev_layer)
            prev_layer = tf.concat([prev_layer, input_variables], 1)
            # Fully connected layers here
            for i in [2048, 512, 128, 16]:
                prev_layer = tf.layers.dense(
                    prev_layer, i, tf.nn.relu, 
                    use_bias=True, kernel_regularizer=tf.contrib.layers.l2_regularizer(0.00001))
                prev_layer = tf.layers.dropout(prev_layer, 0.3, training=training)
            self.output = tf.layers.dense(prev_layer, 2, activation=tf.nn.tanh, 
                use_bias=True, kernel_regularizer=tf.contrib.layers.l2_regularizer(0.00001))
            # Trainers and losses here
            self.vars = tf.get_collection(tf.GraphKeys.TRAINABLE_VARIABLES, scope=name)
            if example_output is not None:
                self.loss = tf.reduce_mean(tf.squared_difference(self.output, example_output)*weights) + tf.losses.get_regularization_loss(name)
                adam = tf.train.AdamOptimizer(1e-5, 0.85)
                self.trainer = adam.minimize(self.loss, global_step, self.vars)
                tf.summary.scalar('Loss', self.loss)
            # Summaries
            h, v = tf.split(self.output, [1,1], 1)
            tf.summary.histogram('Horizontal', h)
            tf.summary.histogram('Vertical', v)

