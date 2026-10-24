package com.talent.config.internal;

import com.talent.config.ConfigType;

import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

@Entity
@Table(name = "config_definition")
public class ConfigDefinitionEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    Long id;
    @Enumerated(EnumType.STRING)
    ConfigType type;
    String code;
    String name;
    String description;

    protected ConfigDefinitionEntity() {
    }

    ConfigDefinitionEntity(ConfigType type, String code, String name, String description) {
        this.type = type;
        this.code = code;
        this.name = name;
        this.description = description;
    }
}
