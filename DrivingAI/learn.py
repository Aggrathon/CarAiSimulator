import sys
from timeit import default_timer as timer
import tensorflow as tf
from model import Session, get_network
from data import get_shuffle_batch


def learn(image, variables, example, iterations=50000, summary_interval=100):
    """
        Learn to drive from examples
    """
    try:
        global_step, network_a, network_b = get_network(image, variables, example)
        with Session(True, True, global_step) as sess:
            last_save = timer()
            step = 1
            time = 1
            while step < iterations:
                pre = timer()
                _, aloss, step = sess.session.run([network_a.trainer, network_a.loss, global_step])
                _, bloss = sess.session.run([network_b.trainer, network_b.loss])
                if step%summary_interval == 0:
                    sess.save_summary(step)
                    print()
                time = 0.9*time + 0.1 *(timer()-pre)
                if step%10 == 0:
                    print("Training step: %i, Loss A: %.3f, Loss B: %.3f (%.2f s)  "%(step, aloss, bloss, time), end='\r')
                if timer() - last_save > 1800:
                    sess.save_network()
                    last_save = timer()
            aloss_tot = 0
            bloss_tot = 0
            for i in range(10):
                aloss, bloss = sess.session.run([network_a.loss, network_b.loss])
                aloss_tot += aloss
                bloss_tot += bloss
            print("\nFinal loss A: %.3f, Final loss B: %.3f"%(aloss_tot/10, bloss_tot/10))
    except (KeyboardInterrupt, StopIteration):
        pass
    finally:
        print("\nStopping the training")

if __name__ == "__main__":
    tf.logging.set_verbosity(tf.logging.INFO)
    if len(sys.argv) > 1:
        learn(*get_shuffle_batch(16)[:-1], int(sys.argv[1]))
    else:
        learn(*get_shuffle_batch(16)[:-1])
