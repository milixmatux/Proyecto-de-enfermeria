using System.ComponentModel.DataAnnotations;

namespace Enfermeria_app.Models
{
    public class LoginViewModel
    {
        [Required]
        public string Usuario { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Contraseña { get; set; }
    }
}
