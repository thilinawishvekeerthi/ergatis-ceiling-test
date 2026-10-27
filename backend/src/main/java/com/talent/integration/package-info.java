/**
 * Integration layer (ADR-08): canonical model (Employee, OrgUnit, Position, Job, Competency, AppraisalResult,
 * Qualification, Experience, Course, CourseEnrolment), one adapter per source system, External ID map,
 * scheduled delta sync with dead-letter handling. Talent never owns this master data.
 */
@org.springframework.modulith.ApplicationModule(displayName = "Integration")
package com.talent.integration;
