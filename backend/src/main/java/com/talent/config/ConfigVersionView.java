package com.talent.config;

import java.time.Instant;
import java.time.LocalDate;

import com.fasterxml.jackson.databind.JsonNode;

public record ConfigVersionView(Long id, Long definitionId, ConfigType type, String code, String name, int versionNo,
                                ConfigStatus status, LocalDate effectiveFrom, JsonNode payload, String checksum,
                                Long createdBy, Instant createdAt, Long publishedBy, Instant publishedAt) {
}
