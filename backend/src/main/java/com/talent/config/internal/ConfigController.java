package com.talent.config.internal;

import java.util.EnumSet;
import java.util.List;
import java.util.Set;

import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import com.fasterxml.jackson.databind.JsonNode;
import com.talent.config.ConfigDefinitionView;
import com.talent.config.ConfigService;
import com.talent.config.ConfigType;
import com.talent.config.ConfigVersionView;
import com.talent.config.EligibilityEngine;
import com.talent.shared.security.AccessGuard;
import com.talent.shared.security.Action;
import com.talent.shared.security.ObjectType;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

@RestController
@RequestMapping("/api/config")
class ConfigController {

    private final ConfigService config;
    private final AccessGuard access;

    ConfigController(ConfigService config, AccessGuard access) {
        this.config = config;
        this.access = access;
    }

    /** Types every signed-in user may read, because screens need scale and grid labels. */
    private static final Set<ConfigType> DISPLAY_TYPES = EnumSet.of(ConfigType.SCALE, ConfigType.GRID);

    /** Effective version of one config. Scales and grids are open to all users; other types need CONFIG VIEW. */
    @GetMapping("/active/{type}/{code}")
    ConfigVersionView active(@PathVariable ConfigType type, @PathVariable String code) {
        requireReadable(type);
        return config.active(type, code);
    }

    @GetMapping("/active/{type}")
    List<ConfigVersionView> activeOfType(@PathVariable ConfigType type) {
        requireReadable(type);
        return config.activeOfType(type);
    }

    private void requireReadable(ConfigType type) {
        if (!DISPLAY_TYPES.contains(type)) {
            access.require(Action.VIEW, ObjectType.CONFIG, null);
        }
    }

    @GetMapping("/definitions")
    List<ConfigDefinitionView> definitions(@RequestParam(required = false) ConfigType type) {
        access.require(Action.VIEW, ObjectType.CONFIG, null);
        return config.definitions(type);
    }

    @GetMapping("/definitions/{id}")
    ConfigDefinitionView definition(@PathVariable Long id) {
        access.require(Action.VIEW, ObjectType.CONFIG, null);
        return config.definition(id);
    }

    @GetMapping("/versions/{id}")
    ConfigVersionView version(@PathVariable Long id) {
        access.require(Action.VIEW, ObjectType.CONFIG, null);
        return config.version(id);
    }

    record CreateRequest(@NotNull ConfigType type, @NotBlank String code, @NotBlank String name, String description,
                         @NotNull JsonNode payload) {
    }

    @PostMapping("/definitions")
    ConfigVersionView create(@Valid @RequestBody CreateRequest r) {
        access.require(Action.EDIT, ObjectType.CONFIG, null);
        return config.create(r.type(), r.code(), r.name(), r.description(), r.payload());
    }

    @PostMapping("/definitions/{id}/drafts")
    ConfigVersionView newDraft(@PathVariable Long id) {
        access.require(Action.EDIT, ObjectType.CONFIG, null);
        return config.newDraft(id);
    }

    @PutMapping("/versions/{id}")
    ConfigVersionView updateDraft(@PathVariable Long id, @RequestBody JsonNode payload) {
        access.require(Action.EDIT, ObjectType.CONFIG, null);
        return config.updateDraft(id, payload);
    }

    @PostMapping("/versions/{id}/publish")
    ConfigVersionView publish(@PathVariable Long id) {
        access.require(Action.APPROVE, ObjectType.CONFIG, null);
        return config.publish(id);
    }

    @DeleteMapping("/versions/{id}")
    void deleteDraft(@PathVariable Long id) {
        access.require(Action.EDIT, ObjectType.CONFIG, null);
        config.deleteDraft(id);
    }

    record ValidateRequest(@NotNull ConfigType type, String code, @NotNull JsonNode payload) {
    }

    record ValidateResult(boolean valid, String message) {
    }

    @PostMapping("/validate")
    ValidateResult validate(@Valid @RequestBody ValidateRequest r) {
        access.require(Action.VIEW, ObjectType.CONFIG, null);
        try {
            config.validate(r.type(), r.code(), r.payload());
            return new ValidateResult(true, "Valid");
        } catch (RuntimeException e) {
            return new ValidateResult(false, e.getMessage());
        }
    }

    @PostMapping("/rules/check")
    ValidateResult checkRule(@RequestBody String expression) {
        access.require(Action.VIEW, ObjectType.CONFIG, null);
        try {
            EligibilityEngine.parse(expression);
            return new ValidateResult(true, "Valid");
        } catch (RuntimeException e) {
            return new ValidateResult(false, e.getMessage());
        }
    }
}
