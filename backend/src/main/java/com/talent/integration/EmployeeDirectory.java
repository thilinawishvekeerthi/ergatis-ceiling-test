package com.talent.integration;

import java.util.Collection;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;

/** Read access to synced Core HR data (I-01, I-02). Local copies, so screens work when Core HR is down. */
public interface EmployeeDirectory {

    Optional<EmployeeView> find(Long employeeId);

    EmployeeView get(Long employeeId);

    Map<Long, EmployeeView> findAll(Collection<Long> employeeIds);

    Optional<EmployeeView> findByEmployeeNo(String employeeNo);

    /** Simple search by name or employee number; optional org unit filter (includes sub-units). */
    List<EmployeeView> search(String text, Long orgUnitId, int limit);

    List<EmployeeView> activeEmployees();

    Set<Long> directReports(Long supervisorId);

    /** Org unit and every unit below it. */
    Set<Long> orgSubtree(Long orgUnitId);

    Set<Long> employeesInOrgUnits(Collection<Long> orgUnitIds);

    /** Org units whose head is this employee. */
    Set<Long> unitsHeadedBy(Long employeeId);

    /** Head of the employee's org unit, walking up the tree when a unit has no head (or the head is the employee). */
    Optional<Long> headOfDepartmentFor(Long employeeId);

    Optional<OrgUnitView> orgUnit(Long orgUnitId);

    List<OrgUnitView> orgUnits();

    Optional<PositionView> position(Long positionId);

    List<PositionView> positions(String positionTypeOrNull);

    Optional<JobView> job(Long jobId);

    Optional<JobView> jobByCode(String code);

    List<JobView> jobs();

    List<CompetencyView> competencies();

    Optional<CompetencyView> competency(String code);
}
