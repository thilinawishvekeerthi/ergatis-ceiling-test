package com.talent.shared.audit;

import java.time.Instant;

import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

/** Append-only. The database rejects UPDATE and DELETE on this table (see V1 migration). */
@Entity
@Table(name = "audit_log")
public class AuditEntry {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;
    private Instant occurredAt;
    private Long actorEmployeeId;
    private String action;
    private String objectType;
    private String objectId;
    private Long subjectEmployeeId;
    @JdbcTypeCode(SqlTypes.JSON)
    @Column(columnDefinition = "jsonb")
    private String beforeValue;
    @JdbcTypeCode(SqlTypes.JSON)
    @Column(columnDefinition = "jsonb")
    private String afterValue;
    private String reason;

    protected AuditEntry() {
    }

    AuditEntry(Instant occurredAt, Long actor, String action, String objectType, String objectId, Long subject,
               String before, String after, String reason) {
        this.occurredAt = occurredAt;
        this.actorEmployeeId = actor;
        this.action = action;
        this.objectType = objectType;
        this.objectId = objectId;
        this.subjectEmployeeId = subject;
        this.beforeValue = before;
        this.afterValue = after;
        this.reason = reason;
    }

    public Long getId() { return id; }
    public Instant getOccurredAt() { return occurredAt; }
    public Long getActorEmployeeId() { return actorEmployeeId; }
    public String getAction() { return action; }
    public String getObjectType() { return objectType; }
    public String getObjectId() { return objectId; }
    public Long getSubjectEmployeeId() { return subjectEmployeeId; }
    public String getBeforeValue() { return beforeValue; }
    public String getAfterValue() { return afterValue; }
    public String getReason() { return reason; }
}
