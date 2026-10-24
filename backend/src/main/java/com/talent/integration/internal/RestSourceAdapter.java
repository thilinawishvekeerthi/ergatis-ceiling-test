package com.talent.integration.internal;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Set;

import org.springframework.core.ParameterizedTypeReference;
import org.springframework.web.client.RestClient;

/**
 * Calls a source system that exposes the canonical contract over REST:
 * {@code GET {baseUrl}/{kind}?since=ISO-8601} and {@code GET {baseUrl}/{kind}?employeeRef=..}, each returning a JSON array.
 * Systems with other protocols (SOAP, DB views, files) get their own adapter once their spec arrives (B6).
 */
class RestSourceAdapter implements SourceAdapter {

    private final String system;
    private final Set<EntityKind> kinds;
    private final RestClient client;

    RestSourceAdapter(String system, Set<EntityKind> kinds, IntegrationProperties.SystemEndpoint endpoint) {
        this.system = system;
        this.kinds = kinds;
        RestClient.Builder b = RestClient.builder().baseUrl(endpoint.baseUrl());
        if (endpoint.token() != null) {
            b.defaultHeader("Authorization", "Bearer " + endpoint.token());
        }
        this.client = b.build();
    }

    @Override
    public String system() {
        return system;
    }

    @Override
    public Set<EntityKind> kinds() {
        return kinds;
    }

    @Override
    public List<Map<String, Object>> fetch(EntityKind kind, Instant since) {
        return client.get().uri(u -> u.path("/" + kind.name().toLowerCase()).queryParamIfPresent("since",
                        java.util.Optional.ofNullable(since).map(Instant::toString)).build())
                .retrieve().body(new ParameterizedTypeReference<>() {
                });
    }

    @Override
    public List<Map<String, Object>> fetchForEmployee(EntityKind kind, String employeeRef) {
        return client.get().uri(u -> u.path("/" + kind.name().toLowerCase()).queryParam("employeeRef", employeeRef).build())
                .retrieve().body(new ParameterizedTypeReference<>() {
                });
    }
}
