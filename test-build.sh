#!/bin/bash

cd /home/M/Documents/git_repos/Pelican-Keeper

# Build
echo "Building..."
./build.sh linux

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo "Build successful! Ready to test."
