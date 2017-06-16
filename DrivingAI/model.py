import tensorflow as tf
from timeit import default_timer as timer
import os
from data import get_lane_shuffle_batch, get_middle_lane


class Network():
    network_directory = os.path.join('data', 'network')
    log_directory = os.path.join('data', 'logs')
    model_file_name = os.path.join(network_directory, 'model')

    def __init__(self):
        self.global_step = tf.Variable(0, name='global_step')
        self.output = None
        self.loss = None
        self.trainer = None
        os.makedirs(self.log_directory, exist_ok=True)
        os.makedirs(self.network_directory, exist_ok=True)

    def _create_model(self, x, v, y, l, training=True):
        prev_layer = x
        # Convolutional layers here
        prev_layer = tf.layers.batch_normalization(prev_layer, training=training)
        for i in [24, 32, 48]:
            prev_layer = tf.layers.conv2d(prev_layer, i, [5,5], [2,2], 'valid', activation=tf.nn.relu)
        prev_layer = tf.layers.batch_normalization(prev_layer, training=training)
        prev_layer = tf.layers.conv2d(prev_layer, 64, [3,3], [1,1], 'valid', activation=tf.nn.relu)
        # Combine variables
        prev_layer = tf.contrib.layers.flatten(prev_layer)
        prev_layer = tf.concat([prev_layer, v], 1)
        # Fully connected layers here
        for i in [2048, 512, 128, 16]:
            prev_layer = tf.layers.dense(
                prev_layer, i, tf.nn.relu, 
                use_bias=True, kernel_regularizer=tf.contrib.layers.l2_regularizer(0.0001))
            prev_layer = tf.layers.dropout(prev_layer, 0.3, training=training)
        self.output = tf.layers.dense(prev_layer, 2, activation=tf.nn.tanh, 
            use_bias=True, kernel_regularizer=tf.contrib.layers.l2_regularizer(0.0001))
        # Trainers and losses here
        if y is not None:
            self.loss = tf.losses.mean_squared_error(self.output, y) + tf.losses.get_regularization_loss()
            adam = tf.train.AdamOptimizer(1e-5, 0.85)
            self.trainer = adam.minimize(self.loss, self.global_step)
            tf.summary.scalar('loss', self.loss)
            h, v = tf.split(self.output, [1,1], 1)
            tf.summary.histogram('horizontal', h)
            tf.summary.histogram('vertical', v)
        elif l is not None:
            self.loss = tf.losses.add_loss(l) + tf.losses.get_regularization_loss()
            adam = tf.train.AdamOptimizer(1e-5)
            self.trainer = adam.minimize(self.loss, self.global_step)
            tf.summary.scalar('reinforcement loss', self.loss)
            h, v = tf.split(self.output, [1,1], 1)
            tf.summary.histogram('horizontal', h)
            tf.summary.histogram('vertical', v)


    def get_session(self):
        saver = tf.train.Saver()
        session = tf.Session()
        session.run(tf.global_variables_initializer())
        tf.train.start_queue_runners(session)
        try:
            ckpt = tf.train.get_checkpoint_state(self.network_directory)
            if ckpt is None:
                print("\nCreated a new network\n")
            else:
                saver.restore(session, ckpt.model_checkpoint_path)
                print("\nLoaded an existing network\n")
        except Exception as e:
            print("\nCreated a new network (%s)\n"%repr(e))
        return session, saver

    def train_step(self, session, feed_dict=None, summary=False, summary_writer=None, summary_ops=None):
        pre = timer()
        if summary and summary_writer is not None and summary_ops is not None:
            _, loss, step, summary = session.run([self.trainer, self.loss, self.global_step, summary_ops], feed_dict=feed_dict)
            summary_writer.add_summary(summary, step)
            if step%10 == 0:
                print("Training step: %i, loss: %.3f [%.2f s]"%(step, loss, (timer()-pre)*10))
        else:
            _, loss, step = session.run([self.trainer, self.loss, self.global_step], feed_dict=feed_dict)
            if step%10 == 0:
                print("Training step: %i, loss: %.3f (%.2f s)"%(step, loss, (timer()-pre)*10))
        return step, loss
    
    def learn(self, batch_size=64, iterations=10000, summary_interval=100):
        x, v, y = get_lane_shuffle_batch(batch_size)
        self._create_model(x, v, y, None, True)
        summary_ops = tf.summary.merge_all()
        session, saver = self.get_session()
        summary_writer = tf.summary.FileWriter(self.log_directory, session.graph)
        try:
            last_save = timer()
            step = 1
            for _ in range(iterations):
                step, _ = self.train_step(session, None, step%summary_interval==0, summary_writer, summary_ops)
                if timer() - last_save > 1800:
                    saver.save(session, self.model_file_name, self.global_step)
                    last_save = timer()
        except KeyboardInterrupt:
            print("Stopping the training")
        finally:
            saver.save(session, self.model_file_name, self.global_step)
            summary_writer.close()
            session.close()

    def predict(self, input_fn, output_fn, image_width=200, image_height=60, image_depth=4, additional_variables=3):
        x = tf.placeholder(tf.float32, [None, image_width*image_height*image_depth])
        x_ = tf.reshape(x, [-1, image_width, image_height, image_depth])
        v = tf.placeholder(tf.float32, [None, additional_variables])
        self._create_model(*get_middle_lane(x_, v, None), None, False)
        session, _ = self.get_session()
        try:
            while True:
                xi, vi = input_fn()
                out = session.run(self.output, feed_dict={x: xi, v: vi})
                output_fn(out)
        except (KeyboardInterrupt, StopIteration):
            pass
        finally:
            session.close()
    
    def predict_tensor(self, x, output_fn):
        self._create_model(x, None, None, False)
        session, _ = self.get_session()
        try:
            while True:
                out = session.run(self.output)
                output_fn(out[0])
        except (KeyboardInterrupt, StopIteration):
            pass
        finally:
            session.close()

