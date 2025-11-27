USE enfermeria;
GO

-- Seguridad: hacer backup rápido de la tabla (opcional pero recomendado)
SELECT TOP 0 * INTO enf_personas_backup_empty FROM enf_personas; -- crea estructura vacía (opcional)
-- Si quieres respaldo con datos (si la tabla no es muy grande):
-- SELECT * INTO enf_personas_backup_full FROM enf_personas;

-- Agregamos columna Activo sólo si no existe
IF COL_LENGTH('enf_personas', 'Activo') IS NULL
BEGIN
    ALTER TABLE enf_personas
    ADD Activo BIT NOT NULL CONSTRAINT DF_enf_personas_Activo DEFAULT(1);
    PRINT 'Columna Activo creada y con valor por defecto 1 (TRUE)';
END
ELSE
BEGIN
    PRINT 'La columna Activo ya existe. No se hicieron cambios.';
END
GO

-- Verificar las primeras filas y la nueva columna
SELECT TOP 20 id, usuario, password, Activo FROM enf_personas;
GO

