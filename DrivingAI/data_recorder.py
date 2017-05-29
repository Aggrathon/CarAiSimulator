import tensorflow as tf
from communication import Communicator, SIMULATOR_RECORD
from threading import Thread
import os
import datetime


def record_data():
    counter = 0
    records = []
    with Communicator(SIMULATOR_RECORD) as comm:
        print("Waiting for Data...               ", end='\r')
        data = comm.recieve()
        while data is not None and len(data) > 0:
            records.append(data)
            counter += 1
            print("Snapshots recieved:",counter, end='\r')
            data = comm.recieve()
    print()
    return records

def save_data(records, start, step, ):
    counter = start
    os.makedirs("data")
    timestamp = '{:%Y%m%d%H%M%S}'.format(datetime.datetime.now())
    with tf.python_io.TFRecordWriter("data/training_%s_%i.tfrecords"%(timestamp, start)) as writer:
        while counter < len(records):
            data = records[counter]
            record = tf.train.Example(features=tf.train.Features(feature={
                'camera': tf.train.Feature(int64_list=tf.train.Int64List(value=data[:-4])),
                'direction': tf.train.Feature(float_list=tf.train.FloatList(value=[float(data[-4])/127.5*180-180])),
                'speed': tf.train.Feature(float_list=tf.train.FloatList(value=[(float(data[-3])-100)*0.5])),
                'horizontal': tf.train.Feature(float_list=tf.train.FloatList(value=[float(data[-2])/127.5-1])),
                'vertical': tf.train.Feature(float_list=tf.train.FloatList(value=[float(data[-1])/127.5-1]))
            }))
            writer.write(record.SerializeToString())
            counter += step
            if start == 0:
                print("Snapshots saved:", counter, end='\r')
    if start == 0:
        print()

if __name__ == "__main__":
    data = record_data()
    for i in range(4):
        Thread(target=save_data, args=(data, i, 4)).start()