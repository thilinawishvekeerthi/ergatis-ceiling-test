package com.talent.shared.audit;

import java.time.Clock;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Propagation;
import org.springframework.transaction.annotation.Transactional;

import com.talent.shared.json.Json;
import com.talent.shared.security.CurrentUser;

/**
 * Append-only audit log (ADR-07). Call it for every confidential write, every approval and every export.
 * Runs inside the caller's transaction, so a rolled-back change leaves no audit row.
 */
@Service
public class AuditService {

    private final AuditRepository repository;
    private final Clock clock;

    AuditService(AuditRepository repository, Clock clock) {
        this.repository = repository;
        this.clock = clock;
    }

    @Transactional(propagation = Propagation.MANDATORY)
    public void record(String action, String objectType, Object objectId, Long subjectEmployeeId,
                       Object before, Object after, String reason) {
        repository.save(new AuditEntry(clock.instant(), CurrentUser.idOrNull(), action, objectType,
                objectId == null ? null : objectId.toString(), subjectEmployeeId, Json.write(before), Json.write(after), reason));
    }

    /** Same as {@link #record} but opens its own transaction; use for read-only operations such as exports. */
    @Transactional(propagation = Propagation.REQUIRES_NEW)
    public void recordStandalone(String action, String objectType, Object objectId, Long subjectEmployeeId, Object detail) {
        repository.save(new AuditEntry(clock.instant(), CurrentUser.idOrNull(), action, objectType,
                objectId == null ? null : objectId.toString(), subjectEmployeeId, null, Json.write(detail), null));
    }

    @Transactional(readOnly = true)
    public Page<AuditEntry> search(String objectType, String objectId, Long subject, Pageable pageable) {
        return repository.search(objectType, objectId, subject, pageable);
    }
}
