package com.talent.integration;

import java.util.List;
import java.util.Optional;

/** Read access to synced SSHR, Performance and LMS data (I-03, I-04, I-06, I-07). */
public interface TalentDataQuery {

    ResumeView resume(Long employeeId);

    /** Appraisals, most recent period first. */
    List<AppraisalView> appraisals(Long employeeId);

    Optional<AppraisalView> latestFinalisedAppraisal(Long employeeId);

    List<CourseView> courses();

    Optional<CourseView> course(Long courseId);

    /** Courses mapped to a role (job code) or competency code. */
    List<CourseView> coursesFor(String mappingType, String code);

    List<EnrolmentView> enrolments(Long employeeId);
}
