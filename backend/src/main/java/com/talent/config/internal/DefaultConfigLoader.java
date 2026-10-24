package com.talent.config.internal;

import java.io.InputStream;
import java.util.Arrays;
import java.util.Comparator;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.core.io.Resource;
import org.springframework.core.io.support.PathMatchingResourcePatternResolver;
import org.springframework.stereotype.Component;
import org.springframework.transaction.support.TransactionTemplate;

import com.fasterxml.jackson.databind.JsonNode;
import com.talent.config.ConfigService;
import com.talent.config.ConfigType;
import com.talent.shared.json.Json;

/**
 * Ships strong defaults (architecture §7): loads every {@code classpath:config-defaults/*.json} file whose
 * definition does not exist yet, and publishes it as version 1. Existing configuration is never touched.
 * <p>
 * File format: {@code {"type": "SCALE", "code": "...", "name": "...", "description": "...", "payload": {...}}}
 * or a JSON array of such objects.
 */
@Component
@Order(Ordered.HIGHEST_PRECEDENCE)
class DefaultConfigLoader implements ApplicationRunner {

    private static final Logger log = LoggerFactory.getLogger(DefaultConfigLoader.class);

    private final ConfigService config;
    private final ConfigDefinitionRepository definitions;
    private final TransactionTemplate tx;

    DefaultConfigLoader(ConfigService config, ConfigDefinitionRepository definitions, TransactionTemplate tx) {
        this.config = config;
        this.definitions = definitions;
        this.tx = tx;
    }

    @Override
    public void run(ApplicationArguments args) throws Exception {
        Resource[] files = new PathMatchingResourcePatternResolver().getResources("classpath*:config-defaults/*.json");
        Arrays.sort(files, Comparator.comparing(Resource::getFilename));
        for (Resource file : files) {
            try (InputStream in = file.getInputStream()) {
                JsonNode root = Json.mapper().readTree(in);
                if (root.isArray()) {
                    root.forEach(this::load);
                } else {
                    load(root);
                }
            }
        }
    }

    private void load(JsonNode node) {
        ConfigType type = ConfigType.valueOf(node.get("type").asText());
        String code = node.get("code").asText();
        if (definitions.findByTypeAndCode(type, code).isPresent()) {
            return;
        }
        tx.executeWithoutResult(s -> {
            var v = config.create(type, code, node.get("name").asText(),
                    node.hasNonNull("description") ? node.get("description").asText() : null, node.get("payload"));
            config.publish(v.id());
        });
        log.info("Loaded default configuration {} {}", type, code);
    }
}
