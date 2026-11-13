CREATE TABLE `sigocalco_meetings_acta_status` (
    `acta_status_id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_acta_status` PRIMARY KEY (`acta_status_id`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_acta_type` (
    `acta_type_id` int NOT NULL AUTO_INCREMENT,
    `code` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `name` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_acta_type` PRIMARY KEY (`acta_type_id`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_log` (
    `log_id` bigint NOT NULL AUTO_INCREMENT,
    `action` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `module` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `user_name` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_log` PRIMARY KEY (`log_id`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_meeting_status` (
    `meeting_status_id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_meeting_status` PRIMARY KEY (`meeting_status_id`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_participant` (
    `participant_id` int NOT NULL AUTO_INCREMENT,
    `userLogin` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `userName` varchar(250) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `email` varchar(254) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `participant_type` enum('INTERNO','EXTERNO') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_participant` PRIMARY KEY (`participant_id`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_permission` (
    `permission_id` int NOT NULL AUTO_INCREMENT,
    `permission_key` varchar(120) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `permission_value` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_permission` PRIMARY KEY (`permission_id`)
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_template` (
    `template_id` int NOT NULL AUTO_INCREMENT,
    `acta_type_id` int NOT NULL,
    `name` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `scope` enum('GLOBAL','CENTRO') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `solution_center_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `created_by_user` int NOT NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_template` PRIMARY KEY (`template_id`),
    CONSTRAINT `AK_sigocalco_meetings_template_template_id_acta_type_id` UNIQUE (`template_id`, `acta_type_id`),
    CONSTRAINT `chk_meetings_template_scope` CHECK ((scope = 'GLOBAL' AND solution_center_code IS NULL) OR (scope = 'CENTRO' AND solution_center_code IS NOT NULL AND CHAR_LENGTH(TRIM(solution_center_code)) > 0)),
    CONSTRAINT `FK_sigocalco_meetings_template_sigocalco_meetings_acta_type_act~` FOREIGN KEY (`acta_type_id`) REFERENCES `sigocalco_meetings_acta_type` (`acta_type_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_template_version` (
    `template_version_id` int NOT NULL AUTO_INCREMENT,
    `template_id` int NOT NULL,
    `acta_type_id` int NOT NULL,
    `version_number` int NOT NULL,
    `created_by_user` int NOT NULL,
    `created_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_template_version` PRIMARY KEY (`template_version_id`),
    CONSTRAINT `AK_sigocalco_meetings_template_version_template_version_id_acta~` UNIQUE (`template_version_id`, `acta_type_id`),
    CONSTRAINT `chk_meetings_version_number` CHECK (version_number > 0),
    CONSTRAINT `FK_sigocalco_meetings_template_version_sigocalco_meetings_templ~` FOREIGN KEY (`template_id`, `acta_type_id`) REFERENCES `sigocalco_meetings_template` (`template_id`, `acta_type_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_acta` (
    `acta_id` int NOT NULL AUTO_INCREMENT,
    `acta_code` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `acta_type_id` int NOT NULL,
    `template_version_id` int NOT NULL,
    `solution_center_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `title` varchar(250) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `document_date` date NOT NULL,
    `acta_status_id` int NOT NULL,
    `created_by_user` int NOT NULL,
    `updated_by_user` int NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `closed_at` datetime(6) NULL,
    CONSTRAINT `PK_sigocalco_meetings_acta` PRIMARY KEY (`acta_id`),
    CONSTRAINT `AK_sigocalco_meetings_acta_acta_id_template_version_id` UNIQUE (`acta_id`, `template_version_id`),
    CONSTRAINT `FK_sigocalco_meetings_acta_sigocalco_meetings_acta_status_acta_~` FOREIGN KEY (`acta_status_id`) REFERENCES `sigocalco_meetings_acta_status` (`acta_status_id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_sigocalco_meetings_acta_sigocalco_meetings_acta_type_acta_ty~` FOREIGN KEY (`acta_type_id`) REFERENCES `sigocalco_meetings_acta_type` (`acta_type_id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_sigocalco_meetings_acta_sigocalco_meetings_template_version_~` FOREIGN KEY (`template_version_id`, `acta_type_id`) REFERENCES `sigocalco_meetings_template_version` (`template_version_id`, `acta_type_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_template_section` (
    `template_section_id` int NOT NULL AUTO_INCREMENT,
    `template_version_id` int NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `content_type` enum('TEXTO','LISTA','TABLA','FORMULARIO','PARTICIPANTES') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `configuration_json` json NULL,
    `sort_order` int NOT NULL,
    `is_required` tinyint(1) NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_template_section` PRIMARY KEY (`template_section_id`),
    CONSTRAINT `AK_sigocalco_meetings_template_section_template_section_id_temp~` UNIQUE (`template_section_id`, `template_version_id`),
    CONSTRAINT `chk_meetings_section_order` CHECK (sort_order > 0),
    CONSTRAINT `FK_sigocalco_meetings_template_section_sigocalco_meetings_templ~` FOREIGN KEY (`template_version_id`) REFERENCES `sigocalco_meetings_template_version` (`template_version_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_acta_center_permission` (
    `acta_center_permission_id` int NOT NULL AUTO_INCREMENT,
    `acta_id` int NOT NULL,
    `solution_center_code` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `permission` enum('LECTURA','EDICION') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `granted_by_user_id` int NOT NULL,
    `granted_at` datetime(6) NOT NULL,
    `revoked_at` datetime(6) NULL,
    CONSTRAINT `PK_sigocalco_meetings_acta_center_permission` PRIMARY KEY (`acta_center_permission_id`),
    CONSTRAINT `FK_sigocalco_meetings_acta_center_permission_sigocalco_meetings~` FOREIGN KEY (`acta_id`) REFERENCES `sigocalco_meetings_acta` (`acta_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_acta_participant` (
    `acta_participant_id` int NOT NULL AUTO_INCREMENT,
    `acta_id` int NOT NULL,
    `participant_id` int NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_acta_participant` PRIMARY KEY (`acta_participant_id`),
    CONSTRAINT `FK_sigocalco_meetings_acta_participant_sigocalco_meetings_acta_~` FOREIGN KEY (`acta_id`) REFERENCES `sigocalco_meetings_acta` (`acta_id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_sigocalco_meetings_acta_participant_sigocalco_meetings_parti~` FOREIGN KEY (`participant_id`) REFERENCES `sigocalco_meetings_participant` (`participant_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_acta_user_permission` (
    `acta_user_permission_id` int NOT NULL AUTO_INCREMENT,
    `acta_id` int NOT NULL,
    `user_id` int NOT NULL,
    `permission` enum('LECTURA','EDICION') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `granted_by_user_id` int NOT NULL,
    `granted_at` datetime(6) NOT NULL,
    `revoked_at` datetime(6) NULL,
    CONSTRAINT `PK_sigocalco_meetings_acta_user_permission` PRIMARY KEY (`acta_user_permission_id`),
    CONSTRAINT `FK_sigocalco_meetings_acta_user_permission_sigocalco_meetings_a~` FOREIGN KEY (`acta_id`) REFERENCES `sigocalco_meetings_acta` (`acta_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_meeting` (
    `meeting_id` int NOT NULL AUTO_INCREMENT,
    `acta_id` int NOT NULL,
    `modality` enum('PRESENCIAL','VIRTUAL','HIBRIDA') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `start_at_utc` datetime(6) NOT NULL,
    `end_at_utc` datetime(6) NOT NULL,
    `time_zone` varchar(60) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'America/Bogota',
    `location_description` varchar(250) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `organizer_user_id` int NOT NULL,
    `meeting_status_id` int NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    CONSTRAINT `PK_sigocalco_meetings_meeting` PRIMARY KEY (`meeting_id`),
    CONSTRAINT `chk_meetings_meeting_dates` CHECK (end_at_utc > start_at_utc),
    CONSTRAINT `FK_sigocalco_meetings_meeting_sigocalco_meetings_acta_acta_id` FOREIGN KEY (`acta_id`) REFERENCES `sigocalco_meetings_acta` (`acta_id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_sigocalco_meetings_meeting_sigocalco_meetings_meeting_status~` FOREIGN KEY (`meeting_status_id`) REFERENCES `sigocalco_meetings_meeting_status` (`meeting_status_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_acta_section` (
    `acta_section_id` int NOT NULL AUTO_INCREMENT,
    `acta_id` int NOT NULL,
    `template_version_id` int NOT NULL,
    `template_section_id` int NOT NULL,
    `content_json` json NULL,
    CONSTRAINT `PK_sigocalco_meetings_acta_section` PRIMARY KEY (`acta_section_id`),
    CONSTRAINT `FK_sigocalco_meetings_acta_section_sigocalco_meetings_acta_acta~` FOREIGN KEY (`acta_id`, `template_version_id`) REFERENCES `sigocalco_meetings_acta` (`acta_id`, `template_version_id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_sigocalco_meetings_acta_section_sigocalco_meetings_template_~` FOREIGN KEY (`template_section_id`, `template_version_id`) REFERENCES `sigocalco_meetings_template_section` (`template_section_id`, `template_version_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_meeting_calendar` (
    `meeting_calendar_id` int NOT NULL AUTO_INCREMENT,
    `meeting_id` int NOT NULL,
    `external_event_id` varchar(512) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `organizer_email` varchar(254) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `create_teams` tinyint(1) NOT NULL,
    `teams_join_url` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `sync_status` enum('PENDIENTE','SINCRONIZADO','ERROR','CANCELADO') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDIENTE',
    `last_error` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `synchronized_at` datetime(6) NULL,
    CONSTRAINT `PK_sigocalco_meetings_meeting_calendar` PRIMARY KEY (`meeting_calendar_id`),
    CONSTRAINT `FK_sigocalco_meetings_meeting_calendar_sigocalco_meetings_meeti~` FOREIGN KEY (`meeting_id`) REFERENCES `sigocalco_meetings_meeting` (`meeting_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE TABLE `sigocalco_meetings_meeting_reservation` (
    `meeting_reservation_id` int NOT NULL AUTO_INCREMENT,
    `meeting_id` int NOT NULL,
    `external_reservation_id` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `external_room_id` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `reservation_status` enum('SOLICITADA','CONFIRMADA','RECHAZADA','CANCELADA','ERROR') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'SOLICITADA',
    `requested_at` datetime(6) NOT NULL,
    `synchronized_at` datetime(6) NULL,
    CONSTRAINT `PK_sigocalco_meetings_meeting_reservation` PRIMARY KEY (`meeting_reservation_id`),
    CONSTRAINT `FK_sigocalco_meetings_meeting_reservation_sigocalco_meetings_me~` FOREIGN KEY (`meeting_id`) REFERENCES `sigocalco_meetings_meeting` (`meeting_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


CREATE UNIQUE INDEX `IX_sigocalco_meetings_acta_acta_code` ON `sigocalco_meetings_acta` (`acta_code`);


CREATE INDEX `IX_sigocalco_meetings_acta_acta_status_id` ON `sigocalco_meetings_acta` (`acta_status_id`);


CREATE INDEX `IX_sigocalco_meetings_acta_acta_type_id` ON `sigocalco_meetings_acta` (`acta_type_id`);


CREATE INDEX `IX_sigocalco_meetings_acta_solution_center_code_created_by_user~` ON `sigocalco_meetings_acta` (`solution_center_code`, `created_by_user`, `document_date`);


CREATE INDEX `IX_sigocalco_meetings_acta_template_version_id_acta_type_id` ON `sigocalco_meetings_acta` (`template_version_id`, `acta_type_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_acta_center_permission_acta_id_solution_c~` ON `sigocalco_meetings_acta_center_permission` (`acta_id`, `solution_center_code`);


CREATE INDEX `IX_sigocalco_meetings_acta_center_permission_solution_center_co~` ON `sigocalco_meetings_acta_center_permission` (`solution_center_code`, `revoked_at`, `acta_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_acta_participant_acta_id_participant_id` ON `sigocalco_meetings_acta_participant` (`acta_id`, `participant_id`);


CREATE INDEX `IX_sigocalco_meetings_acta_participant_participant_id` ON `sigocalco_meetings_acta_participant` (`participant_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_acta_section_acta_id_template_section_id` ON `sigocalco_meetings_acta_section` (`acta_id`, `template_section_id`);


CREATE INDEX `IX_sigocalco_meetings_acta_section_acta_id_template_version_id` ON `sigocalco_meetings_acta_section` (`acta_id`, `template_version_id`);


CREATE INDEX `IX_sigocalco_meetings_acta_section_template_section_id_template~` ON `sigocalco_meetings_acta_section` (`template_section_id`, `template_version_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_acta_status_name` ON `sigocalco_meetings_acta_status` (`name`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_acta_type_code` ON `sigocalco_meetings_acta_type` (`code`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_acta_type_name` ON `sigocalco_meetings_acta_type` (`name`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_acta_user_permission_acta_id_user_id` ON `sigocalco_meetings_acta_user_permission` (`acta_id`, `user_id`);


CREATE INDEX `IX_sigocalco_meetings_acta_user_permission_user_id_revoked_at_a~` ON `sigocalco_meetings_acta_user_permission` (`user_id`, `revoked_at`, `acta_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_meeting_acta_id` ON `sigocalco_meetings_meeting` (`acta_id`);


CREATE INDEX `IX_sigocalco_meetings_meeting_meeting_status_id` ON `sigocalco_meetings_meeting` (`meeting_status_id`);


CREATE INDEX `IX_sigocalco_meetings_meeting_start_at_utc_end_at_utc` ON `sigocalco_meetings_meeting` (`start_at_utc`, `end_at_utc`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_meeting_calendar_meeting_id` ON `sigocalco_meetings_meeting_calendar` (`meeting_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_meeting_reservation_external_reservation_~` ON `sigocalco_meetings_meeting_reservation` (`external_reservation_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_meeting_reservation_meeting_id` ON `sigocalco_meetings_meeting_reservation` (`meeting_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_meeting_status_name` ON `sigocalco_meetings_meeting_status` (`name`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_participant_email` ON `sigocalco_meetings_participant` (`email`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_permission_permission_key` ON `sigocalco_meetings_permission` (`permission_key`);


CREATE INDEX `IX_sigocalco_meetings_template_acta_type_id_scope_solution_cent~` ON `sigocalco_meetings_template` (`acta_type_id`, `scope`, `solution_center_code`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_template_section_template_version_id_sort~` ON `sigocalco_meetings_template_section` (`template_version_id`, `sort_order`);


CREATE INDEX `IX_sigocalco_meetings_template_version_template_id_acta_type_id` ON `sigocalco_meetings_template_version` (`template_id`, `acta_type_id`);


CREATE UNIQUE INDEX `IX_sigocalco_meetings_template_version_template_id_version_numb~` ON `sigocalco_meetings_template_version` (`template_id`, `version_number`);


