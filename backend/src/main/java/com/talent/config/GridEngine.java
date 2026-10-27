package com.talent.config;

import java.util.List;
import java.util.Optional;

/** Places an (x, y) score pair into a grid cell (ADR-03). Stateless. */
public final class GridEngine {

    private GridEngine() {
    }

    public static Optional<GridDefinition.Cell> place(GridDefinition grid, Double x, Double y) {
        if (x == null || y == null) {
            return Optional.empty();
        }
        int col = index(grid.columns(), x);
        int row = index(grid.rows(), y);
        if (col < 0 || row < 0) {
            return Optional.empty();
        }
        return Optional.of(grid.cellAt(row, col));
    }

    private static int index(List<GridDefinition.Range> ranges, double v) {
        for (int i = 0; i < ranges.size(); i++) {
            var r = ranges.get(i);
            boolean last = i == ranges.size() - 1;
            if (v >= r.min() && (v < r.max() || (last && v <= r.max()))) {
                return i;
            }
        }
        return -1;
    }
}
