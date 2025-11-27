-- Habilitar sa
ALTER LOGIN [sa] ENABLE;
-- Establecer o cambiar contraseña
ALTER LOGIN [sa] WITH PASSWORD = 'Infomaniacos2025!';
GO
SELECT SERVERPROPERTY('IsIntegratedSecurityOnly') AS SoloAutenticacionWindows;