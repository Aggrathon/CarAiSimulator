import tensorflow as tf
from timeit import default_timer as timer
import os


class Network():
    directory = 'data'
    file_name = os.path.join(directory, 'model')

    def __init__(self, image_width=256, image_height=128, additional_variables=2):
        self.image_width = image_width
        self.image_height = image_height
        self.additional_variables = additional_variables
        self.global_step = tf.Variable(0, name='global_step')
        self.input = None
        self.example = None
        self.output = None
        self.loss_example = None
        self.train_example = None


    def _create_model_placeholder(self, training=True):
        self._create_model(
            tf.placeholder(tf.float32, [None, self.image_width*self.image_height*4+self.additional_variables]),
            tf.placeholder(tf.float32, [None, 2]),
            None,
            training
        )

    def _create_model(self, x, y, l, training=True):
        self.input = x
        image, variables = tf.split(self.input, [-1, self.additional_variables], 1)
        prev_layer = tf.reshape(image ,[-1, 256, 128, 4])
        # Convolutional layers here
        prev_layer = tf.layers.batch_normalization(prev_layer, training=training)
        for i in [24, 32, 48]:
            prev_layer = tf.layers.conv2d(prev_layer, i, [5,5], [2,2], 'valid', activation=tf.nn.relu)
        prev_layer = tf.layers.batch_normalization(prev_layer, training=training)
        prev_layer = tf.layers.conv2d(prev_layer, 64, [3,3], [1,1], 'valid', activation=tf.nn.relu)
        prev_layer = tf.layers.conv2d(prev_layer, 64, [3,3], [1,1], 'valid', activation=tf.nn.relu)
        # Combine variables
        prev_layer = tf.contrib.layers.flatten(prev_layer)
        prev_layer = tf.concat([prev_layer, variables], 1)
        # Fully connected layers here
        for i in [4096, 512, 128, 16]:
            prev_layer = tf.layers.dense(prev_layer, i, tf.nn.relu)
            prev_layer = tf.layers.dropout(prev_layer, 0.4, training=training)
        self.output = tf.layers.dense(prev_layer, 2, activation=tf.nn.tanh)
        # Trainers and losses here
        self.example = y
        self.loss_example = tf.losses.mean_squared_error(self.example, self.output)
        adam = tf.train.AdamOptimizer(1e-4)
        self.train_example = adam.minimize(self.loss_example, self.global_step)


    def get_session(self):
        saver = tf.train.Saver()
        session = tf.Session()
        session.run(tf.global_variables_initializer())
        tf.train.start_queue_runners(session)
        try:
            ckpt = tf.train.get_checkpoint_state(self.directory)
            saver.restore(session, ckpt.model_checkpoint_path)
            print("\nLoaded an existing network\n")
        except Exception as e:
            print("\nCreated a new network (%s)\n"%repr(e))
        return session, saver

    def train_step(self, session, feed_dict=None, summary_interval=0):
        pre = timer()
        _, loss, step = session.run([self.train_example, self.loss_example, self.global_step], feed_dict=feed_dict)
        print("Training step: %i, loss: %.3f (%.2f s)"%(step, loss, timer()-pre))
        if summary_interval > 0 and step%summary_interval == 0:
            pass #TODO summaries
        return step, loss
    
    def train(self, batch_fn, iterations=1000, summary_interval=10):
        x, y = batch_fn()
        self._create_model(x, y, None, True)
        session, saver = self.get_session()
        try:
            last_save = timer()
            for _ in range(iterations):
                self.train_step(session, None, summary_interval)
                if timer() - last_save > 1800:
                    saver.save(session, self.file_name, self.global_step)
                    last_save = timer()
        except KeyboardInterrupt:
            print("Stopping the training")
        finally:
            saver.save(session, self.file_name, self.global_step)
            session.close()

    def predict(self, input_fn, output_fn):
        self._create_model_placeholder(False)
        session, _ = self.get_session()
        try:
            while True:
                out = session.run([self.output], feed_dict={self.input:input_fn()})
                output_fn(out[0][0])
        except (KeyboardInterrupt, StopIteration):
            pass
        finally:
            session.close()
