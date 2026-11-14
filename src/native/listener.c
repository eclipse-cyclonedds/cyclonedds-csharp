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
#include <stdlib.h>

#if defined(_WIN32)
#define CS_EXPORT __declspec(dllexport)
#else
#define CS_EXPORT __attribute__((visibility("default")))
#endif

/* Fixed ABI: 64-bit field first, then eight 32-bit fields, on every platform. */
typedef struct cs_status {
  uint64_t last_handle;
  uint32_t kind, total_count;
  int32_t total_change;
  uint32_t current_count;
  int32_t current_change;
  uint32_t not_alive_count;
  int32_t not_alive_change;
  uint32_t reserved;
} cs_status;
_Static_assert(sizeof(cs_status) == 40, "cs_status ABI size");

typedef void (*cs_status_callback)(const cs_status *, void *);
typedef struct cs_listener {
  dds_listener_t *listener;
  cs_status_callback callback;
  void *argument;
} cs_listener;

static void on_data_available(dds_entity_t entity, void *arg) {
  (void)entity;
  cs_listener *ctx = arg;
  const cs_status out = {.kind = DDS_DATA_AVAILABLE_STATUS};
  ctx->callback(&out, ctx->argument);
}

static cs_status convert_publication_matched(dds_publication_matched_status_t status) {
  const cs_status out = {.kind = DDS_PUBLICATION_MATCHED_STATUS,
    .total_count = status.total_count, .total_change = status.total_count_change, .current_count = status.current_count, .current_change = status.current_count_change, .last_handle = status.last_subscription_handle};
  return out;
}
static void on_publication_matched(dds_entity_t entity, dds_publication_matched_status_t status, void *arg) {
  (void)entity;
  cs_listener *ctx = arg;
  const cs_status out = convert_publication_matched(status);
  ctx->callback(&out, ctx->argument);
}

static cs_status convert_subscription_matched(dds_subscription_matched_status_t status) {
  const cs_status out = {.kind = DDS_SUBSCRIPTION_MATCHED_STATUS,
    .total_count = status.total_count, .total_change = status.total_count_change, .current_count = status.current_count, .current_change = status.current_count_change, .last_handle = status.last_publication_handle};
  return out;
}
static void on_subscription_matched(dds_entity_t entity, dds_subscription_matched_status_t status, void *arg) {
  (void)entity;
  cs_listener *ctx = arg;
  const cs_status out = convert_subscription_matched(status);
  ctx->callback(&out, ctx->argument);
}

static cs_status convert_offered_deadline_missed(dds_offered_deadline_missed_status_t status) {
  const cs_status out = {.kind = DDS_OFFERED_DEADLINE_MISSED_STATUS,
    .total_count = status.total_count, .total_change = status.total_count_change, .last_handle = status.last_instance_handle};
  return out;
}
static void on_offered_deadline_missed(dds_entity_t entity, dds_offered_deadline_missed_status_t status, void *arg) {
  (void)entity;
  cs_listener *ctx = arg;
  const cs_status out = convert_offered_deadline_missed(status);
  ctx->callback(&out, ctx->argument);
}

static cs_status convert_requested_deadline_missed(dds_requested_deadline_missed_status_t status) {
  const cs_status out = {.kind = DDS_REQUESTED_DEADLINE_MISSED_STATUS,
    .total_count = status.total_count, .total_change = status.total_count_change, .last_handle = status.last_instance_handle};
  return out;
}
static void on_requested_deadline_missed(dds_entity_t entity, dds_requested_deadline_missed_status_t status, void *arg) {
  (void)entity;
  cs_listener *ctx = arg;
  const cs_status out = convert_requested_deadline_missed(status);
  ctx->callback(&out, ctx->argument);
}

static cs_status convert_liveliness_lost(dds_liveliness_lost_status_t status) {
  const cs_status out = {.kind = DDS_LIVELINESS_LOST_STATUS,
    .total_count = status.total_count, .total_change = status.total_count_change};
  return out;
}
static void on_liveliness_lost(dds_entity_t entity, dds_liveliness_lost_status_t status, void *arg) {
  (void)entity;
  cs_listener *ctx = arg;
  const cs_status out = convert_liveliness_lost(status);
  ctx->callback(&out, ctx->argument);
}

static cs_status convert_liveliness_changed(dds_liveliness_changed_status_t status) {
  const cs_status out = {.kind = DDS_LIVELINESS_CHANGED_STATUS,
    .current_count = status.alive_count, .current_change = status.alive_count_change, .not_alive_count = status.not_alive_count, .not_alive_change = status.not_alive_count_change, .last_handle = status.last_publication_handle};
  return out;
}
static void on_liveliness_changed(dds_entity_t entity, dds_liveliness_changed_status_t status, void *arg) {
  (void)entity;
  cs_listener *ctx = arg;
  const cs_status out = convert_liveliness_changed(status);
  ctx->callback(&out, ctx->argument);
}

CS_EXPORT int32_t cs_listener_create(cs_status_callback callback, void *argument, cs_listener **out) {
  *out = NULL;
  cs_listener *ctx = calloc(1, sizeof(*ctx));
  if (!ctx) return DDS_RETCODE_OUT_OF_RESOURCES;
  ctx->callback = callback;
  ctx->argument = argument;
  ctx->listener = dds_create_listener(ctx);
  if (!ctx->listener) { free(ctx); return DDS_RETCODE_OUT_OF_RESOURCES; }
  *out = ctx;
  return DDS_RETCODE_OK;
}

CS_EXPORT int32_t cs_listener_set(dds_entity_t entity, cs_listener *ctx, uint32_t mask) {
  dds_lset_data_available(ctx->listener, (mask & DDS_DATA_AVAILABLE_STATUS) ? on_data_available : NULL);
  dds_lset_publication_matched(ctx->listener, (mask & DDS_PUBLICATION_MATCHED_STATUS) ? on_publication_matched : NULL);
  dds_lset_subscription_matched(ctx->listener, (mask & DDS_SUBSCRIPTION_MATCHED_STATUS) ? on_subscription_matched : NULL);
  dds_lset_offered_deadline_missed(ctx->listener, (mask & DDS_OFFERED_DEADLINE_MISSED_STATUS) ? on_offered_deadline_missed : NULL);
  dds_lset_requested_deadline_missed(ctx->listener, (mask & DDS_REQUESTED_DEADLINE_MISSED_STATUS) ? on_requested_deadline_missed : NULL);
  dds_lset_liveliness_lost(ctx->listener, (mask & DDS_LIVELINESS_LOST_STATUS) ? on_liveliness_lost : NULL);
  dds_lset_liveliness_changed(ctx->listener, (mask & DDS_LIVELINESS_CHANGED_STATUS) ? on_liveliness_changed : NULL);
  /* dds_set_listener waits for in-flight callbacks before replacing the listener. */
  return dds_set_listener(entity, mask ? ctx->listener : NULL);
}

/* Call only after detaching the listener from its entity. */
CS_EXPORT void cs_listener_free(cs_listener *ctx) {
  if (!ctx) return;
  dds_delete_listener(ctx->listener);
  free(ctx);
}

CS_EXPORT int32_t cs_get_status(dds_entity_t entity, uint32_t kind, cs_status *out) {
  *out = (cs_status){0};
  switch (kind) {
    case DDS_PUBLICATION_MATCHED_STATUS: {
      dds_publication_matched_status_t status;
      const dds_return_t ret = dds_get_publication_matched_status(entity, &status);
      if (ret == DDS_RETCODE_OK) *out = convert_publication_matched(status);
      return ret;
    }
    case DDS_SUBSCRIPTION_MATCHED_STATUS: {
      dds_subscription_matched_status_t status;
      const dds_return_t ret = dds_get_subscription_matched_status(entity, &status);
      if (ret == DDS_RETCODE_OK) *out = convert_subscription_matched(status);
      return ret;
    }
    case DDS_OFFERED_DEADLINE_MISSED_STATUS: {
      dds_offered_deadline_missed_status_t status;
      const dds_return_t ret = dds_get_offered_deadline_missed_status(entity, &status);
      if (ret == DDS_RETCODE_OK) *out = convert_offered_deadline_missed(status);
      return ret;
    }
    case DDS_REQUESTED_DEADLINE_MISSED_STATUS: {
      dds_requested_deadline_missed_status_t status;
      const dds_return_t ret = dds_get_requested_deadline_missed_status(entity, &status);
      if (ret == DDS_RETCODE_OK) *out = convert_requested_deadline_missed(status);
      return ret;
    }
    case DDS_LIVELINESS_LOST_STATUS: {
      dds_liveliness_lost_status_t status;
      const dds_return_t ret = dds_get_liveliness_lost_status(entity, &status);
      if (ret == DDS_RETCODE_OK) *out = convert_liveliness_lost(status);
      return ret;
    }
    case DDS_LIVELINESS_CHANGED_STATUS: {
      dds_liveliness_changed_status_t status;
      const dds_return_t ret = dds_get_liveliness_changed_status(entity, &status);
      if (ret == DDS_RETCODE_OK) *out = convert_liveliness_changed(status);
      return ret;
    }
    default: return DDS_RETCODE_BAD_PARAMETER;
  }
}
