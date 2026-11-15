# Copyright(c) 2026 Sven Trittler
#
# This program and the accompanying materials are made available under the
# terms of the Eclipse Public License v. 2.0 which is available at
# http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
# v. 1.0 which is available at
# http://www.eclipse.org/org/documents/edl-v10.php.
#
# SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
add_library(cycloneddsidlcsharp SHARED generator.c)
target_link_libraries(cycloneddsidlcsharp PRIVATE CycloneDDS::idl)
set_target_properties(cycloneddsidlcsharp PROPERTIES VERSION ${PROJECT_VERSION} SOVERSION ${PROJECT_VERSION_MAJOR})
if(MSVC)
  target_compile_options(cycloneddsidlcsharp PRIVATE /W4)
else()
  target_compile_options(cycloneddsidlcsharp PRIVATE -Wall -Wextra -Werror)
endif()
install(TARGETS cycloneddsidlcsharp RUNTIME DESTINATION bin LIBRARY DESTINATION lib ARCHIVE DESTINATION lib)
