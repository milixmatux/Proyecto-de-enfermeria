using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Enfermeria_app.Models;

public partial class EnfPersona
{
    public int Id { get; set; }

    public string Cedula { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public string Usuario { get; set; } = null!;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string Password { get; set; } = null!;

    public string? Departamento { get; set; }

    public string Tipo { get; set; } = null!;

    public string? Seccion { get; set; }

    public DateOnly? FechaNacimiento { get; set; }

    [MaxLength(10)] 

    public string Sexo { get; set; } = null!;

    public bool Activo { get; set; } = true;

    public virtual ICollection<EnfCita> EnfCitaIdPersonaNavigations { get; set; } = new List<EnfCita>();

    public virtual ICollection<EnfCita> EnfCitaIdProfeLlegadaNavigations { get; set; } = new List<EnfCita>();

    public virtual ICollection<EnfCita> EnfCitaIdProfeSalidaNavigations { get; set; } = new List<EnfCita>();
}
