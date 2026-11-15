# Copyright(c) 2026 Sven Trittler
#
# This program and the accompanying materials are made available under the
# terms of the Eclipse Public License v. 2.0 which is available at
# http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
# v. 1.0 which is available at
# http://www.eclipse.org/org/documents/edl-v10.php.
#
# SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
# The bridge uses the matching typelib implementation to consume idlc's XTypes blobs.
set(CYCLONEDDS_SOURCE_DIR "${PROJECT_SOURCE_DIR}/build/_deps/cyclonedds-src" CACHE PATH "Source tree of the matching Cyclone DDS runtime")
if(NOT EXISTS "${CYCLONEDDS_SOURCE_DIR}/src/core/ddsc/src/dds__entity.h")
  message(FATAL_ERROR "Set CYCLONEDDS_SOURCE_DIR to the matching Cyclone DDS source tree (scripts/bootstrap-native.sh provides it).")
endif()
add_library(cycloneddscsharp SHARED bridge.c listener.c)
target_include_directories(cycloneddscsharp PRIVATE "${CYCLONEDDS_SOURCE_DIR}/src/core/ddsc/src")
target_link_libraries(cycloneddscsharp PRIVATE CycloneDDS::ddsc ${CMAKE_DL_LIBS})
target_compile_definitions(cycloneddscsharp PRIVATE CS_BUILD)
if(WIN32)
  set(ddsc_filename "ddsc.dll")
else()
  set(ddsc_filename "$<TARGET_SONAME_FILE_NAME:CycloneDDS::ddsc>")
endif()
add_custom_command(TARGET cycloneddscsharp POST_BUILD
  COMMAND ${CMAKE_COMMAND} -E copy_if_different $<TARGET_FILE:CycloneDDS::ddsc>
    "$<TARGET_FILE_DIR:cycloneddscsharp>/${ddsc_filename}")
set_target_properties(cycloneddscsharp PROPERTIES VERSION ${PROJECT_VERSION} SOVERSION ${PROJECT_VERSION_MAJOR})
if(MSVC)
  target_compile_options(cycloneddscsharp PRIVATE /W4)
else()
  target_compile_options(cycloneddscsharp PRIVATE -Wall -Wextra -Werror)
endif()
install(TARGETS cycloneddscsharp RUNTIME DESTINATION bin LIBRARY DESTINATION lib ARCHIVE DESTINATION lib)
