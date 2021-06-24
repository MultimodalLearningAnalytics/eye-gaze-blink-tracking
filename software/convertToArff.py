import sys
import numpy as np
import pandas as pd

csv = pd.read_csv(sys.argv[1])

with open("arffoutput.arff", "w") as arffFile:
    arffFile.write("% Automatically converted file\n")

    arffFile.write(f"@RELATION FullStoreFeatures\n\n")

    columns = csv.columns
    for c in columns:
        classValues = csv["class"].unique()
        cType = "numeric"
        if c == "class":
            cType = "{attentive,inattentive}"
        if c == "timestamp":
            cType = "string"
        arffFile.write(f"@ATTRIBUTE {c} {cType}\n")

    arffFile.write('\n\n@DATA\n')

    for index, row in csv.iterrows():
        rowString = ""
        for c in columns:
            rowString = rowString + str(row[c]) + ','
        rowString = rowString[:-1] + '\n'
        arffFile.write(rowString)
