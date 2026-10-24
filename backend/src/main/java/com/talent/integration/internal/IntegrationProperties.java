package com.talent.integration.internal;

import java.util.Map;

import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * @param mode    {@code mock}: read {@code classpath:mock-data/*.json} (no client systems needed).
 *                {@code rest}: call each system's canonical REST endpoint from {@code systems}.
 * @param systems per system: base URL and bearer token for the REST adapter.
 * @param syncCron when the scheduled delta sync runs (default nightly 01:00, done well before 06:00 per NFR §5).
 */
@ConfigurationProperties("talent.integration")
record IntegrationProperties(String mode, Map<String, SystemEndpoint> systems, String syncCron, boolean syncOnStartup) {

    record SystemEndpoint(String baseUrl, String token) {
    }

    IntegrationProperties {
        mode = mode == null ? "mock" : mode;
        systems = systems == null ? Map.of() : systems;
    }
}
