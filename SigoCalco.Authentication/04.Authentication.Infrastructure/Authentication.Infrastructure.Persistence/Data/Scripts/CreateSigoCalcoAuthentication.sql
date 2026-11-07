-- SigoCalco.Authentication: instalación inicial, MySQL 8 / InnoDB.
-- REVISAR Y EJECUTAR MANUALMENTE. La aplicación no ejecuta este archivo.
-- La base bdcalco_sigocalco debe existir; las ocho tablas deben estar ausentes.
-- No modifica WordPress, SitraCalco, Meetings ni Reservations.
-- Detenerse ante cualquier error: el DDL de MySQL realiza commits implícitos.
-- Sin DROP, ALTER, borrado de datos ni desactivación de claves foráneas.
-- email obligatorio (no único), centro obligatorio para todos los roles.
-- La creación usuario + rol + centro es atómica en el repositorio.
-- El administrador debe respetar esa misma regla al cargar datos por SQL.
-- Log: CURRENT_TIMESTAMP(6) mantiene la precisión del DATETIME(6).
USE `bdcalco_sigocalco`;

CREATE TABLE `sigocalco_authentication_log` (
    `log_id` BIGINT NOT NULL AUTO_INCREMENT,
    `action` VARCHAR(50) NOT NULL,
    `module` VARCHAR(100) NOT NULL,
    `description` VARCHAR(500) NOT NULL,
    `user_name` VARCHAR(100) NOT NULL,
    `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`log_id`)
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `sigocalco_authentication_menuoption` (
    `IdMenuOption` INT NOT NULL AUTO_INCREMENT,
    `nameMenuOption` VARCHAR(100) NOT NULL,
    `pathMenuOption` VARCHAR(200) NULL DEFAULT NULL,
    `parentMenuOption` INT NULL DEFAULT NULL,
    `orderMenuOption` INT NOT NULL,
    `statusMenuOption` TINYINT NOT NULL DEFAULT '1',
    PRIMARY KEY (`IdMenuOption`),
    KEY `fk_sigocalco_authentication_menu_parent` (`parentMenuOption`),
    CONSTRAINT `fk_sigocalco_authentication_menu_parent`
        FOREIGN KEY (`parentMenuOption`)
        REFERENCES `sigocalco_authentication_menuoption` (`IdMenuOption`)
        ON DELETE SET NULL ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `sigocalco_authentication_role` (
    `IdRole` INT NOT NULL AUTO_INCREMENT,
    `nameRole` VARCHAR(100) NOT NULL,
    `role_code` VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NULL DEFAULT NULL,
    `statusRole` TINYINT NOT NULL DEFAULT '1',
    PRIMARY KEY (`IdRole`),
    UNIQUE KEY `uq_sigocalco_authentication_name` (`nameRole`),
    UNIQUE KEY `uq_sigocalco_authentication_role_code` (`role_code`)
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `sigocalco_authentication_solution_center` (
    `solution_center_id` INT NOT NULL AUTO_INCREMENT,
    `solution_center_code` VARCHAR(64) NOT NULL,
    `solution_center_name` VARCHAR(200) NOT NULL,
    PRIMARY KEY (`solution_center_id`),
    UNIQUE KEY `uq_sigocalco_authentication_center_code` (`solution_center_code`)
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `sigocalco_authentication_users` (
    `IdUser` INT NOT NULL AUTO_INCREMENT,
    `wordpressUserId` BIGINT UNSIGNED NOT NULL,
    `userLogin` VARCHAR(60) NOT NULL,
    `userName` VARCHAR(250) NOT NULL,
    `email` VARCHAR(254) NOT NULL,
    `statusUser` TINYINT(1) NOT NULL DEFAULT '1',
    PRIMARY KEY (`IdUser`),
    UNIQUE KEY `uq_sigocalco_authentication_users_user_login` (`userLogin`),
    UNIQUE KEY `uq_sigocalco_authentication_users_user_wordpress_id` (`wordpressUserId`)
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `sigocalco_authentication_rolemenuoption` (
    `IdRoleMenuOption` INT NOT NULL AUTO_INCREMENT,
    `IdRole` INT NOT NULL,
    `IdMenuOption` INT NOT NULL,
    PRIMARY KEY (`IdRoleMenuOption`),
    UNIQUE KEY `uq_sigocalco_authentication_role_menu_option` (`IdRole`, `IdMenuOption`),
    KEY `fk_sigocalco_authentication_role_menu_option_menu_option` (`IdMenuOption`),
    CONSTRAINT `fk_sigocalco_authentication_role_menu_option_menu_option`
        FOREIGN KEY (`IdMenuOption`)
        REFERENCES `sigocalco_authentication_menuoption` (`IdMenuOption`)
        ON DELETE CASCADE ON UPDATE RESTRICT,
    CONSTRAINT `fk_sigocalco_authentication_role_menu_option_role`
        FOREIGN KEY (`IdRole`)
        REFERENCES `sigocalco_authentication_role` (`IdRole`)
        ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `sigocalco_authentication_sessions` (
    `id_session` VARCHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `id_user` INT NOT NULL,
    `created_at` DATETIME(6) NOT NULL,
    `last_activity_at` DATETIME(6) NOT NULL,
    `expires_at` DATETIME(6) NOT NULL,
    `revoked_at` DATETIME(6) NULL DEFAULT NULL,
    `refresh_token_hash` VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL DEFAULT NULL,
    PRIMARY KEY (`id_session`),
    KEY `ix_sigocalco_authentication_sessions_user` (`id_user`),
    CONSTRAINT `fk_sigocalco_authentication_sessions_user`
        FOREIGN KEY (`id_user`)
        REFERENCES `sigocalco_authentication_users` (`IdUser`)
        ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

CREATE TABLE `sigocalco_authentication_userrole` (
    `IdUserRole` INT NOT NULL AUTO_INCREMENT,
    `IdUser` INT NOT NULL,
    `IdRole` INT NOT NULL,
    `solution_center_id` INT NOT NULL,
    PRIMARY KEY (`IdUserRole`),
    UNIQUE KEY `uq_sigocalco_authentication_user_role` (`IdUser`, `IdRole`),
    KEY `fk_sigocalco_authentication_user_role_role` (`IdRole`),
    KEY `ix_sigocalco_authentication_userrole_center` (`solution_center_id`),
    CONSTRAINT `fk_sigocalco_authentication_user_role_user`
        FOREIGN KEY (`IdUser`)
        REFERENCES `sigocalco_authentication_users` (`IdUser`)
        ON DELETE CASCADE ON UPDATE RESTRICT,
    CONSTRAINT `fk_sigocalco_authentication_user_role_role`
        FOREIGN KEY (`IdRole`)
        REFERENCES `sigocalco_authentication_role` (`IdRole`)
        ON DELETE RESTRICT ON UPDATE RESTRICT,
    CONSTRAINT `fk_sigocalco_authentication_userrole_center`
        FOREIGN KEY (`solution_center_id`)
        REFERENCES `sigocalco_authentication_solution_center` (`solution_center_id`)
        ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARACTER SET=utf8mb4;

-- Cargar únicamente catálogos, roles, menús y primer usuario aprobados.
-- El rol funcional PDV, si se utiliza, conserva role_code='PDV'; su ID no es fijo.
-- No se incluyen usuarios, credenciales, centros ficticios ni permisos inventados.
-- wordpressUserId no tiene FK externa y WordPress es de solo lectura.

SELECT TABLE_NAME, ENGINE, TABLE_COLLATION
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'bdcalco_sigocalco'
  AND TABLE_NAME IN (
    'sigocalco_authentication_log', 'sigocalco_authentication_menuoption',
    'sigocalco_authentication_role', 'sigocalco_authentication_solution_center',
    'sigocalco_authentication_users', 'sigocalco_authentication_rolemenuoption',
    'sigocalco_authentication_sessions', 'sigocalco_authentication_userrole')
ORDER BY TABLE_NAME;
