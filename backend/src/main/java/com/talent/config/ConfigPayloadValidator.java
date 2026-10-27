package com.talent.config;

import com.fasterxml.jackson.databind.JsonNode;

/**
 * Validates a payload before it is saved or published. Modules that own a config type
 * (workflow, access, notification) register one as a Spring bean.
 */
public interface ConfigPayloadValidator {

    ConfigType type();

    /** Throw {@link com.talent.shared.web.BusinessRuleException} when the payload is invalid. */
    void validate(String code, JsonNode payload);
}
