package com.talent.integration.internal;

import java.util.EnumSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import static com.talent.integration.internal.EntityKind.*;

@Configuration
class AdapterConfig {

    /** Which canonical entities each system of record owns (architecture §4.12 interface catalogue). */
    static final Map<String, Set<EntityKind>> OWNERSHIP = Map.of(
            "CORE_HR", EnumSet.of(ORG_UNIT, JOB, POSITION, EMPLOYEE),
            "PERFORMANCE", EnumSet.of(COMPETENCY, APPRAISAL),
            "SSHR", EnumSet.of(QUALIFICATION, EXPERIENCE, EXPERTISE),
            "LMS", EnumSet.of(COURSE, ENROLMENT));

    static final List<String> ORDER = List.of("CORE_HR", "PERFORMANCE", "SSHR", "LMS");

    @Bean
    List<SourceAdapter> sourceAdapters(IntegrationProperties props) {
        return ORDER.stream().<SourceAdapter>map(system -> switch (props.mode()) {
            case "mock" -> new MockSourceAdapter(system, "mock-data/" + system.toLowerCase() + ".json");
            case "rest" -> {
                var endpoint = props.systems().get(system);
                if (endpoint == null) {
                    throw new IllegalStateException("talent.integration.systems." + system + ".base-url is required in rest mode");
                }
                yield new RestSourceAdapter(system, OWNERSHIP.get(system), endpoint);
            }
            default -> throw new IllegalStateException("Unknown talent.integration.mode " + props.mode());
        }).toList();
    }
}
