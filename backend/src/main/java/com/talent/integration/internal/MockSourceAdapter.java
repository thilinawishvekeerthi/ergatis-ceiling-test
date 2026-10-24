package com.talent.integration.internal;

import java.io.InputStream;
import java.time.Instant;
import java.util.ArrayList;
import java.util.EnumSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Set;

import org.springframework.core.io.ClassPathResource;

import com.fasterxml.jackson.core.type.TypeReference;
import com.talent.shared.json.Json;

/** Serves canonical records from {@code classpath:mock-data/<system>.json}, so screens can be built without client systems. */
class MockSourceAdapter implements SourceAdapter {

    private final String system;
    private final Map<EntityKind, List<Map<String, Object>>> data = new LinkedHashMap<>();

    MockSourceAdapter(String system, String resource) {
        this.system = system;
        try (InputStream in = new ClassPathResource(resource).getInputStream()) {
            Map<String, List<Map<String, Object>>> raw = Json.mapper().readValue(in, new TypeReference<>() {
            });
            raw.forEach((k, v) -> data.put(EntityKind.valueOf(k), v));
        } catch (Exception e) {
            throw new IllegalStateException("Cannot read mock data " + resource, e);
        }
    }

    @Override
    public String system() {
        return system;
    }

    @Override
    public Set<EntityKind> kinds() {
        return data.isEmpty() ? Set.of() : EnumSet.copyOf(data.keySet());
    }

    @Override
    public List<Map<String, Object>> fetch(EntityKind kind, Instant since) {
        return new ArrayList<>(data.getOrDefault(kind, List.of()));
    }

    @Override
    public List<Map<String, Object>> fetchForEmployee(EntityKind kind, String employeeRef) {
        String key = kind == EntityKind.EMPLOYEE ? "id" : "employeeRef";
        return data.getOrDefault(kind, List.of()).stream()
                .filter(r -> Objects.equals(String.valueOf(r.get(key)), employeeRef)).toList();
    }
}
