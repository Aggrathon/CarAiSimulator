import tensorflow as tf
from timeit import default_timer as timer


def model_function(features, labels, mode, params):
    training = (mode==tf.estimator.ModeKeys.TRAIN)
    features = tf.cast(features, tf.float32)
    image, speed, direction = tf.split(features, [-1,1,1], 1)
    prev_layer = tf.reshape(image,[-1, 256, 128, 4])
    # Convolutional layers here
    prev_layer = tf.layers.batch_normalization(prev_layer, training=training)
    for i in [24, 32, 48]:
        prev_layer = tf.layers.conv2d(prev_layer, i, [5,5], [2,2], 'valid', activation=tf.nn.relu)
    prev_layer = tf.layers.batch_normalization(prev_layer, training=training)
    prev_layer = tf.layers.conv2d(prev_layer, 64, [3,3], [1,1], 'valid', activation=tf.nn.relu)
    prev_layer = tf.layers.conv2d(prev_layer, 64, [3,3], [1,1], 'valid', activation=tf.nn.relu)
    # Combine variables
    prev_layer = tf.reshape(prev_layer, [-1, 25*9*64])
    prev_layer = tf.concat([prev_layer, speed, direction], 1)
    # Fully connected layers here
    for i in [4096, 512, 128, 16]:
        prev_layer = tf.layers.dense(prev_layer, i, tf.nn.relu)
        prev_layer = tf.layers.dropout(prev_layer, 0.4, training=training)
    output = tf.layers.dense(prev_layer, 2, activation=tf.nn.tanh)
    # Trainers and losses here
    if mode == tf.estimator.ModeKeys.PREDICT:
        labels = tf.constant(0, tf.float32, output.get_shape())
    loss = tf.losses.mean_squared_error(labels, output)
    eval_metric_ops = { "rmse": tf.metrics.root_mean_squared_error(labels, output) }
    trainer = tf.contrib.layers.optimize_loss(
        loss=loss,
        global_step=tf.contrib.framework.get_global_step(), 
        optimizer='Adam',
        learning_rate = 1e-4
    )
    horiz, vert = tf.split(output, [1,1], 1)
    return tf.estimator.EstimatorSpec(
        mode=mode,
        predictions={'horizontal': horiz, 'vertical': vert},
        loss=loss,
        train_op=trainer,
        eval_metric_ops=eval_metric_ops
    )

def Estimator(**params):
    return tf.estimator.Estimator(
        model_fn=model_function,
        model_dir='data',
        params=params
    )
