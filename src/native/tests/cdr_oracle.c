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
#include "dds__serdata_default.h"
#include <dds/cdr/dds_cdrstream.h>
#include <dds/ddsrt/endian.h>
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
    /* The default serdata-to-sample path uses the allocation capacity as the
       stream size. With an evolved XCDR1 appendable type it can read spare heap
       bytes as added members instead of applying their defaults. Use the same
       native C decoder with a stream bounded by the actual normalized payload. */
    struct dds_serdata_default *serialized = (struct dds_serdata_default *)data;
    const struct dds_sertype_default *type = (const struct dds_sertype_default *)data->type;
    const uint32_t padding = ddsrt_fromBE2u(serialized->hdr.options) & DDS_CDR_HDR_PADDING_MASK;
    const uint32_t version = ddsi_sertype_enc_id_xcdr_version(serialized->hdr.identifier);
    uint32_t actual_size;
    dds_istream_t stream;
    /* Poison spare capacity so accidentally decoding it fails deterministically
       on every platform, including allocators that usually return zeroed pages. */
    memset(serialized->data + serialized->pos, 0xa5, serialized->size - serialized->pos);
    if (serialized->pos < padding || dds_stream_normalize_to_istream_with_flags(&stream,
        serialized->data, serialized->pos - padding, false, version, &type->type,
        false, type->normalize_flags, &actual_size) != DDS_STREAM_NORMALIZE_SUCCESS)
      ret = DDS_RETCODE_BAD_PARAMETER;
    else
      dds_stream_read_sample(&stream, sample, &dds_cdrstream_default_allocator, &type->type);
    ddsi_serdata_unref(data);
  }
  dds_writer_unlock(wr);
  return ret;
}
EXPORT void oracle_free(void *buffer) { dds_free(buffer); }
EXPORT void oracle_fini(void *sample, const dds_topic_descriptor_t *descriptor) { dds_sample_free(sample, descriptor, DDS_FREE_CONTENTS); }
/* Query the native runtime independently of managed IsDisposed bookkeeping. */
EXPORT int32_t oracle_entity_parent(dds_entity_t entity) { return dds_get_parent(entity); }
EXPORT int32_t oracle_entity_children(dds_entity_t entity, dds_entity_t *children, uint32_t capacity) {
  return dds_get_children(entity, children, capacity);
}
