#!/bin/sh 

set -e

if [ ! -e "v2.0.3.tar.gz" ]; then
	wget https://github.com/mstorsjo/fdk-aac/archive/refs/tags/v2.0.3.tar.gz
fi

if [ ! -d "fdk-aac-2.0.3" ]; then
	tar -xf v2.0.3.tar.gz
fi

if [ -d "fdk-aac-2.0.3/build" ]; then
	rm -rf fdk-aac-2.0.3/build
fi

cd fdk-aac-2.0.3

mkdir build && cd build

CFLAGS="-march=armv8-a+crc+crypto -mtune=cortex-a57 -mtp=soft -fPIE" \
  CXXFLAGS="-march=armv8-a+crc+crypto -mtune=cortex-a57 -mtp=soft -fPIE -fno-rtti" \
  cmake .. -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=OFF -D CMAKE_C_COMPILER=/opt/devkitpro/devkitA64/bin/aarch64-none-elf-gcc -D CMAKE_CXX_COMPILER=/opt/devkitpro/devkitA64/bin/aarch64-none-elf-g++

make fdk-aac -j 4

cmake -DCMAKE_INSTALL_PREFIX=$(pwd) -P cmake_install.cmake

cd ..
cd ..