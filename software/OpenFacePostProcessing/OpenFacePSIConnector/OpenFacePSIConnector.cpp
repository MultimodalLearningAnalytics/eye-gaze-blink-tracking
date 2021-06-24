///////////////////////////////////////////////////////////////////////////////
// Copyright (C) 2017, Carnegie Mellon University and University of Cambridge,
// all rights reserved.
//
// ACADEMIC OR NON-PROFIT ORGANIZATION NONCOMMERCIAL RESEARCH USE ONLY
//
// BY USING OR DOWNLOADING THE SOFTWARE, YOU ARE AGREEING TO THE TERMS OF THIS LICENSE AGREEMENT.  
// IF YOU DO NOT AGREE WITH THESE TERMS, YOU MAY NOT USE OR DOWNLOAD THE SOFTWARE.
//
// License can be found in OpenFace-license.txt

//     * Any publications arising from the use of this software, including but
//       not limited to academic journal and conference publications, technical
//       reports and manuals, must cite at least one of the following works:
//
//       OpenFace 2.0: Facial Behavior Analysis Toolkit
//       Tadas Baltrušaitis, Amir Zadeh, Yao Chong Lim, and Louis-Philippe Morency
//       in IEEE International Conference on Automatic Face and Gesture Recognition, 2018  
//
//       Convolutional experts constrained local model for facial landmark detection.
//       A. Zadeh, T. Baltrušaitis, and Louis-Philippe Morency,
//       in Computer Vision and Pattern Recognition Workshops, 2017.    
//
//       Rendering of Eyes for Eye-Shape Registration and Gaze Estimation
//       Erroll Wood, Tadas Baltrušaitis, Xucong Zhang, Yusuke Sugano, Peter Robinson, and Andreas Bulling 
//       in IEEE International. Conference on Computer Vision (ICCV),  2015 
//
//       Cross-dataset learning and person-specific normalisation for automatic Action Unit detection
//       Tadas Baltrušaitis, Marwa Mahmoud, and Peter Robinson 
//       in Facial Expression Recognition and Analysis Challenge, 
//       IEEE International Conference on Automatic Face and Gesture Recognition, 2015 
//
///////////////////////////////////////////////////////////////////////////////


// OpenFacePSIConnector.cpp : Binary that allows PSI access to the OpenFace API's

// Local includes
#include "LandmarkCoreIncludes.h"
#include "ImageManipulationHelpers.h"

#include <Face_utils.h>
#include <FaceAnalyser.h>
#include <GazeEstimation.h>
#include <RecorderOpenFace.h>
#include <RecorderOpenFaceParameters.h>
#include <ImageCapture.h>
#include <Visualizer.h>
#include <VisualizationUtils.h>

#include <sys/socket.h>
#include <arpa/inet.h>
#include <unistd.h>

#ifndef CONFIG_DIR
#define CONFIG_DIR "~"
#endif

#define INFO_STREAM( stream ) \
std::cout << stream << std::endl

#define WARN_STREAM( stream ) \
std::cout << "Warning: " << stream << std::endl

#define ERROR_STREAM( stream ) \
std::cout << "Error: " << stream << std::endl

static void printErrorAndAbort(const std::string & error)
{
	std::cout << error << std::endl;
}

#define FATAL_STREAM( stream ) \
printErrorAndAbort( std::string( "Fatal error: " ) + stream )

std::vector<std::string> get_arguments(int argc, char **argv)
{

	std::vector<std::string> arguments;

	// First argument is reserved for the name of the executable
	for (int i = 0; i < argc; ++i)
	{
		arguments.push_back(std::string(argv[i]));
	}
	return arguments;
}

int main(int argc, char **argv)
{

	std::vector<std::string> arguments = get_arguments(argc, argv);

	// no arguments: output usage
	if (arguments.size() == 1)
	{
		std::cout << "For command line arguments see:" << std::endl;
		std::cout << " https://github.com/TadasBaltrusaitis/OpenFace/wiki/Command-line-arguments";
		return 0;
	}

	// Load the modules that are being used for tracking and face analysis
	// Load face landmark detector
	LandmarkDetector::FaceModelParameters det_parameters(arguments);
	// Always track gaze in feature extraction
	LandmarkDetector::CLNF face_model(det_parameters.model_location);

	if (!face_model.loaded_successfully)
	{
		std::cout << "ERROR: Could not load the landmark detector" << std::endl;
		return 1;
	}

	// Load facial feature extractor and AU analyser
	FaceAnalysis::FaceAnalyserParameters face_analysis_params(arguments);
	FaceAnalysis::FaceAnalyser face_analyser(face_analysis_params);

	if (!face_model.eye_model)
	{
		std::cout << "WARNING: no eye model found" << std::endl;
	}

	if (face_analyser.GetAUClassNames().size() == 0 && face_analyser.GetAUClassNames().size() == 0)
	{
		std::cout << "WARNING: no Action Unit models found" << std::endl;
	}

	// A utility for visualizing the results
	Utilities::Visualizer visualizer(arguments);

	// Tracking FPS for visualization
	Utilities::FpsTracker fps_tracker;
	fps_tracker.AddFrame();

	// During this while loop, we open the image file every time again, using a new image_reader
	Utilities::ImageCapture first_image_reader;

	// The sequence reader chooses what to open based on command line arguments provided
	std::vector<std::string> dup_arguments(arguments); // needed because ImageCapture modifies arguments vector
	if (!first_image_reader.Open(dup_arguments))
	{
		WARN_STREAM("Could not open image file!");
		return -1;
	}
	first_image_reader.GetNextImage(); // necessary to load .name parameter

	Utilities::RecorderOpenFaceParameters recording_params(arguments, true, false,
			first_image_reader.fx, first_image_reader.fy,
			first_image_reader.cx, first_image_reader.cy,
			30); // use fake FPS of 30

	Utilities::RecorderOpenFace open_face_rec(first_image_reader.name, recording_params, arguments);

	// Connect to C# PSIConnector Socket
	int obj_socket = 0, bytes_read;
	struct sockaddr_in serv_addr;
	const char *message_ready = "r";
	char buffer[1] = {0};
	if ((obj_socket = socket(AF_INET, SOCK_STREAM, 0)) < 0)
	{
		printf("Could not create socket!");
		return -1;
	}
	serv_addr.sin_family = AF_INET;
	serv_addr.sin_port = htons(12345);
	if (inet_pton(AF_INET, "127.0.0.1", &serv_addr.sin_addr) <= 0)
	{
		printf("\nInvalid IP address! This IP Address is not supported !\n");
		return -1;
	}
	if (connect(obj_socket, (struct sockaddr *)&serv_addr, sizeof(serv_addr)) < 0)
	{
		printf("Connection Failed: Can't establish a connection over this socket!");
		return -1;
	}

	// Start waiting for compute command from C#
	int frame_number = 0;
	while (true)
	{
		// Wait for compute command 'c' from PSI to start
		bytes_read = read(obj_socket, buffer, 1);
		if (bytes_read != 1)
		{
			WARN_STREAM("Did not receive exactly 1 byte over socket, exiting...");
			return -1;
		}
		INFO_STREAM("Received compute command, starting compute...");

		// During this while loop, we open the image file every time again, using a new image_reader
		Utilities::ImageCapture image_reader;

		// The sequence reader chooses what to open based on command line arguments provided
		std::vector<std::string> dup_arguments(arguments);  // needed because ImageCapture modifies arguments vector
		if (!image_reader.Open(dup_arguments))
		{
			WARN_STREAM("Could not open image file (in while loop)");
			break;
		}

		INFO_STREAM("Reading from image file");

		cv::Mat captured_image = image_reader.GetNextImage();
		frame_number++;

		// Converting to grayscale
		cv::Mat_<uchar> grayscale_image;
		Utilities::ConvertToGrayscale_8bit(captured_image, grayscale_image);


		// The actual facial landmark detection / tracking
		bool detection_success = LandmarkDetector::DetectLandmarksInVideo(captured_image, face_model, det_parameters, grayscale_image);
		
		// Gaze tracking, absolute gaze direction
		cv::Point3f gazeDirection0(0, 0, 0); cv::Point3f gazeDirection1(0, 0, 0); cv::Vec2d gazeAngle(0, 0);

		if (detection_success && face_model.eye_model)
		{
			GazeAnalysis::EstimateGaze(face_model, gazeDirection0, image_reader.fx, image_reader.fy, image_reader.cx, image_reader.cy, true);
			GazeAnalysis::EstimateGaze(face_model, gazeDirection1, image_reader.fx, image_reader.fy, image_reader.cx, image_reader.cy, false);
			gazeAngle = GazeAnalysis::GetGazeAngle(gazeDirection0, gazeDirection1);
		}
		
		// Do face alignment
		cv::Mat sim_warped_img;
		cv::Mat_<double> hog_descriptor; int num_hog_rows = 0, num_hog_cols = 0;

		// Perform AU detection and HOG feature extraction, as this can be expensive only compute it if needed by output or visualization
		//if (recording_params.outputAlignedFaces() || recording_params.outputHOG() || recording_params.outputAUs() || visualizer.vis_align || visualizer.vis_hog || visualizer.vis_aus)
		//{
			face_analyser.AddNextFrame(captured_image, face_model.detected_landmarks, face_model.detection_success, (float) 1/(float) 30 * (float) frame_number, false); // use fake FPS of 30
			face_analyser.GetLatestAlignedFace(sim_warped_img);
			face_analyser.GetLatestHOG(hog_descriptor, num_hog_rows, num_hog_cols);
		//}
		
		// Work out the pose of the head from the tracked model
		cv::Vec6d pose_estimate = LandmarkDetector::GetPose(face_model, image_reader.fx, image_reader.fy, image_reader.cx, image_reader.cy);

		// Keeping track of FPS
		fps_tracker.AddFrame();

		// Displaying the tracking visualizations
		visualizer.SetImage(captured_image, image_reader.fx, image_reader.fy, image_reader.cx, image_reader.cy);
		visualizer.SetObservationFaceAlign(sim_warped_img);
		visualizer.SetObservationHOG(hog_descriptor, num_hog_rows, num_hog_cols);
		visualizer.SetObservationLandmarks(face_model.detected_landmarks, face_model.detection_certainty, face_model.GetVisibilities());
		visualizer.SetObservationPose(pose_estimate, face_model.detection_certainty);
		visualizer.SetObservationGaze(gazeDirection0, gazeDirection1, LandmarkDetector::CalculateAllEyeLandmarks(face_model), LandmarkDetector::Calculate3DEyeLandmarks(face_model, image_reader.fx, image_reader.fy, image_reader.cx, image_reader.cy), face_model.detection_certainty);
		visualizer.SetObservationActionUnits(face_analyser.GetCurrentAUsReg(), face_analyser.GetCurrentAUsClass());
		visualizer.SetFps(fps_tracker.GetFPS());

		// detect key presses
		char character_press = visualizer.ShowObservation();
		
		// quit processing the current sequence (useful when in Webcam mode)
		if (character_press == 'q')
		{
			break;
		}

		// Setting up the recorder output
		open_face_rec.SetObservationHOG(detection_success, hog_descriptor, num_hog_rows, num_hog_cols, 31); // The number of channels in HOG is fixed at the moment, as using FHOG
		open_face_rec.SetObservationVisualization(visualizer.GetVisImage());
		open_face_rec.SetObservationActionUnits(face_analyser.GetCurrentAUsReg(), face_analyser.GetCurrentAUsClass());
		open_face_rec.SetObservationLandmarks(face_model.detected_landmarks, face_model.GetShape(image_reader.fx, image_reader.fy, image_reader.cx, image_reader.cy),
			face_model.params_global, face_model.params_local, face_model.detection_certainty, detection_success);
		open_face_rec.SetObservationPose(pose_estimate);
		open_face_rec.SetObservationGaze(gazeDirection0, gazeDirection1, gazeAngle, LandmarkDetector::CalculateAllEyeLandmarks(face_model), LandmarkDetector::Calculate3DEyeLandmarks(face_model, image_reader.fx, image_reader.fy, image_reader.cx, image_reader.cy));
		open_face_rec.SetObservationTimestamp((float) 1/ (float) 30 * (float) frame_number); // use fake FPS of 30
		open_face_rec.SetObservationFaceID(0);
		open_face_rec.SetObservationFrameNumber(frame_number);
		open_face_rec.SetObservationFaceAlign(sim_warped_img);
		open_face_rec.WriteObservation();
		open_face_rec.WriteObservationTracked();

		// Grabbing the next frame in the sequence
		captured_image = image_reader.GetNextImage();
		if (!captured_image.empty())
		{
			WARN_STREAM("Second frame was not empty! Aborting");
			break;
		}

		INFO_STREAM("Processing image done");

		// Send ready command 'r' to PSI to signal that processing is done
		send(obj_socket, message_ready, strlen(message_ready), 0);
	}

	INFO_STREAM("Closing output recorder");
	open_face_rec.Close();
	INFO_STREAM("Closed successfully");

	if (recording_params.outputAUs())
	{
		// TODO do post-processing after every frame??
		INFO_STREAM("Postprocessing the Action Unit predictions");
		face_analyser.PostprocessOutputFile(open_face_rec.GetCSVFile());
	}
	INFO_STREAM("Postprocessing Action Unit predictions done");

	return 0;
}
