# Local libraries
include_directories(${LandmarkDetector_SOURCE_DIR}/include)
	
add_executable(OpenFacePSIConnector OpenFacePSIConnector.cpp)
target_link_libraries(OpenFacePSIConnector LandmarkDetector)
target_link_libraries(OpenFacePSIConnector FaceAnalyser)
target_link_libraries(OpenFacePSIConnector GazeAnalyser)
target_link_libraries(OpenFacePSIConnector Utilities)

install (TARGETS OpenFacePSIConnector DESTINATION bin)