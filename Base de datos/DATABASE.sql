-- 1. Crear la base de datos y seleccionarla
-- =============================================
IF DB_ID('enfermeria') IS NULL
BEGIN
    CREATE DATABASE enfermeria;
END;
GO

USE enfermeria;
GO


-- =============================================
-- 2. Tabla: enf_personas
-- =============================================
CREATE TABLE enf_personas (
    id INT IDENTITY(1,1) PRIMARY KEY,
    cedula VARCHAR(20) NOT NULL UNIQUE,
    nombre VARCHAR(100) NOT NULL,
    telefono VARCHAR(20) NULL,
    email VARCHAR(100) UNIQUE NULL,
    usuario VARCHAR(50) NOT NULL UNIQUE,
    password VARCHAR(255) NOT NULL,
    departamento VARCHAR(100) NULL,
    tipo VARCHAR(20) NOT NULL 
        CONSTRAINT chk_tipo_persona 
        CHECK (tipo IN ('Estudiante','Funcionario','Profesor','Asistente')),
    seccion VARCHAR(50) NULL,
    fecha_nacimiento DATE NULL,
    sexo CHAR(1) NOT NULL 
        CONSTRAINT chk_sexo_persona 
        CHECK (sexo IN ('M','F'))
);
GO


-- =============================================
-- 3. Tabla: enf_horarios
-- =============================================
CREATE TABLE enf_horarios (
    id INT IDENTITY(1,1) PRIMARY KEY,
    fecha DATE NOT NULL,
    hora TIME NOT NULL,
    estado VARCHAR(50) NOT NULL,
    fecha_creacion DATETIME NOT NULL 
        CONSTRAINT df_horarios_fecha_creacion 
        DEFAULT GETDATE(),
    usuario_creacion VARCHAR(50) NOT NULL,
    fecha_modificacion DATETIME NULL,
    usuario_modificacion VARCHAR(50) NULL
);
GO

-- Trigger para actualizar fecha_modificacion al hacer UPDATE
CREATE TRIGGER trg_enf_horarios_update
ON enf_horarios
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE H
    SET 
        fecha_modificacion = GETDATE()
    FROM enf_horarios AS H
    INNER JOIN inserted AS I 
        ON H.id = I.id;
END;
GO


-- =============================================
-- 4. Tabla: enf_citas
-- =============================================
CREATE TABLE enf_citas (
    id INT IDENTITY(1,1) PRIMARY KEY,
    id_persona INT NULL,
    id_horario INT NOT NULL,
    hora_llegada TIME NULL,
    hora_salida TIME NULL,
    id_profe_llegada INT NULL,
    id_profe_salida INT NULL,
    mensaje_llegada VARCHAR(MAX) NULL,
    mensaje_salida VARCHAR(MAX) NULL,
    estado VARCHAR(20) NOT NULL 
        CONSTRAINT chk_estado_cita 
        DEFAULT 'Creada' 
        CHECK (estado IN ('Creada','Llegada','Completada','Cancelada')),
    fecha_creacion DATETIME NOT NULL 
        CONSTRAINT df_citas_fecha_creacion 
        DEFAULT GETDATE(),
    usuario_creacion VARCHAR(50) NOT NULL,
    fecha_modificacion DATETIME NULL,
    usuario_modificacion VARCHAR(50) NULL,

    -- Claves foráneas 
    CONSTRAINT fk_cita_persona 
        FOREIGN KEY (id_persona) 
        REFERENCES enf_personas(id),

    CONSTRAINT fk_cita_horario 
        FOREIGN KEY (id_horario) 
        REFERENCES enf_horarios(id),

    CONSTRAINT fk_profe_llegada 
        FOREIGN KEY (id_profe_llegada) 
        REFERENCES enf_personas(id),

    CONSTRAINT fk_profe_salida 
        FOREIGN KEY (id_profe_salida) 
        REFERENCES enf_personas(id)
);
GO

-- Trigger para actualizar fecha_modificacion al hacer UPDATE
CREATE TRIGGER trg_enf_citas_update
ON enf_citas
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE C
    SET 
        fecha_modificacion = GETDATE()
    FROM enf_citas AS C
    INNER JOIN inserted AS I 
        ON C.id = I.id;
END;
GO




-- Habilitar sa
ALTER LOGIN [sa] ENABLE;
-- Establecer o cambiar contraseña
ALTER LOGIN [sa] WITH PASSWORD = 'Infomaniacos2025!';
GO

USE enfermeria;
-- 1?? Eliminar la restricción CHECK existente
ALTER TABLE enf_personas 
DROP CONSTRAINT chk_tipo_persona;
GO




--------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------

USE enfermeria;

DECLARE @sql NVARCHAR(MAX);

-- Encontrar el nombre del constraint aunque tenga nombre raro
SELECT @sql = 'ALTER TABLE enf_personas DROP CONSTRAINT ' + QUOTENAME(name)
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('enf_personas')
  AND name LIKE '%chk_tipo_persona%';

IF @sql IS NOT NULL
    EXEC(@sql);



UPDATE enf_personas
SET tipo = 'Consultorio'
WHERE tipo IN ('Asistente', 'Doctor');

ALTER TABLE enf_personas 
ADD CONSTRAINT chk_tipo_persona 
CHECK (
    tipo IN (
        'Estudiante',
        'Funcionario',
        'Profesor',
        'Consultorio',
        'Administrativo'
    )
);
