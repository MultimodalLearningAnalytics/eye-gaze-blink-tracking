import sys

def read_csv_file(filename, skip_header):
    with open(filename) as file:
        lines = file.readlines()
        if skip_header:
            return lines[1:]
        else:
            return lines

if len(sys.argv) < 3:
    raise ValueError("Please provide at least 2 file names")

lines = []
lines.extend(read_csv_file(sys.argv[1], False))

for i in range(2, len(sys.argv)-1):
    lines.extend(read_csv_file(sys.argv[i], True))

with open(sys.argv[-1], "w") as file:
    file.writelines(lines)
