package com.talent.config.internal;

import java.util.List;
import java.util.Optional;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

import com.talent.config.ConfigType;

interface ConfigDefinitionRepository extends JpaRepository<ConfigDefinitionEntity, Long> {

    Optional<ConfigDefinitionEntity> findByTypeAndCode(ConfigType type, String code);

    List<ConfigDefinitionEntity> findByTypeOrderByCode(ConfigType type);

    List<ConfigDefinitionEntity> findAllByOrderByTypeAscCodeAsc();
}

interface ConfigVersionRepository extends JpaRepository<ConfigVersionEntity, Long> {

    List<ConfigVersionEntity> findByDefinitionIdOrderByVersionNoDesc(Long definitionId);

    @Query("""
            select v from ConfigVersionEntity v
            where v.definition.id = :definitionId and v.status in (com.talent.config.ConfigStatus.PUBLISHED, com.talent.config.ConfigStatus.LOCKED)
            order by v.versionNo desc limit 1""")
    Optional<ConfigVersionEntity> findEffective(Long definitionId);

    @Query("select max(v.versionNo) from ConfigVersionEntity v where v.definition.id = :definitionId")
    Integer maxVersionNo(Long definitionId);
}

interface ScaleLevelRepository extends JpaRepository<ScaleLevelEntity, Long> {

    void deleteByVersionId(Long versionId);
}
