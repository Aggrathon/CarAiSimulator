import tensorflow as tf

class Network():

    def __init__(self, training=True):
        self.input_image = tf.placeholder(tf.uint8, [None, 128*256, 4])
        prev_layer = tf.reshape(self.input_image,[-1, 256, 128, 4])
        #3x Convolutional layers here
        tf.layers.conv2d(prev_layer, 32, [5,5], [2,2], 'same', activation=tf.nn.relu)
        tf.layers.conv2d(prev_layer, 64, [5,5], [2,2], 'same', activation=tf.nn.relu)
        tf.layers.conv2d(prev_layer, 96, [5,5], [2,2], 'same', activation=tf.nn.relu)
        #Combine variables
        prev_layer = tf.reshape(prev_layer, [-1, 256*128/(2**3)*96])
        self.input_speed = tf.placeholder(tf.float32, [None, 1])
        self.input_direction = tf.placeholder(tf.float32, [None, 1])
        prev_layer = tf.concat([prev_layer, self.input_speed, self.input_direction], 1)
        #Nx fully connected layers here
        for i in [1024, 128, 32, 8]:
            prev_layer = tf.layers.dense(prev_layer, i, tf.nn.relu)
            prev_layer = tf.layers.dropout(prev_layer, 0.4, training=training)
        logits = tf.layers.dense(prev_layer, 2)
        #Trainers and losses here
        self.output_horizontal = tf.placeholder(tf.float32, [None,1])
        self.output_vertical = tf.placeholder(tf.float32, [None,1])
        labels = tf.concat([self.output_horizontal, self.output_vertical], 1)
        self.loss = tf.losses.sigmoid_cross_entropy(labels, logits)
        self.trainer = tf.contrib.layers.optimize_loss(loss=self.loss, optimizer='ADAM')

    def train(self):
        pass

    def drive(self, session, camera, speed, direction):
        return 0.0, 1.0

