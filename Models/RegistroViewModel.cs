using System.ComponentModel.DataAnnotations;

namespace Enfermeria_app.Models
{
    public class RegistroViewModel
    {
        [Required]
        public string Cedula { get; set; }

        [Required]
        public string Nombre { get; set; }

        public string? Telefono { get; set; }

        public string? Email { get; set; }

        [Required]
        public string Usuario { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string? Departamento { get; set; }

        [Required]
        public string Tipo { get; set; }

        public string? Seccion { get; set; }

        [Required]
        public DateOnly FechaNacimiento { get; set; }  

        [Required]
        public string Sexo { get; set; } 
    }
}
