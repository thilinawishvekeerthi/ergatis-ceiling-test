package com.talent.config;

import java.util.List;

public record ConfigDefinitionView(Long id, ConfigType type, String code, String name, String description,
                                   List<ConfigVersionView> versions) {
}
