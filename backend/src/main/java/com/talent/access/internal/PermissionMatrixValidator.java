package com.talent.access.internal;

import java.util.HashSet;
import java.util.Set;

import org.springframework.stereotype.Component;

import com.fasterxml.jackson.databind.JsonNode;
import com.talent.access.PermissionMatrix;
import com.talent.access.Roles;
import com.talent.config.ConfigPayloadValidator;
import com.talent.config.ConfigType;
import com.talent.shared.json.Json;
import com.talent.shared.security.Action;
import com.talent.shared.security.DataScope;
import com.talent.shared.security.ObjectType;
import com.talent.shared.web.BusinessRuleException;

@Component
class PermissionMatrixValidator implements ConfigPayloadValidator {

    @Override
    public ConfigType type() {
        return ConfigType.PERMISSION_MATRIX;
    }

    @Override
    public void validate(String code, JsonNode payload) {
        PermissionMatrix m;
        try {
            m = Json.mapper().treeToValue(payload, PermissionMatrix.class);
        } catch (Exception e) {
            throw new BusinessRuleException("CONFIG_INVALID", "Invalid permission matrix: " + e.getMessage());
        }
        if (m.grants() == null || m.grants().isEmpty()) {
            throw new BusinessRuleException("CONFIG_INVALID", "The matrix has no grants");
        }
        Set<String> seen = new HashSet<>();
        boolean configAdmin = false;
        for (PermissionMatrix.Grant g : m.grants()) {
            if (g.role() == null || g.object() == null || g.scope() == null || g.actions() == null || g.actions().isEmpty()) {
                throw new BusinessRuleException("CONFIG_INVALID", "Each grant needs role, object, actions and scope");
            }
            if (!seen.add(g.role() + "/" + g.object() + "/" + g.scope())) {
                throw new BusinessRuleException("CONFIG_INVALID", "Duplicate grant " + g.role() + " / " + g.object() + " / " + g.scope());
            }
            if (Roles.EMPLOYEE.equals(g.role()) && g.confidential()) {
                throw new BusinessRuleException("CONFIG_INVALID", "The EMPLOYEE role cannot be given confidential access");
            }
            if (g.object() == ObjectType.CONFIG && g.actions().contains(Action.APPROVE) && g.scope() == DataScope.ALL) {
                configAdmin = true;
            }
        }
        if (!configAdmin) {
            throw new BusinessRuleException("CONFIG_INVALID", "At least one role must keep CONFIG APPROVE, or nobody could fix the matrix");
        }
    }
}
