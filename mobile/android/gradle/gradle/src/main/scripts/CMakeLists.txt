# Empty file to trick the Android Gradle Plugin to download the NDK. This is because AGP requires
# the NDK in order to strip debug symbols from native libraries, does not download it in that case
# (instead, AGP only downloads it when AGP thinks it needs the NDK to build a native library).

cmake_minimum_required(VERSION 3.10.2)
project(flutter_dummy NONE)

