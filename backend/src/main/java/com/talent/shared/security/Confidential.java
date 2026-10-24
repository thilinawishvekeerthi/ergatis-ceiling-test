package com.talent.shared.security;

import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Marks a response field as TALENT_CONFIDENTIAL (ADR-07), e.g. nine-box cell, potential, successor status,
 * risk of loss. The field is written as {@code null} unless the current user has an explicit confidential
 * grant for {@link #value()} on the record's subject. The enclosing type must implement {@link HasSubject}.
 */
@Retention(RetentionPolicy.RUNTIME)
@Target({ElementType.FIELD, ElementType.METHOD, ElementType.RECORD_COMPONENT})
public @interface Confidential {

    ObjectType value();
}
