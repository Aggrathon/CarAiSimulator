import tensorflow as tf
from timeit import default_timer as timer


def model_function(features, labels, mode, params):
    training = (mode==tf.estimator.ModeKeys.TRAIN)
    image, speed, direction = tf.split(features, [-1,1,1], 1)
    prev_layer = tf.reshape(image,[-1, 256, 128, 4])
    # Convolutional layers here
    for i in [32, 64, 96, 128]:
        prev_layer = tf.layers.conv2d(prev_layer, i, [5,5], [2,2], 'same', activation=tf.nn.relu)
    # Combine variables
    prev_layer = tf.reshape(prev_layer, [-1, 8*4*128])
    prev_layer = tf.concat([prev_layer, speed, direction], 1)
    # Fully connected layers here
    for i in [4096, 512, 128, 16]:
        prev_layer = tf.layers.dense(prev_layer, i, tf.nn.relu)
        prev_layer = tf.layers.dropout(prev_layer, 0.4, training=training)
    output = tf.layers.dense(prev_layer, 2, activation=tf.nn.tanh)
    # Trainers and losses here
    loss = tf.losses.mean_squared_error(labels, output)
    trainer = tf.contrib.layers.optimize_loss(
        loss=loss,
        global_step=tf.contrib.framework.get_global_step(), 
        optimizer='Adam',
        learning_rate = 0.001
    )
    eval_metric_ops = { "rmse": tf.metrics.root_mean_squared_error(labels, output) }
    return tf.estimator.EstimatorSpec(
        mode=mode,
        predictions={'output': output},
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
