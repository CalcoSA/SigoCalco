-- MIGRACION PENDIENTE DE APROBACION. MySQL 8. No ejecutar automaticamente al iniciar la API.
-- Seleccionar explicitamente la base de desarrollo autorizada antes de ejecutar el archivo completo.
-- Requiere respaldo verificado y ventana sin escrituras sobre plantillas/secciones.
-- No borra, fusiona ni renombra plantillas. No modifica versiones, JSON, actas ni Authentication.
-- Agrega una columna derivada y un indice unico GLOBAL al nombre normalizado; amplía el ENUM al final.
-- DDL MySQL hace commit implicito: dos ALTER no forman una transaccion conjunta. Si hay un fallo,
-- inspeccionar el estado antes de recuperar. La rutina rechaza un estado parcial o inesperado.
-- El SELECT previo muestra IDs duplicados sin revelar datos de otras tablas.
SELECT LOWER(TRIM(name)) COLLATE utf8mb4_bin AS normalized_name,
       COUNT(*) AS total, GROUP_CONCAT(template_id ORDER BY template_id) AS template_ids
FROM sigocalco_meetings_template
GROUP BY LOWER(TRIM(name)) COLLATE utf8mb4_bin HAVING COUNT(*) > 1;

DELIMITER $$
CREATE PROCEDURE meetings_migrate_template_sections_20261125()
SQL SECURITY INVOKER
BEGIN
    DECLARE has_column INT DEFAULT 0;
    DECLARE has_index INT DEFAULT 0;
    DECLARE current_type TEXT;
    DECLARE old_type TEXT DEFAULT 'enum(''TEXTO'',''LISTA'',''TABLA'',''FORMULARIO'',''PARTICIPANTES'')';
    IF DATABASE() IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Seleccione una base autorizada antes de migrar.';
    END IF;
    SELECT COUNT(*) INTO has_column FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='sigocalco_meetings_template' AND COLUMN_NAME='name_normalized';
    SELECT COUNT(*) INTO has_index FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='sigocalco_meetings_template'
      AND INDEX_NAME='UX_meetings_template_name_normalized' AND NON_UNIQUE=0 AND COLUMN_NAME='name_normalized';
    SELECT COLUMN_TYPE INTO current_type FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='sigocalco_meetings_template_section' AND COLUMN_NAME='content_type';
    IF has_column<>0 OR has_index<>0 OR current_type IS NULL OR BINARY current_type<>BINARY old_type THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Migracion previa, parcial o esquema inesperado: inspeccione antes de continuar.';
    END IF;
    IF EXISTS (SELECT 1 FROM sigocalco_meetings_template
               GROUP BY LOWER(TRIM(name)) COLLATE utf8mb4_bin HAVING COUNT(*)>1) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Hay nombres duplicados: resolverlos manualmente con aprobacion. No se cambio el esquema.';
    END IF;
    ALTER TABLE sigocalco_meetings_template
        ADD COLUMN name_normalized varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin
            GENERATED ALWAYS AS (lower(trim(`name`))) STORED,
        ADD UNIQUE INDEX UX_meetings_template_name_normalized (name_normalized);
    ALTER TABLE sigocalco_meetings_template_section
        MODIFY COLUMN content_type enum('TEXTO','LISTA','TABLA','FORMULARIO','PARTICIPANTES','SUBTITULO') NOT NULL;
END$$
DELIMITER ;
CALL meetings_migrate_template_sections_20261125();
DROP PROCEDURE meetings_migrate_template_sections_20261125;
-- Si CALL aborta y el cliente deja de ejecutar, retirar solo la rutina auxiliar tras revisar el error.
-- No volver a ejecutar ALTER a ciegas ni usar IF NOT EXISTS para ocultar un estado parcial.
