package com.talent.config;

/** A typed payload together with the version it came from, so callers can pin the version ID. */
public record ActiveConfig<T>(Long versionId, int versionNo, T payload) {
}
