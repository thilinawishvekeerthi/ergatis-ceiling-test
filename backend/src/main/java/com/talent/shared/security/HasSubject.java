package com.talent.shared.security;

/** A response object about one employee. Needed for {@link Confidential} field masking. */
public interface HasSubject {

    Long subjectEmployeeId();
}
