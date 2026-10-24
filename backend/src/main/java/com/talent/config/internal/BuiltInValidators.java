package com.talent.config.internal;

import java.util.HashSet;
import java.util.Set;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import com.fasterxml.jackson.databind.JsonNode;
import com.talent.config.BenchStrengthDefinition;
import com.talent.config.ConfigPayloadValidator;
import com.talent.config.ConfigType;
import com.talent.config.EligibilityEngine;
import com.talent.config.EligibilityRuleDefinition;
import com.talent.config.GridDefinition;
import com.talent.config.IdpProgressDefinition;
import com.talent.config.ProfileMatchWeights;
import com.talent.config.ScaleDefinition;
import com.talent.shared.json.Json;
import com.talent.shared.web.BusinessRuleException;

/** Validators for the config types this module owns. Limits from ADR-03 keep test effort bounded. */
@Configuration
class BuiltInValidators {

    static final int MIN_SCALE_LEVELS = 2;
    static final int MAX_SCALE_LEVELS = 10;
    static final int MIN_GRID = 2;
    static final int MAX_GRID = 5;

    @Bean
    ConfigPayloadValidator scaleValidator() {
        return validator(ConfigType.SCALE, (code, node) -> {
            ScaleDefinition s = parse(node, ScaleDefinition.class);
            if (s.levels() == null || s.levels().size() < MIN_SCALE_LEVELS || s.levels().size() > MAX_SCALE_LEVELS) {
                fail("A scale needs " + MIN_SCALE_LEVELS + " to " + MAX_SCALE_LEVELS + " levels");
            }
            Set<Integer> values = new HashSet<>();
            Set<String> codes = new HashSet<>();
            for (var l : s.levels()) {
                if (!values.add(l.value())) {
                    fail("Duplicate level value " + l.value());
                }
                if (l.code() == null || l.code().isBlank() || !codes.add(l.code())) {
                    fail("Each level needs a unique code");
                }
                if (l.label() == null || l.label().isBlank()) {
                    fail("Each level needs a label");
                }
                if (l.reportingBand() == null || l.reportingBand().isBlank()) {
                    fail("Level " + l.code() + " needs a reporting band (used for trend reports across versions)");
                }
            }
        });
    }

    @Bean
    ConfigPayloadValidator gridValidator() {
        return validator(ConfigType.GRID, (code, node) -> {
            GridDefinition g = parse(node, GridDefinition.class);
            int cols = g.columns() == null ? 0 : g.columns().size();
            int rows = g.rows() == null ? 0 : g.rows().size();
            if (cols < MIN_GRID || cols > MAX_GRID || rows < MIN_GRID || rows > MAX_GRID) {
                fail("Grid size must be between " + MIN_GRID + "×" + MIN_GRID + " and " + MAX_GRID + "×" + MAX_GRID);
            }
            if (g.xScale() == null || g.yScale() == null) {
                fail("Both axes must bind to a scale");
            }
            if (g.mode() == null) {
                fail("Placement mode is required");
            }
            checkRanges(g.columns(), "column");
            checkRanges(g.rows(), "row");
            if (g.cells() == null || g.cells().size() != rows * cols) {
                fail("Grid needs exactly " + rows * cols + " cells");
            }
            Set<String> seen = new HashSet<>();
            Set<String> codes = new HashSet<>();
            for (var c : g.cells()) {
                if (c.row() < 0 || c.row() >= rows || c.col() < 0 || c.col() >= cols || !seen.add(c.row() + ":" + c.col())) {
                    fail("Cell positions must cover every row and column exactly once");
                }
                if (c.code() == null || !codes.add(c.code())) {
                    fail("Each cell needs a unique code");
                }
                if (c.label() == null || c.reportingBand() == null) {
                    fail("Each cell needs a label and a reporting band");
                }
            }
        });
    }

    @Bean
    ConfigPayloadValidator eligibilityValidator() {
        return validator(ConfigType.ELIGIBILITY_RULE, (code, node) -> {
            EligibilityRuleDefinition r = parse(node, EligibilityRuleDefinition.class);
            EligibilityEngine.parse(r.expression());
        });
    }

    @Bean
    ConfigPayloadValidator profileMatchValidator() {
        return validator(ConfigType.PROFILE_MATCH_WEIGHTS, (code, node) -> {
            ProfileMatchWeights w = parse(node, ProfileMatchWeights.class);
            if (w.defaults() == null) {
                fail("Default weights are required");
            }
            checkWeights(w.defaults());
            if (w.perJobFamily() != null) {
                w.perJobFamily().values().forEach(BuiltInValidators::checkWeights);
            }
        });
    }

    @Bean
    ConfigPayloadValidator benchStrengthValidator() {
        return validator(ConfigType.BENCH_STRENGTH, (code, node) -> {
            BenchStrengthDefinition b = parse(node, BenchStrengthDefinition.class);
            if (b.method() == null) {
                fail("Method is required");
            }
            if (b.method() == BenchStrengthDefinition.Method.COUNT && (b.countedReadiness() == null || b.countedReadiness().isEmpty())) {
                fail("COUNT method needs the readiness codes to count");
            }
            if (b.method() == BenchStrengthDefinition.Method.WEIGHTED && (b.readinessWeights() == null || b.readinessWeights().isEmpty())) {
                fail("WEIGHTED method needs readiness weights");
            }
        });
    }

    @Bean
    ConfigPayloadValidator idpProgressValidator() {
        return validator(ConfigType.IDP_PROGRESS, (code, node) -> {
            if (parse(node, IdpProgressDefinition.class).method() == null) {
                fail("Method is required");
            }
        });
    }

    private static void checkWeights(ProfileMatchWeights.Weights w) {
        if (w.qualification() < 0 || w.experience() < 0 || w.competency() < 0 || w.total() <= 0) {
            fail("Weights must be zero or more, and at least one must be above zero");
        }
    }

    private static void checkRanges(java.util.List<GridDefinition.Range> ranges, String what) {
        for (int i = 0; i < ranges.size(); i++) {
            var r = ranges.get(i);
            if (r.min() >= r.max()) {
                fail("Each " + what + " range needs min < max");
            }
            if (i > 0 && ranges.get(i - 1).max() != r.min()) {
                fail("The " + what + " ranges must be in order and touch each other without gaps");
            }
        }
    }

    static <T> T parse(JsonNode node, Class<T> type) {
        try {
            return Json.mapper().treeToValue(node, type);
        } catch (Exception e) {
            throw new BusinessRuleException("CONFIG_INVALID", "Payload is not a valid " + type.getSimpleName() + ": " + e.getMessage());
        }
    }

    static void fail(String message) {
        throw new BusinessRuleException("CONFIG_INVALID", message);
    }

    private static ConfigPayloadValidator validator(ConfigType type, java.util.function.BiConsumer<String, JsonNode> check) {
        return new ConfigPayloadValidator() {
            @Override
            public ConfigType type() {
                return type;
            }

            @Override
            public void validate(String code, JsonNode payload) {
                check.accept(code, payload);
            }
        };
    }
}
