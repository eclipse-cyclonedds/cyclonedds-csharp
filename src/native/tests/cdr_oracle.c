// Copyright(c) 2026 Sven Trittler
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
#include <dds/dds.h>
#include <string.h>
#include "dds__writer.h"
#include <dds/ddsi/ddsi_endpoint.h>
#include <dds/ddsi/ddsi_serdata.h>
#include <dds/ddsi/ddsi_keyhash.h>
#if defined(_WIN32)
#define EXPORT __declspec(dllexport)
#else
#define EXPORT __attribute__((visibility("default")))
#endif
/* Test-only oracle: use Cyclone's C-layout serializer independently of C#. */
EXPORT int32_t oracle_encode(dds_entity_t writer, const void *sample,
    void **buffer, uint32_t *size, unsigned char *hash, uint32_t force_md5) {
  dds_writer *wr;
  dds_return_t ret = dds_writer_lock(writer, &wr);
  if (ret < 0) return ret;
  struct ddsi_serdata *data = ddsi_serdata_from_sample(wr->m_wr->type, SDK_DATA, sample);
  if (!data) ret = DDS_RETCODE_BAD_PARAMETER;
  else {
    struct ddsi_keyhash keyhash;
    ddsi_serdata_get_keyhash(data, &keyhash, force_md5 != 0);
    memcpy(hash, keyhash.value, 16);
    *size = ddsi_serdata_size(data);
    *buffer = dds_alloc(*size);
    ddsi_serdata_to_ser(data, 0, *size, *buffer);
    ddsi_serdata_unref(data);
  }
  dds_writer_unlock(wr);
  return ret;
}
EXPORT int32_t oracle_decode(dds_entity_t writer, const unsigned char *buffer,
    uint32_t size, void *sample, unsigned char *hash, uint32_t force_md5) {
  dds_writer *wr;
  dds_return_t ret = dds_writer_lock(writer, &wr);
  if (ret < 0) return ret;
  ddsrt_iovec_t iov = { .iov_base = (void *)buffer, .iov_len = size };
  struct ddsi_serdata *data = ddsi_serdata_from_ser_iov(wr->m_wr->type, SDK_DATA, 1, &iov, size);
  if (!data) ret = DDS_RETCODE_BAD_PARAMETER;
  else {
    struct ddsi_keyhash keyhash;
    ddsi_serdata_get_keyhash(data, &keyhash, force_md5 != 0);
    memcpy(hash, keyhash.value, 16);
    if (!ddsi_serdata_to_sample(data, sample, NULL, NULL)) ret = DDS_RETCODE_ERROR;
    ddsi_serdata_unref(data);
  }
  dds_writer_unlock(wr);
  return ret;
}
EXPORT void oracle_free(void *buffer) { dds_free(buffer); }
EXPORT void oracle_fini(void *sample, const dds_topic_descriptor_t *descriptor) { dds_sample_free(sample, descriptor, DDS_FREE_CONTENTS); }
