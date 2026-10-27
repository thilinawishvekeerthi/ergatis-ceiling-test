package com.talent.config.internal;

import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

/** Typed copy of a published scale's levels, for report queries (ADR-02 "payload storage"). */
@Entity
@Table(name = "scale_level")
public class ScaleLevelEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    Long id;
    Long versionId;
    String scaleCode;
    int levelValue;
    String code;
    String label;
    String colour;
    String reportingBand;

    protected ScaleLevelEntity() {
    }

    ScaleLevelEntity(Long versionId, String scaleCode, int levelValue, String code, String label, String colour, String band) {
        this.versionId = versionId;
        this.scaleCode = scaleCode;
        this.levelValue = levelValue;
        this.code = code;
        this.label = label;
        this.colour = colour;
        this.reportingBand = band;
    }
}
