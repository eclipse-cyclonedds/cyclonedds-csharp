#!/usr/bin/env bash
# Copyright(c) 2026 Sven Trittler
#
# This program and the accompanying materials are made available under the
# terms of the Eclipse Public License v. 2.0 which is available at
# http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
# v. 1.0 which is available at
# http://www.eclipse.org/org/documents/edl-v10.php.
#
# SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
set -euo pipefail
repo_dir="$(cd "$(dirname "$0")/.." && pwd)"
if [[ $# -gt 1 || ( $# -eq 1 && "$1" != "--source-only" ) ]]; then
  printf 'Usage: %s [--source-only]\n' "$0" >&2
  exit 1
fi
native_ref=master
version="$(tr -d '\r\n' < "$repo_dir/VERSION")"
release_tag=""
if [[ "${GITHUB_REF_TYPE:-}" == "tag" ]]; then
  release_tag="${GITHUB_REF_NAME:?Missing GitHub tag name}"
elif [[ -z "${GITHUB_REF_TYPE:-}" ]] && ! git -C "$repo_dir" symbolic-ref -q HEAD >/dev/null 2>&1; then
  # Local builds from a release checkout follow the same policy as CI.
  release_tag="$(git -C "$repo_dir" describe --tags --exact-match HEAD 2>/dev/null || true)"
fi
if [[ -n "$release_tag" ]]; then
  if [[ "${release_tag#v}" != "$version" ]]; then
    printf 'Release tag %s does not match VERSION %s.\n' "$release_tag" "$version" >&2
    exit 1
  fi
  native_ref="$version"
fi
source_dir="$repo_dir/build/_deps/cyclonedds-src"
binary_dir="$repo_dir/build/_deps/cyclonedds-build"
install_dir="$repo_dir/build/_deps/cyclonedds-install"
git clone --depth 1 --branch "$native_ref" \
  https://github.com/eclipse-cyclonedds/cyclonedds.git "$source_dir"
if [[ "${1:-}" == "--source-only" ]]; then
  printf 'Native source: %s (%s)\n' "$source_dir" "$native_ref"
  exit 0
fi
cmake -S "$source_dir" -B "$binary_dir" \
  -DCMAKE_BUILD_TYPE=Release -DCMAKE_INSTALL_PREFIX="$install_dir" \
  '-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded$<$<CONFIG:Debug>:Debug>DLL' \
  -DBUILD_SHARED_LIBS=ON -DBUILD_TESTING=OFF -DBUILD_EXAMPLES=OFF -DBUILD_DDSPERF=OFF \
  -DENABLE_SECURITY=OFF -DENABLE_SSL=OFF
cmake --build "$binary_dir" --parallel
cmake --install "$binary_dir"
printf '\nNative installation: %s\n' "$install_dir"
