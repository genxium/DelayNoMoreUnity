#!/bin/bash

if [ $# -ne 1 ]; then 
  echo "Usage: $0 [Debug|Release]"
  exit 1
fi

if [ "Debug" != $1 ] && [ "Release" != $1 ]; then 
  echo "Usage: $0 [Debug|Release]"
  exit 1
fi

basedir=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )

OS_USER=$USER
ServerEnv=$1
EXEPATH=bin/$1/net7.0/backend
LOG_PATH="/var/log/dllm.log"

# Make sure that the following "PidFile" is "git ignored".
PID_FILE="$basedir/dllm.pid"

sudo su - root -c "touch $LOG_PATH" 
sudo su - root -c "chown $OS_USER:$OS_USER $LOG_PATH" 

if [ "Debug" == $ServerEnv ]; then
        dotnet run >$LOG_PATH 2>&1 &
elif [ "Release" == $ServerEnv ]; then
        $basedir/$EXEPATH >$LOG_PATH 2>&1 &
else
        echo "Only Debug or Release run is supported!"
fi
echo $! > $PID_FILE
