# Selfdriving Car AI and Simulator
This project contains a neural network for driving a car in a simulator. The simulator is also part of the project. The goal with not using a existing game/simulator is to allow more control over the data being fed to the AI, which made some experimentation possible.

## Simulator
The simulator is made with unity. It generates random terrains in order to create varied learning situations. The simulator communicates with the AI through a local socket, this means that often both the simulator and the AI have to be started.

## AI
The AI receives the following input:  
1. A color image.  
2. A grayscale image created from the depth and normal buffers (a LADAR scanner would be the real life equivalent).  
3. The current speed of the car.  

The output is acceleration and turning values.

## Download
A windows version of the simulator can be downloaded [here](https://github.com/Aggrathon/CarAiSimulator/releases).
The trained network is unfortunately too big to distribute here (maybe the fully connected layers coud be smaller).

## Usage
Here is the normal flow for using the AI:
1. Use the simulator and the `record.py` script to to create examples of how humans drive.  
2. Train the AI on the recorded examples using the `learn.py` script.  
3. Improve the AI with reinforcement learning, using the simulator and the `train.py` script.  
4. Let the AI drive in the simulator with the `drive.py` script.

## Dependencies
- Python 3
- Tensorflow
- Unity (2017.2)
