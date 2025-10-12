cmake_minimum_required(VERSION 3.23)
project(osspeech LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Unity の Plugins/x86_64 へビルド成果をコピー
# パスは your-unity-project からの相対で調整してください
set(UNITY_PLUGIN_DIR "${CMAKE_SOURCE_DIR}/../../../Assets/Plugins/x86_64")

add_library(TtsPlugin SHARED
    src/TtsPlugin.cpp
    src/TtsPlugin.h
)

if (MSVC)
  target_compile_options(TtsPlugin PRIVATE /W4 /permissive- /EHsc /Zc:__cplusplus)
  # ランタイムは/MD（配布時OS同梱ランタイム使用）。静的化したい場合は/MTへ。
  target_compile_options(TtsPlugin PRIVATE /MD)
  target_compile_options(TtsPlugin PRIVATE /utf-8)
endif()

# Windows / SAPI / COM
target_compile_definitions(TtsPlugin PRIVATE UNICODE _UNICODE)
target_link_libraries(TtsPlugin PRIVATE
    ole32
    oleaut32
    uuid
)

# ビルド後コピー
add_custom_command(TARGET TtsPlugin POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${UNITY_PLUGIN_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:TtsPlugin> "${UNITY_PLUGIN_DIR}/"
    COMMENT "Copying TtsPlugin.dll to Assets/Plugins/x86_64"
)
