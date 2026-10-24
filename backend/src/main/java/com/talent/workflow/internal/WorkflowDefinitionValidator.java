package com.talent.workflow.internal;

import java.util.HashSet;
import java.util.Set;

import org.springframework.stereotype.Component;

import com.fasterxml.jackson.databind.JsonNode;
import com.talent.config.ConfigPayloadValidator;
import com.talent.config.ConfigType;
import com.talent.shared.json.Json;
import com.talent.shared.web.BusinessRuleException;
import com.talent.workflow.WorkflowDefinition;

@Component
class WorkflowDefinitionValidator implements ConfigPayloadValidator {

    @Override
    public ConfigType type() {
        return ConfigType.WORKFLOW;
    }

    @Override
    public void validate(String code, JsonNode payload) {
        WorkflowDefinition d;
        try {
            d = Json.mapper().treeToValue(payload, WorkflowDefinition.class);
        } catch (Exception e) {
            throw fail("Invalid workflow definition: " + e.getMessage());
        }
        if (d.states() == null || d.states().isEmpty() || d.transitions() == null) {
            throw fail("A workflow needs states and transitions");
        }
        Set<String> states = new HashSet<>();
        for (var s : d.states()) {
            if (s.code() == null || !states.add(s.code())) {
                throw fail("State codes must be unique");
            }
            if (!s.terminal() && (s.actor() == null || !s.actor().matches("[A-Z_]+(\\([A-Z_]+\\))?"))) {
                throw fail("State " + s.code() + " needs an actor like DIRECT_SUPERVISOR, ROLE(HR_APPROVER) or NONE");
            }
        }
        if (!states.contains(d.initialState())) {
            throw fail("Initial state " + d.initialState() + " is not defined");
        }
        Set<String> keys = new HashSet<>();
        for (var t : d.transitions()) {
            if (!states.contains(t.from()) || !states.contains(t.to())) {
                throw fail("Transition " + t.action() + " uses an unknown state");
            }
            if (d.state(t.from()).orElseThrow().terminal()) {
                throw fail("Terminal state " + t.from() + " cannot have outgoing transitions");
            }
            if (!keys.add(t.from() + "/" + t.action())) {
                throw fail("Duplicate action " + t.action() + " from " + t.from());
            }
        }
    }

    private static BusinessRuleException fail(String msg) {
        return new BusinessRuleException("CONFIG_INVALID", msg);
    }
}
