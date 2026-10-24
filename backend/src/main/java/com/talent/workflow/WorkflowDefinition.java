package com.talent.workflow;

import java.util.List;
import java.util.Optional;

/**
 * Payload of config type WORKFLOW. Each non-terminal state names the actor who owns the task in that state,
 * e.g. {@code DIRECT_SUPERVISOR}, {@code HOD}, {@code ROLE(HR_APPROVER)}, {@code CYCLE_ASSIGNMENT(REVIEWER)},
 * {@code SUBJECT} (the employee the record is about) or {@code NONE} (no task; only system transitions).
 */
public record WorkflowDefinition(String description, String initialState, List<State> states, List<Transition> transitions) {

    public record State(String code, String label, String actor, Integer slaDays, boolean terminal) {
    }

    /** {@code system}: only the application may fire it (e.g. publish results); no task needed. */
    public record Transition(String from, String action, String to, String label, boolean commentRequired, boolean system) {
    }

    public Optional<State> state(String code) {
        return states.stream().filter(s -> s.code().equals(code)).findFirst();
    }

    public List<Transition> from(String state) {
        return transitions.stream().filter(t -> t.from().equals(state)).toList();
    }
}
