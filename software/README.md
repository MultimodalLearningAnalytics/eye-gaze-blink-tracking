# Software

This folder contains all the software used for creating and generating the data and models used during the research.

There are several different programs, all listed here with their description in an order that one would normally use them.
Most program can be configured by modifying string or integer constants above the program entry point.

- **OpenFacePostProcessing** is a combination of a C# Microsoft PSI script and a modified C++ OpenFace script to post-process the raw webcam video into eye gaze and blink values. The C# part can be directly ran and waits for the C++ OpenFacePSIConnector binary to start in another process. This binary needs to be compiled with the [OpenFace library](https://github.com/TadasBaltrusaitis/OpenFace).
- **ExtractFeatureWindows** is a script written in C# with the Microsoft PSI framework. It contains code to extract eye gaze and blink features from time windows in a GazeBlinkData store coming from a recording. Output is a .csv file with the features and the labels. The different modes are:
  - **experiment2**: Extracts features from consecutive windows of 10s, 20s and 30s from all recording of a particular participant and labels them "Attentive".
  - **experiment3**: Extracts features from windows of 10s, 20s and 30s preceding a distraction button press or a long deblur time and labels them "Inattentive" or "Distracted".
  - **fullstore**: Extracts features for all frames: it takes 20s windows preceding each frame and calculates the features from that window. Can be used to create a prediction for each frame of a recording, visualizing the output of a classifier model.
  - **exp3-attentive-inattentive**: Extract features only from a long reading experiment. It extracts "Attentive"-labeled features from each consecutive 20s window unless the window is close to a distraction button press or a long deblur time. Features labeled as "Inattentive" are extracted similar to the **experiment3** option.
- **converToArff.py** is a Python utility script that simply converts the output of **ExtractFeatureWindows** with option **fullstore** from .csv file format to .arff file format.
- **ClassifyInstances** is a Java program that takes an .arff file containing timestamps and features and labels them using a specified Weka trained classifier model. The output is the same .arff file, now containing the predicted class as label.
- **CreatePredictionsStore** is a C# script which creates a new Microsoft PSI store containing a predictions stream coming from the input .arff file. The input .arff file should be an output of the **ClassifyInstances** program. Caution: 1 means "Inattentive", 0 means "Attentive".
- **appendCSV.py** is a Python utility script that simply appends the given .csv files and writes the results back to a given (new) file.

