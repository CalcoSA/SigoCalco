CREATE TABLE `sigocalco_meetings_meeting_notification` (
    `meeting_notification_id` bigint NOT NULL AUTO_INCREMENT,
    `meeting_id` int NOT NULL,
    `recipient_email` varchar(254) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `recipient_name` varchar(250) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `message_id` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `subject` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `text_body` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `html_body` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
    `status` enum('PENDIENTE','PROCESANDO','ENVIADO','ERROR','INCIERTO','SIMULADO') CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDIENTE',
    `last_error` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
    `attempts` int NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    `accepted_at` datetime(6) NULL,
    CONSTRAINT `PK_sigocalco_meetings_meeting_notification` PRIMARY KEY (`meeting_notification_id`),
    CONSTRAINT `FK_sigocalco_meetings_meeting_notification_sigocalco_meetings_m~` FOREIGN KEY (`meeting_id`) REFERENCES `sigocalco_meetings_meeting` (`meeting_id`) ON DELETE RESTRICT
) ENGINE=InnoDB CHARACTER SET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE UNIQUE INDEX `IX_sigocalco_meetings_meeting_notification_meeting_id_recipient~` ON `sigocalco_meetings_meeting_notification` (`meeting_id`, `recipient_email`);

CREATE UNIQUE INDEX `IX_sigocalco_meetings_meeting_notification_message_id` ON `sigocalco_meetings_meeting_notification` (`message_id`);

CREATE INDEX `IX_sigocalco_meetings_meeting_notification_status_created_at` ON `sigocalco_meetings_meeting_notification` (`status`, `created_at`);
