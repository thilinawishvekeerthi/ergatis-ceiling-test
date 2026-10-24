package com.talent.config;

import java.util.List;

/**
 * A configured N × M grid (ADR-03). The "nine-box" is a 3 × 3 instance.
 * Column index 0 is the lowest X range; row index 0 is the lowest Y range.
 * A range matches {@code min <= score < max}; the last range also includes {@code max}.
 */
public record GridDefinition(String xScale, String xLabel, String yScale, String yLabel,
                             List<Range> columns, List<Range> rows, List<Cell> cells, PlacementMode mode) {

    public enum PlacementMode { MANUAL, CALCULATED, CALCULATED_WITH_OVERRIDE }

    public record Range(String label, double min, double max) {
    }

    public record Cell(int row, int col, String code, String label, String colour, String reportingBand) {
    }

    public Cell cell(String code) {
        return cells.stream().filter(c -> c.code().equals(code)).findFirst()
                .orElseThrow(() -> new IllegalArgumentException("Unknown grid cell " + code));
    }

    public Cell cellAt(int row, int col) {
        return cells.stream().filter(c -> c.row() == row && c.col() == col).findFirst()
                .orElseThrow(() -> new IllegalArgumentException("No cell at " + row + "," + col));
    }
}
