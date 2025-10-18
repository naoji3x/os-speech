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

add_library(TextToSpeechBridge SHARED
    src/TextToSpeechBridge.cpp
    src/TextToSpeechBridge.h
)

if (MSVC)
  target_compile_options(TextToSpeechBridge PRIVATE /W4 /permissive- /EHsc /Zc:__cplusplus /utf-8)
  # /MD（VC++再頒布に依存）。静的リンクにしたい場合は /MT に変更可。
  target_compile_options(TextToSpeechBridge PRIVATE /MD)
endif()

# Windows / SAPI / COM
target_compile_definitions(TextToSpeechBridge PRIVATE UNICODE _UNICODE)
target_link_libraries(TextToSpeechBridge PRIVATE ole32 oleaut32 uuid)

# ビルド後コピー
add_custom_command(TARGET TextToSpeechBridge POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${UNITY_PLUGIN_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy "$<TARGET_FILE:TextToSpeechBridge>" "${UNITY_PLUGIN_DIR}/"
    COMMENT "Copying TextToSpeechBridge.dll to Assets/Plugins/${UNITY_CPU_DIR}"
)
