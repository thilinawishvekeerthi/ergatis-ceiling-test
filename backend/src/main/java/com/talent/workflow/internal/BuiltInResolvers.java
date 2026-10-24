package com.talent.workflow.internal;

import java.util.Set;

import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import com.talent.access.RoleDirectory;
import com.talent.integration.EmployeeDirectory;
import com.talent.workflow.ActorResolver;
import com.talent.workflow.WorkflowSubject;

@Configuration
class BuiltInResolvers {

    @Bean
    ActorResolver directSupervisorResolver(EmployeeDirectory directory) {
        return resolver("DIRECT_SUPERVISOR", (s, arg) -> directory.find(s.subjectEmployeeId())
                .map(e -> e.supervisorId() == null ? Set.<Long>of() : Set.of(e.supervisorId())).orElse(Set.of()));
    }

    @Bean
    ActorResolver hodResolver(EmployeeDirectory directory) {
        return resolver("HOD", (s, arg) -> directory.headOfDepartmentFor(s.subjectEmployeeId()).map(Set::of).orElse(Set.of()));
    }

    @Bean
    ActorResolver roleResolver(RoleDirectory roles) {
        return resolver("ROLE", (s, arg) -> roles.holdersOf(arg));
    }

    @Bean
    ActorResolver subjectResolver() {
        return resolver(WorkflowEngine.SUBJECT, (s, arg) -> s.subjectEmployeeId() == null ? Set.of() : Set.of(s.subjectEmployeeId()));
    }

    private static ActorResolver resolver(String name, java.util.function.BiFunction<WorkflowSubject, String, Set<Long>> fn) {
        return new ActorResolver() {
            @Override
            public String name() {
                return name;
            }

            @Override
            public Set<Long> resolve(WorkflowSubject subject, String argument) {
                return fn.apply(subject, argument);
            }
        };
    }
}
