package com.talent.config;

import java.util.Comparator;
import java.util.List;
import java.util.Optional;

/**
 * A rating scale (G-A1). Each level maps to a stable reporting band so trends work across versions (ADR-02).
 */
public record ScaleDefinition(String description, List<Level> levels) {

    public record Level(int value, String code, String label, String colour, String reportingBand) {
    }

    public Optional<Level> level(int value) {
        return levels.stream().filter(l -> l.value() == value).findFirst();
    }

    public Optional<Level> levelByCode(String code) {
        return levels.stream().filter(l -> l.code().equals(code)).findFirst();
    }

    public int min() {
        return levels.stream().mapToInt(Level::value).min().orElse(0);
    }

    public int max() {
        return levels.stream().mapToInt(Level::value).max().orElse(0);
    }

    public List<Level> sorted() {
        return levels.stream().sorted(Comparator.comparingInt(Level::value)).toList();
    }
}
