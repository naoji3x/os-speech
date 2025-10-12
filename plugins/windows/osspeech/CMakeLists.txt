cmake_minimum_required(VERSION 3.23)
project(osspeech LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ▼ 追加：Plugins 配下のCPUフォルダ名を外から指定できるように
#   例) -DUNITY_CPU_DIR=x86_64  または  -DUNITY_CPU_DIR=arm64
if(NOT DEFINED UNITY_CPU_DIR)
  set(UNITY_CPU_DIR "x86_64")  # 既定は x86_64
endif()

# Unity の Plugins/<CPU> へビルド成果をコピー
set(UNITY_PLUGIN_DIR "${CMAKE_SOURCE_DIR}/../../../Assets/Plugins/${UNITY_CPU_DIR}")

add_library(TtsPlugin SHARED
    src/TtsPlugin.cpp
    src/TtsPlugin.h
)

if (MSVC)
  target_compile_options(TtsPlugin PRIVATE /W4 /permissive- /EHsc /Zc:__cplusplus /utf-8)
  # /MD（VC++再頒布に依存）。静的リンクにしたい場合は /MT に変更可。
  target_compile_options(TtsPlugin PRIVATE /MD)
endif()

# Windows / SAPI / COM
target_compile_definitions(TtsPlugin PRIVATE UNICODE _UNICODE)
target_link_libraries(TtsPlugin PRIVATE ole32 oleaut32 uuid)

# ビルド後コピー
add_custom_command(TARGET TtsPlugin POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${UNITY_PLUGIN_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:TtsPlugin>" "${UNITY_PLUGIN_DIR}/"
    COMMENT "Copying TtsPlugin.dll to Assets/Plugins/${UNITY_CPU_DIR}"
)
