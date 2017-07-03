import tensorflow as tf
from timeit import default_timer as timer
import os
from data import score_buffer
import numpy as np

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
        self._save = training is not None and training is not False
        self.saver = tf.train.Saver()
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

    def __enter__(self):
        return self

    def __exit__(self, type, value, traceback):
        if self._save:
            self.saver.save(self.session, self.model_file_name, self.global_step)
        self.coord.request_stop()
        self.coord.join()
        self.session.close()
    
    def learn(self, iterations=50000, summary_interval=100):
        summary_ops = tf.summary.merge_all()
        summary_writer = tf.summary.FileWriter(self.log_directory, self.session.graph)
        try:
            last_save = timer()
            step = 1
            time = 1
            for _ in range(iterations):
                pre = timer()
                _, aloss, step = self.session.run([self.network_a.trainer, self.network_a.loss, self.global_step])
                _, bloss = self.session.run([self.network_b.trainer, self.network_b.loss])
                if step%summary_interval == 0:
                    summary_writer.add_summary(self.session.run(summary_ops), step)
                time = 0.9*time + 0.1 *(timer()-pre)
                if step%10 == 0:
                    print("Training step: %i, Loss A: %.3f, Loss B: %.3f (%.2f s)"%(step, aloss, bloss, time), end='\r')
                if timer() - last_save > 1800:
                    self.saver.save(self.session, self.model_file_name, self.global_step)
                    last_save = timer()
        except KeyboardInterrupt:
            print("\nStopping the training")
        finally:
            summary_writer.close()

    def drive(self, input_fn, output_fn, use_network_a=True):
        net = self.network_a if use_network_a else self.network_b
        try:
            while True:
                output_fn(self.session.run(net.output, feed_dict=input_fn()))
        except (KeyboardInterrupt, StopIteration):
            pass

    def train(self, input_fn, output_fn, feed_fn, batch_size=16, iterations=50000, summary_interval=50):
        summary_ops = tf.summary.merge_all()
        summary_writer = tf.summary.FileWriter(self.log_directory, self.session.graph)
        buffer = score_buffer()
        array_a = []
        array_b = []
        def fill_buffer(array, output):
            print("Filling the reinforcement buffer...     ", end='\r')
            while buffer.get_num_scored() < 10 * batch_size:
                fd, x, v, s = input_fn()
                y = self.session.run(output, feed_dict=fd)
                if np.random.uniform() < 0.2:
                    y[0][0] = np.clip(y[0][0] + np.random.normal(0, 0.15) + np.random.normal(0, 0.15), -1, 1)
                    y[0][1] = np.clip(y[0][1] + np.random.normal(0, 0.15) + np.random.normal(0, 0.15), -1, 1)
                output_fn(y)
                buffer.add_item([x, v, y[0], s])
            for i in buffer.clear_buffer():
                array.append(i)
            np.random.shuffle(array)

        try:
            last_save = timer()
            step = 1
            time = 1
            for _ in range(iterations):
                if len(array_a) < batch_size:
                    fill_buffer(array_a, self.network_b.output)
                if len(array_b) < batch_size:
                    fill_buffer(array_b, self.network_a.output)
                
                pre = timer()
                state = array_a.pop()
                grad_a = self.session.run(self.network_a.gradient, feed_dict=feed_fn(*([i] for i in state)))*(state[-1][0]-0.3)
                state = array_b.pop()
                grad_b = self.session.run(self.network_b.gradient, feed_dict=feed_fn(*([i] for i in state)))*(state[-1][0]-0.3)
                for _ in range(batch_size-1):
                    state = array_a.pop()
                    g_a = self.session.run(self.network_a.gradient, feed_dict=feed_fn(*([i] for i in state)))
                    for i, v in enumerate(grad_a):
                        if g_a[i] is not None:
                            grad_a[i] = grad_a[i] + g_a[i]*(state[-1][0]-0.3)
                    state = array_b.pop()
                    g_b = self.session.run(self.network_b.gradient, feed_dict=feed_fn(*([i] for i in state)))
                    for i, v in enumerate(grad_b):
                        if g_a[i] is not None:
                            grad_b[i] = grad_b[i] + g_b[i]*(state[-1][0]-0.3)
                for i, v in enumerate(grad_a):
                    if grad_a[i] is not None:
                        grad_a[i] = (grad_a[i][0], grad_a[i][1]*2/batch_size)
                    if grad_b[i] is not None:
                        grad_b[i] = (grad_b[i][0], grad_b[i][1]*2/batch_size)
                self.session.run([self.network_a.update, self.network_b.update], dict(
                    zip(self.network_a.gradient, grad_a)+
                    zip(self.network_b.gradient, grad_b)
                ))

                if step%summary_interval == 0:
                    def get_batch(arr):
                        x = [u[0] for u in arr[-batch_size:]]
                        v = [u[1] for u in arr[-batch_size:]]
                        y = [u[2] for u in arr[-batch_size:]]
                        s = [u[4] for u in arr[-batch_size:]]
                        for _ in range(batch_size):
                            arr.pop()
                        return x, v, y, s
                    b_a = get_batch(array_a)
                    b_b = get_batch(array_b)
                    summary_writer.add_summary(self.session.run(summary_ops,
                        feed_dict=feed_fn(b_a[0]+b_b[0], b_a[1]+b_b[1], b_a[2]+b_b[2], b_a[3]+b_b[3])), step)
                time = 0.9*time + 0.1 *(timer()-pre)
                if step%10 == 0:
                    print("Training step: %i (%.2f s)"%(step, time), end='\r')
                if timer() - last_save > 1800:
                    self.saver.save(self.session, self.model_file_name, self.global_step)
                    last_save = timer()
        except (KeyboardInterrupt, StopIteration):
            print("\nStopping the training")
        finally:
            summary_writer.close()



class Network():

    def __init__(self, input_image, input_variables, example_output=None, score=None, training=True, global_step=None, name="Network"):
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
            if example_output is not None:
                self.vars = tf.get_collection(tf.GraphKeys.TRAINABLE_VARIABLES, scope=name)
                self.loss = tf.reduce_mean(tf.squared_difference(self.output, example_output)) + tf.losses.get_regularization_loss(name)
                adam = tf.train.AdamOptimizer(1e-5, 0.85)
                self.trainer = adam.minimize(self.loss, global_step, self.vars)
                grad_list = adam.compute_gradients(self.loss, self.vars)
                self.gradient = [i[0] for i in grad_list]
                self.update = adam.apply_gradients(grad_list, global_step)
                tf.summary.scalar('Loss', self.loss)
            if score is not None:
                tf.summary.histogram('Score', score)
            # Summaries
            h, v = tf.split(self.output, [1,1], 1)
            tf.summary.histogram('Horizontal', h)
            tf.summary.histogram('Vertical', v)

