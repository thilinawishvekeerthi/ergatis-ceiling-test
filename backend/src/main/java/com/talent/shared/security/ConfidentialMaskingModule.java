package com.talent.shared.security;

import java.util.List;

import org.springframework.beans.factory.ObjectProvider;
import org.springframework.stereotype.Component;

import com.fasterxml.jackson.core.JsonGenerator;
import com.fasterxml.jackson.databind.BeanDescription;
import com.fasterxml.jackson.databind.SerializationConfig;
import com.fasterxml.jackson.databind.SerializerProvider;
import com.fasterxml.jackson.databind.module.SimpleModule;
import com.fasterxml.jackson.databind.ser.BeanPropertyWriter;
import com.fasterxml.jackson.databind.ser.BeanSerializerModifier;

/**
 * Masks {@link Confidential} fields in every API response (ADR-07 field classification).
 * Masking happens at serialisation, so no controller can forget it.
 */
@Component
public class ConfidentialMaskingModule extends SimpleModule {

    public ConfidentialMaskingModule(ObjectProvider<AccessGuard> guard) {
        super("confidential-masking");
        setSerializerModifier(new BeanSerializerModifier() {
            @Override
            public List<BeanPropertyWriter> changeProperties(SerializationConfig config, BeanDescription desc, List<BeanPropertyWriter> props) {
                return props.stream().map(p -> {
                    Confidential c = p.getAnnotation(Confidential.class);
                    if (c == null && p.getMember() != null) {
                        c = p.getMember().getAnnotation(Confidential.class);
                    }
                    return c == null ? p : new MaskingWriter(p, c.value(), guard);
                }).toList();
            }
        });
    }

    static final class MaskingWriter extends BeanPropertyWriter {
        private final ObjectType object;
        private final transient ObjectProvider<AccessGuard> guard;

        MaskingWriter(BeanPropertyWriter base, ObjectType object, ObjectProvider<AccessGuard> guard) {
            super(base);
            this.object = object;
            this.guard = guard;
        }

        @Override
        public void serializeAsField(Object bean, JsonGenerator gen, SerializerProvider prov) throws Exception {
            if (bean instanceof HasSubject hs && CurrentUser.orNull() != null
                    && !guard.getObject().canViewConfidential(object, hs.subjectEmployeeId())) {
                gen.writeNullField(getName());
                return;
            }
            if (!(bean instanceof HasSubject)) {
                throw new IllegalStateException(bean.getClass().getSimpleName() + " has @Confidential fields but does not implement HasSubject");
            }
            super.serializeAsField(bean, gen, prov);
        }
    }
}
