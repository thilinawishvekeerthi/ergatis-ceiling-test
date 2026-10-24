package com.talent.config.internal;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.Clock;
import java.time.LocalDate;
import java.util.HexFormat;
import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;

import org.springframework.context.ApplicationEventPublisher;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.JsonNode;
import com.talent.config.ActiveConfig;
import com.talent.config.ConfigDefinitionView;
import com.talent.config.ConfigPayloadValidator;
import com.talent.config.ConfigService;
import com.talent.config.ConfigStatus;
import com.talent.config.ConfigType;
import com.talent.config.ConfigVersionPublished;
import com.talent.config.ConfigVersionView;
import com.talent.config.ScaleDefinition;
import com.talent.shared.audit.AuditService;
import com.talent.shared.json.Json;
import com.talent.shared.security.CurrentUser;
import com.talent.shared.web.BusinessRuleException;
import com.talent.shared.web.NotFoundException;

@Service
@Transactional
class ConfigServiceImpl implements ConfigService {

    private final ConfigDefinitionRepository definitions;
    private final ConfigVersionRepository versions;
    private final ScaleLevelRepository scaleLevels;
    private final Map<ConfigType, ConfigPayloadValidator> validators;
    private final AuditService audit;
    private final ApplicationEventPublisher events;
    private final Clock clock;

    ConfigServiceImpl(ConfigDefinitionRepository definitions, ConfigVersionRepository versions, ScaleLevelRepository scaleLevels,
                      List<ConfigPayloadValidator> validators, AuditService audit, ApplicationEventPublisher events, Clock clock) {
        this.definitions = definitions;
        this.versions = versions;
        this.scaleLevels = scaleLevels;
        this.validators = validators.stream().collect(Collectors.toMap(ConfigPayloadValidator::type, Function.identity()));
        this.audit = audit;
        this.events = events;
        this.clock = clock;
    }

    @Override
    @Transactional(readOnly = true)
    public ConfigVersionView active(ConfigType type, String code) {
        ConfigDefinitionEntity def = definitions.findByTypeAndCode(type, code)
                .orElseThrow(() -> new NotFoundException("Configuration " + type, code));
        return versions.findEffective(def.id).map(this::view)
                .orElseThrow(() -> new NotFoundException("Published configuration " + type, code));
    }

    @Override
    @Transactional(readOnly = true)
    public ConfigVersionView version(Long versionId) {
        return view(load(versionId));
    }

    @Override
    @Transactional(readOnly = true)
    public <T> ActiveConfig<T> activePayload(ConfigType type, String code, Class<T> payloadType) {
        ConfigVersionView v = active(type, code);
        return new ActiveConfig<>(v.id(), v.versionNo(), Json.convert(v.payload(), payloadType));
    }

    @Override
    @Transactional(readOnly = true)
    public <T> T payload(Long versionId, Class<T> payloadType) {
        return Json.read(load(versionId).payloadJson, payloadType);
    }

    @Override
    @Transactional(readOnly = true)
    public List<ConfigVersionView> activeOfType(ConfigType type) {
        return definitions.findByTypeOrderByCode(type).stream()
                .flatMap(d -> versions.findEffective(d.id).stream())
                .map(this::view)
                .toList();
    }

    @Override
    public void lock(Long versionId) {
        ConfigVersionEntity v = load(versionId);
        if (v.status == ConfigStatus.DRAFT) {
            throw new BusinessRuleException("CONFIG_NOT_PUBLISHED", "A draft configuration cannot be used");
        }
        if (v.status == ConfigStatus.PUBLISHED) {
            v.status = ConfigStatus.LOCKED;
            v.lockedAt = clock.instant();
        }
    }

    @Override
    @Transactional(readOnly = true)
    public List<ConfigDefinitionView> definitions(ConfigType typeOrNull) {
        var defs = typeOrNull == null ? definitions.findAllByOrderByTypeAscCodeAsc() : definitions.findByTypeOrderByCode(typeOrNull);
        return defs.stream().map(this::definitionView).toList();
    }

    @Override
    @Transactional(readOnly = true)
    public ConfigDefinitionView definition(Long definitionId) {
        return definitionView(definitions.findById(definitionId).orElseThrow(() -> new NotFoundException("Configuration", definitionId)));
    }

    @Override
    public ConfigVersionView create(ConfigType type, String code, String name, String description, JsonNode payload) {
        if (definitions.findByTypeAndCode(type, code).isPresent()) {
            throw new BusinessRuleException("CONFIG_EXISTS", type + " " + code + " already exists");
        }
        validate(type, code, payload);
        ConfigDefinitionEntity def = definitions.save(new ConfigDefinitionEntity(type, code, name, description));
        ConfigVersionEntity v = versions.save(new ConfigVersionEntity(def, 1, Json.write(payload), CurrentUser.idOrNull(), clock.instant()));
        audit.record("CONFIG_CREATED", "CONFIG", v.id, null, null, Map.of("type", type, "code", code), null);
        return view(v);
    }

    @Override
    public ConfigVersionView newDraft(Long definitionId) {
        ConfigDefinitionEntity def = definitions.findById(definitionId).orElseThrow(() -> new NotFoundException("Configuration", definitionId));
        List<ConfigVersionEntity> all = versions.findByDefinitionIdOrderByVersionNoDesc(definitionId);
        if (all.stream().anyMatch(v -> v.status == ConfigStatus.DRAFT)) {
            throw new BusinessRuleException("DRAFT_EXISTS", "A draft already exists. Edit or delete it first.");
        }
        String payload = all.isEmpty() ? "{}" : all.get(0).payloadJson;
        ConfigVersionEntity v = versions.save(new ConfigVersionEntity(def, all.isEmpty() ? 1 : all.get(0).versionNo + 1,
                payload, CurrentUser.idOrNull(), clock.instant()));
        return view(v);
    }

    @Override
    public ConfigVersionView updateDraft(Long versionId, JsonNode payload) {
        ConfigVersionEntity v = load(versionId);
        requireDraft(v);
        validate(v.definition.type, v.definition.code, payload);
        String before = v.payloadJson;
        v.payloadJson = Json.write(payload);
        audit.record("CONFIG_DRAFT_UPDATED", "CONFIG", v.id, null, readTree(before), readTree(v.payloadJson), null);
        return view(v);
    }

    @Override
    public ConfigVersionView publish(Long versionId) {
        ConfigVersionEntity v = load(versionId);
        requireDraft(v);
        ConfigDefinitionEntity def = v.definition;
        validate(def.type, def.code, readTree(v.payloadJson));
        versions.findEffective(def.id).ifPresent(prev -> {
            prev.status = ConfigStatus.RETIRED;
            prev.retiredAt = clock.instant();
        });
        v.status = ConfigStatus.PUBLISHED;
        v.publishedAt = clock.instant();
        v.publishedBy = CurrentUser.idOrNull();
        v.effectiveFrom = LocalDate.now(clock);
        v.checksum = sha256(v.payloadJson);
        if (def.type == ConfigType.SCALE) {
            ScaleDefinition scale = Json.read(v.payloadJson, ScaleDefinition.class);
            scaleLevels.deleteByVersionId(v.id);
            scale.levels().forEach(l -> scaleLevels.save(
                    new ScaleLevelEntity(v.id, def.code, l.value(), l.code(), l.label(), l.colour(), l.reportingBand())));
        }
        audit.record("CONFIG_PUBLISHED", "CONFIG", v.id, null, null, Map.of("type", def.type, "code", def.code, "version", v.versionNo), null);
        events.publishEvent(new ConfigVersionPublished(def.type, def.code, v.id, v.versionNo));
        return view(v);
    }

    @Override
    public void deleteDraft(Long versionId) {
        ConfigVersionEntity v = load(versionId);
        requireDraft(v);
        if (v.versionNo == 1) {
            throw new BusinessRuleException("FIRST_VERSION", "Version 1 cannot be deleted");
        }
        versions.delete(v);
    }

    @Override
    public void validate(ConfigType type, String code, JsonNode payload) {
        if (payload == null || payload.isNull()) {
            throw new BusinessRuleException("CONFIG_INVALID", "Payload is required");
        }
        ConfigPayloadValidator validator = validators.get(type);
        if (validator != null) {
            validator.validate(code, payload);
        }
    }

    private void requireDraft(ConfigVersionEntity v) {
        if (v.status != ConfigStatus.DRAFT) {
            throw new BusinessRuleException("CONFIG_IMMUTABLE", "Version " + v.versionNo + " is " + v.status
                    + ". Only drafts can change. Create a new version instead.");
        }
    }

    private ConfigVersionEntity load(Long id) {
        return versions.findById(id).orElseThrow(() -> new NotFoundException("Configuration version", id));
    }

    private ConfigDefinitionView definitionView(ConfigDefinitionEntity d) {
        return new ConfigDefinitionView(d.id, d.type, d.code, d.name, d.description,
                versions.findByDefinitionIdOrderByVersionNoDesc(d.id).stream().map(this::view).toList());
    }

    private ConfigVersionView view(ConfigVersionEntity v) {
        ConfigDefinitionEntity d = v.definition;
        return new ConfigVersionView(v.id, d.id, d.type, d.code, d.name, v.versionNo, v.status, v.effectiveFrom,
                readTree(v.payloadJson), v.checksum, v.createdBy, v.createdAt, v.publishedBy, v.publishedAt);
    }

    private static JsonNode readTree(String json) {
        try {
            return Json.mapper().readTree(json);
        } catch (JsonProcessingException e) {
            throw new IllegalStateException(e);
        }
    }

    private static String sha256(String s) {
        try {
            return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(s.getBytes(StandardCharsets.UTF_8)));
        } catch (Exception e) {
            throw new IllegalStateException(e);
        }
    }
}
