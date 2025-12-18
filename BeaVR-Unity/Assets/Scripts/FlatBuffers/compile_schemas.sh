#!/bin/bash
# Get the directory where this script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"

# Compile the schema, outputting C# files to the script's directory
# The namespace defined in the fbs (BEAVRApp) will create a subdirectory BEAVRApp
flatc --csharp -o "$SCRIPT_DIR" "$SCRIPT_DIR/teleop.fbs"

echo "Compiled teleop.fbs to $SCRIPT_DIR"
