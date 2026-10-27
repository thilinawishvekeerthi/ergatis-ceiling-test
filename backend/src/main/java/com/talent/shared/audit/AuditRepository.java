package com.talent.shared.audit;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

interface AuditRepository extends JpaRepository<AuditEntry, Long> {

    @Query("""
            select a from AuditEntry a
            where (:objectType is null or a.objectType = :objectType)
              and (:objectId is null or a.objectId = :objectId)
              and (:subject is null or a.subjectEmployeeId = :subject)
            order by a.id desc""")
    Page<AuditEntry> search(@Param("objectType") String objectType, @Param("objectId") String objectId,
                            @Param("subject") Long subject, Pageable pageable);
}
