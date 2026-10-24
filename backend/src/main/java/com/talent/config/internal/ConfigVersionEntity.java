package com.talent.config.internal;

import java.time.Instant;
import java.time.LocalDate;

import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

import com.talent.config.ConfigStatus;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import jakarta.persistence.Version;

@Entity
@Table(name = "config_version")
public class ConfigVersionEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    Long id;
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "definition_id")
    ConfigDefinitionEntity definition;
    int versionNo;
    @Enumerated(EnumType.STRING)
    ConfigStatus status;
    LocalDate effectiveFrom;
    @JdbcTypeCode(SqlTypes.JSON)
    @Column(columnDefinition = "jsonb")
    String payloadJson;
    String checksum;
    Long createdBy;
    Instant createdAt;
    Long publishedBy;
    Instant publishedAt;
    Instant lockedAt;
    Instant retiredAt;
    @Version
    long rowVersion;

    protected ConfigVersionEntity() {
    }

    ConfigVersionEntity(ConfigDefinitionEntity definition, int versionNo, String payloadJson, Long createdBy, Instant createdAt) {
        this.definition = definition;
        this.versionNo = versionNo;
        this.status = ConfigStatus.DRAFT;
        this.payloadJson = payloadJson;
        this.createdBy = createdBy;
        this.createdAt = createdAt;
    }
}
