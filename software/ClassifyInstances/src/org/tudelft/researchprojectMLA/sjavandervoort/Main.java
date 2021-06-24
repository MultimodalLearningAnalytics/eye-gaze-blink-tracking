package org.tudelft.researchprojectMLA.sjavandervoort;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.FileReader;
import java.io.FileWriter;

import weka.classifiers.misc.InputMappedClassifier;
import weka.core.Instances;

public class Main {
    private static final String PATH_INPUT_FILE = "/some/path/todo";
    private static final String PATH_OUTPUT_FILE = "/some/path/todo/20s-FullStoreFeatures-labelled.arff";
    private static final String PATH_MODEL_FILE = "/some/path/todo/Model-20sWindow-RandomForest-Attentive-DistractedInattentive.model";

    public static void main(String[] args) throws Exception {
        InputMappedClassifier classifier = new InputMappedClassifier();
        classifier.setModelPath(PATH_MODEL_FILE);

        // Load unlabelled instances
        Instances unlabeled = new Instances(new BufferedReader(new FileReader(PATH_INPUT_FILE)));
        // Identify class attribute
        unlabeled.setClassIndex(unlabeled.numAttributes() - 1);

        // Create copy of unlabelled instances and set relation name
        Instances labeled = new Instances(unlabeled);
        labeled.setRelationName(unlabeled.relationName() + "-labelled");

        // Label instances
        for (int i = 0; i < unlabeled.numInstances(); i++) {
            double clsLabel = classifier.classifyInstance(unlabeled.instance(i));
            System.out.println(clsLabel);
            labeled.instance(i).setClassValue(clsLabel);
        }

        // Save labeled instances to new .arff file
        BufferedWriter writer = new BufferedWriter(new FileWriter(PATH_OUTPUT_FILE));
        writer.write(labeled.toString());
        writer.newLine();
        writer.flush();
        writer.close();
    }
}
