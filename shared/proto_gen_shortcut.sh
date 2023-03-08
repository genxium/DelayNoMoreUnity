#!/bin/bash

shared_basedir=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )/
protoc -I=$shared_basedir/ --csharp_out=. room_downsync_frame.proto 
