package com.talent.config;

import java.util.List;

import com.fasterxml.jackson.databind.JsonNode;

/** Read and manage versioned configuration (ADR-02). */
public interface ConfigService {

    /** The effective (latest Published or Locked) version, or throws NotFoundException. */
    ConfigVersionView active(ConfigType type, String code);

    ConfigVersionView version(Long versionId);

    <T> ActiveConfig<T> activePayload(ConfigType type, String code, Class<T> payloadType);

    <T> T payload(Long versionId, Class<T> payloadType);

    /** All effective versions of a type, e.g. every active notification template. */
    List<ConfigVersionView> activeOfType(ConfigType type);

    /**
     * Marks a version as used. Published → Locked; Locked and Retired stay as they are.
     * Call this when a cycle or record first references a version.
     */
    void lock(Long versionId);

    // ---- admin -------------------------------------------------------------------------

    List<ConfigDefinitionView> definitions(ConfigType typeOrNull);

    ConfigDefinitionView definition(Long definitionId);

    /** Creates a definition with version 1 as a Draft. */
    ConfigVersionView create(ConfigType type, String code, String name, String description, JsonNode payload);

    /** Creates a new Draft copied from the latest version. Only one Draft may exist at a time. */
    ConfigVersionView newDraft(Long definitionId);

    ConfigVersionView updateDraft(Long versionId, JsonNode payload);

    ConfigVersionView publish(Long versionId);

    void deleteDraft(Long versionId);

    /** Checks a payload without saving it. */
    void validate(ConfigType type, String code, JsonNode payload);
}
